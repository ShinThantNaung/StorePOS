# Store POS: Entity Relationship Diagram

The database is one local SQLite file (`%LOCALAPPDATA%\StorePOS\store.db`), schema version 4.
The tables are created and upgraded in [`StorePOS/Data/Db.cs`](../StorePOS/Data/Db.cs).

```mermaid
erDiagram
    products {
        INTEGER id PK
        TEXT name
        TEXT name_key UK "name + company + size, for uniqueness"
        TEXT category "pesticide | herbicide | fertilizer | seed | other"
        TEXT unit "bottle | bag (older rows may hold typed text)"
        INTEGER price "whole kyat, >= 0"
        INTEGER sold_by_weight "0 / 1"
        INTEGER active "0 / 1"
        TEXT company "v2, optional"
        TEXT size "v2, optional"
    }

    customers {
        INTEGER id PK
        TEXT name
        TEXT address
        TEXT phone "nullable"
        TEXT created_at
    }

    sales {
        INTEGER id PK
        TEXT sold_at
        INTEGER total
        INTEGER amount_paid
        INTEGER cash_tendered
        INTEGER customer_id FK "nullable (walk-in)"
        TEXT status "saved | voided"
        TEXT void_reason "nullable"
        TEXT voided_at "nullable"
    }

    sale_items {
        INTEGER id PK
        INTEGER sale_id FK
        INTEGER product_id FK
        TEXT product_name "copied at sale time"
        TEXT unit "copied at sale time"
        INTEGER sold_by_weight
        INTEGER quantity_milli "thousandths, > 0"
        INTEGER unit_price "price charged"
        INTEGER list_price "price on the list"
        INTEGER line_total
    }

    debts {
        INTEGER id PK
        INTEGER customer_id FK
        INTEGER sale_id FK,UK "one debt per sale"
        INTEGER original_amount "> 0"
        TEXT created_at
    }

    repayments {
        INTEGER id PK
        INTEGER customer_id FK
        INTEGER amount "> 0"
        TEXT paid_on
        TEXT created_at
        INTEGER reversed "0 / 1"
        TEXT reverse_reason "nullable"
        TEXT reversed_at "nullable"
    }

    repayment_allocations {
        INTEGER id PK
        INTEGER repayment_id FK
        INTEGER debt_id FK
        INTEGER amount "> 0"
    }

    stock_ins {
        INTEGER id PK
        TEXT received_at
        TEXT supplier
        TEXT note
        TEXT created_at
    }

    stock_in_items {
        INTEGER id PK
        INTEGER stock_in_id FK
        INTEGER product_id FK
        INTEGER quantity_milli "> 0"
    }

    stock_movements {
        INTEGER id PK
        INTEGER product_id FK
        TEXT moved_at
        INTEGER change_milli "signed, <> 0"
        TEXT kind "sale | void | stock_in | adjustment"
        INTEGER sale_id FK "nullable"
        INTEGER stock_in_id FK "nullable"
        TEXT reason "damaged | count_wrong | expired | other"
        TEXT note
    }

    price_changes {
        INTEGER id PK
        INTEGER product_id FK
        TEXT changed_at
        INTEGER old_price "null for the first price"
        INTEGER new_price ">= 0"
        TEXT source "added | edited | csv_import"
        TEXT file_name "CSV file name, if imported"
    }

    customers |o--o{ sales : buys
    sales ||--|{ sale_items : contains
    products ||--o{ sale_items : "sold as"
    customers ||--o{ debts : owes
    sales ||--o| debts : "unpaid part becomes"
    customers ||--o{ repayments : pays
    repayments ||--o{ repayment_allocations : "split into"
    debts ||--o{ repayment_allocations : "paid off by"
    stock_ins ||--|{ stock_in_items : contains
    products ||--o{ stock_in_items : delivered
    products ||--o{ stock_movements : "stock changes"
    sales |o--o{ stock_movements : "sale / void"
    stock_ins |o--o{ stock_movements : "stock in"
    products ||--o{ price_changes : "price history"
```

## Rules the diagram does not show

- **Money** is stored in whole kyat (`INTEGER`). **Quantities** are stored in thousandths (`*_milli`), so 2.5 kg is `2500`.
- **Dates** are text: `yyyy-MM-dd HH:mm:ss` for times and `yyyy-MM-dd` for `repayments.paid_on`.
- **Stock is never stored.** A product's stock is `SUM(stock_movements.change_milli)`. Rows in `stock_movements` are never edited or deleted.
  - A sale writes one `sale` row per item (negative change). Voiding the sale writes matching `void` rows (positive change).
  - A delivery writes one `stock_ins` row, its `stock_in_items`, and one `stock_in` movement per item.
  - Adjust count writes one `adjustment` movement with a `reason`.
- **A customer's balance** is the sum of `debts.original_amount` for saved (not voided) sales, minus the `repayment_allocations` of repayments that are not reversed.
- **`repayment_allocations`** are rebuilt whenever a repayment is added or reversed, or a credit sale is voided: active repayments pay off open debts, oldest sale first.
- **Nothing is deleted.** Sales are voided with a reason, and repayments are reversed with a reason.
- **`sale_items` copies** the product name, unit and prices, so later edits to a product do not change past sales.
- **`price_changes`** gets a row in the same transaction as every product insert or price change. Products from before schema version 4 have no rows until their price next changes.

## Schema versions

| Version | Adds |
|---|---|
| 1 | `products`, `customers`, `sales`, `sale_items`, `debts`, `repayments`, `repayment_allocations` |
| 2 | `products.company`, `products.size` |
| 3 | `stock_ins`, `stock_in_items`, `stock_movements` |
| 4 | `price_changes` |

`Db.EnsureCreated()` reads `PRAGMA user_version` and applies each missing step in one transaction, so an older database upgrades itself when the app opens it.
