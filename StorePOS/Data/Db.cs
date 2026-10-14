using Microsoft.Data.Sqlite;

namespace StorePOS.Data;

/// <summary>The local SQLite file. Every write that touches more than one row runs in one transaction.</summary>
public sealed class Db(string filePath)
{
    public const int SchemaVersion = 4;

    public string FilePath { get; } = filePath;

    public SqliteConnection OpenConnection()
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
        }.ToString();
        var conn = new SqliteConnection(cs);
        conn.Open();
        using var cmd = conn.CreateCommand();
        // FULL: a power cut after Commit() cannot lose the sale.
        cmd.CommandText = "PRAGMA synchronous=FULL;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    public Session Read() => new(OpenConnection(), transaction: false);
    public Session Write() => new(OpenConnection(), transaction: true);

    public void EnsureCreated()
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using var s = Write();
        var version = s.Scalar<long>("PRAGMA user_version");
        if (version < 1) s.Exec(Schema);
        if (version < 2) s.Exec(SchemaV2);
        if (version < 3) s.Exec(SchemaV3);
        if (version < 4) s.Exec(SchemaV4);
        if (version < SchemaVersion) s.Exec($"PRAGMA user_version={SchemaVersion}");
        s.Commit();
    }

    /// <summary>
    /// Version 2: a product's company and size. name_key now holds name + company + size
    /// (ProductService.IdentityKey), so the same name may exist for another company or size.
    /// For products with neither it is still the plain name key, so old rows need no update.
    /// </summary>
    private const string SchemaV2 = """
        ALTER TABLE products ADD COLUMN company TEXT NOT NULL DEFAULT '';
        ALTER TABLE products ADD COLUMN size TEXT NOT NULL DEFAULT '';
        """;

    /// <summary>
    /// Version 3: stock. Every change to a product's stock is one stock_movements row and the
    /// stock is their sum. Sales saved before this version have no movements, so stock starts
    /// at zero until the opening count is entered with Adjust count.
    /// </summary>
    private const string SchemaV3 = """
        CREATE TABLE IF NOT EXISTS stock_ins(
            id INTEGER PRIMARY KEY,
            received_at TEXT NOT NULL,
            supplier TEXT NOT NULL DEFAULT '',
            note TEXT NOT NULL DEFAULT '',
            created_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_stock_ins_received ON stock_ins(received_at);
        CREATE TABLE IF NOT EXISTS stock_in_items(
            id INTEGER PRIMARY KEY,
            stock_in_id INTEGER NOT NULL REFERENCES stock_ins(id),
            product_id INTEGER NOT NULL REFERENCES products(id),
            quantity_milli INTEGER NOT NULL CHECK(quantity_milli > 0)
        );
        CREATE INDEX IF NOT EXISTS ix_stock_in_items_stock_in ON stock_in_items(stock_in_id);
        CREATE TABLE IF NOT EXISTS stock_movements(
            id INTEGER PRIMARY KEY,
            product_id INTEGER NOT NULL REFERENCES products(id),
            moved_at TEXT NOT NULL,
            change_milli INTEGER NOT NULL CHECK(change_milli <> 0),
            kind TEXT NOT NULL CHECK(kind IN ('sale','void','stock_in','adjustment')),
            sale_id INTEGER REFERENCES sales(id),
            stock_in_id INTEGER REFERENCES stock_ins(id),
            reason TEXT CHECK(reason IS NULL OR reason IN ('damaged','count_wrong','expired','other')),
            note TEXT NOT NULL DEFAULT ''
        );
        -- Holds every column the stock totals and running balances read, so they never touch the table.
        CREATE INDEX IF NOT EXISTS ix_stock_movements_product ON stock_movements(product_id, moved_at, id, change_milli, kind);
        CREATE INDEX IF NOT EXISTS ix_stock_movements_sale ON stock_movements(sale_id) WHERE sale_id IS NOT NULL;
        CREATE INDEX IF NOT EXISTS ix_stock_movements_stock_in ON stock_movements(stock_in_id) WHERE stock_in_id IS NOT NULL;
        """;

    /// <summary>
    /// Version 4: price history (PR8). One row per price a product has had, written in the same
    /// transaction as the product. Products from before this version have no rows until their
    /// price next changes, so their "Price changed" date stays empty rather than being made up.
    /// </summary>
    private const string SchemaV4 = """
        CREATE TABLE IF NOT EXISTS price_changes(
            id INTEGER PRIMARY KEY,
            product_id INTEGER NOT NULL REFERENCES products(id),
            changed_at TEXT NOT NULL,
            old_price INTEGER,
            new_price INTEGER NOT NULL CHECK(new_price >= 0),
            source TEXT NOT NULL CHECK(source IN ('added','edited','csv_import')),
            file_name TEXT NOT NULL DEFAULT ''
        );
        CREATE INDEX IF NOT EXISTS ix_price_changes_product ON price_changes(product_id, changed_at, id);
        """;

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS products(
            id INTEGER PRIMARY KEY,
            name TEXT NOT NULL,
            name_key TEXT NOT NULL UNIQUE,
            category TEXT NOT NULL CHECK(category IN ('pesticide','herbicide','fertilizer','seed','other')),
            unit TEXT NOT NULL,
            price INTEGER NOT NULL CHECK(price >= 0),
            sold_by_weight INTEGER NOT NULL DEFAULT 0,
            active INTEGER NOT NULL DEFAULT 1
        );
        CREATE TABLE IF NOT EXISTS customers(
            id INTEGER PRIMARY KEY,
            name TEXT NOT NULL,
            address TEXT NOT NULL DEFAULT '',
            phone TEXT,
            created_at TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS sales(
            id INTEGER PRIMARY KEY,
            sold_at TEXT NOT NULL,
            total INTEGER NOT NULL,
            amount_paid INTEGER NOT NULL,
            cash_tendered INTEGER NOT NULL,
            customer_id INTEGER REFERENCES customers(id),
            status TEXT NOT NULL DEFAULT 'saved' CHECK(status IN ('saved','voided')),
            void_reason TEXT,
            voided_at TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_sales_sold_at ON sales(sold_at);
        CREATE TABLE IF NOT EXISTS sale_items(
            id INTEGER PRIMARY KEY,
            sale_id INTEGER NOT NULL REFERENCES sales(id),
            product_id INTEGER NOT NULL REFERENCES products(id),
            product_name TEXT NOT NULL,
            unit TEXT NOT NULL,
            sold_by_weight INTEGER NOT NULL,
            quantity_milli INTEGER NOT NULL CHECK(quantity_milli > 0),
            unit_price INTEGER NOT NULL,
            list_price INTEGER NOT NULL,
            line_total INTEGER NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_sale_items_sale ON sale_items(sale_id);
        CREATE TABLE IF NOT EXISTS debts(
            id INTEGER PRIMARY KEY,
            customer_id INTEGER NOT NULL REFERENCES customers(id),
            sale_id INTEGER NOT NULL UNIQUE REFERENCES sales(id),
            original_amount INTEGER NOT NULL CHECK(original_amount > 0),
            created_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_debts_customer ON debts(customer_id);
        CREATE TABLE IF NOT EXISTS repayments(
            id INTEGER PRIMARY KEY,
            customer_id INTEGER NOT NULL REFERENCES customers(id),
            amount INTEGER NOT NULL CHECK(amount > 0),
            paid_on TEXT NOT NULL,
            created_at TEXT NOT NULL,
            reversed INTEGER NOT NULL DEFAULT 0,
            reverse_reason TEXT,
            reversed_at TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_repayments_customer ON repayments(customer_id);
        CREATE TABLE IF NOT EXISTS repayment_allocations(
            id INTEGER PRIMARY KEY,
            repayment_id INTEGER NOT NULL REFERENCES repayments(id),
            debt_id INTEGER NOT NULL REFERENCES debts(id),
            amount INTEGER NOT NULL CHECK(amount > 0)
        );
        CREATE INDEX IF NOT EXISTS ix_alloc_repayment ON repayment_allocations(repayment_id);
        CREATE INDEX IF NOT EXISTS ix_alloc_debt ON repayment_allocations(debt_id);
        """;
}

/// <summary>A connection with an optional transaction. Dispose without Commit() rolls back.</summary>
public sealed class Session : IDisposable
{
    private readonly SqliteConnection _conn;
    private SqliteTransaction? _tx;

    internal Session(SqliteConnection conn, bool transaction)
    {
        _conn = conn;
        if (transaction) _tx = conn.BeginTransaction();
    }

    private SqliteCommand Command(string sql, (string Name, object? Value)[] ps)
    {
        var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = _tx;
        foreach (var (name, value) in ps) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    public int Exec(string sql, params (string, object?)[] ps)
    {
        using var cmd = Command(sql, ps);
        return cmd.ExecuteNonQuery();
    }

    public T Scalar<T>(string sql, params (string, object?)[] ps)
    {
        using var cmd = Command(sql, ps);
        var result = cmd.ExecuteScalar();
        if (result == null || result is DBNull) return default!;
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(result, target);
    }

    public List<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string, object?)[] ps)
    {
        using var cmd = Command(sql, ps);
        using var reader = cmd.ExecuteReader();
        var list = new List<T>();
        while (reader.Read()) list.Add(map(reader));
        return list;
    }

    public long Insert(string sql, params (string, object?)[] ps)
    {
        Exec(sql, ps);
        return Scalar<long>("SELECT last_insert_rowid()");
    }

    public void Commit()
    {
        _tx?.Commit();
        _tx?.Dispose();
        _tx = null;
    }

    public void Dispose()
    {
        _tx?.Dispose();
        _conn.Dispose();
    }
}

public static class ReaderExt
{
    public static string? StrOrNull(this SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    public static long? LongOrNull(this SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetInt64(i);
    public static bool Bool(this SqliteDataReader r, int i) => r.GetInt64(i) != 0;
}
