# Store POS: Low-Level Design (UML class diagrams)

Store POS is a WPF app in the MVVM pattern. The code has four layers, and each layer only calls the one below it:

| Layer | Folder | Job |
|---|---|---|
| Views | `Views/`, `Dialogs/`, `Controls/`, `MainWindow.xaml` | XAML screens and dialogs, bound to view models |
| View models | `ViewModels/` | Screen state, filtering and commands |
| Services | `Services/` | Business rules; every multi-row write is one transaction |
| Data | `Data/Db.cs` | SQLite connection, schema and upgrades |

`Models/` holds the plain data classes that pass between the layers, and `Infrastructure/` holds shared helpers (formatting, translations, MVVM base classes).

The diagrams below are split by layer so each stays readable. Private fields and helper methods are left out.

## 1. Overview

```mermaid
classDiagram
    direction LR
    class App {
        +Services$ AppServices
        #OnStartup(e)
    }
    class MainWindow
    class MainViewModel
    class AppServices
    class Db
    class AppSettings

    App --> AppServices : creates
    App --> MainWindow : creates
    MainWindow ..> MainViewModel : DataContext
    MainViewModel --> AppServices
    AppServices *-- Db
    AppServices *-- AppSettings
    AppServices *-- ProductService
    AppServices *-- SaleService
    AppServices *-- DebtService
    AppServices *-- ReportService
    AppServices *-- CsvImportService
    AppServices *-- StockService
    AppServices *-- BackupService
```

## 2. Data layer and services

```mermaid
classDiagram
    class AppServices {
        +DataFolder string
        +Settings AppSettings
        +Clock Func~DateTime~
        +Db Db
        +Products ProductService
        +Sales SaleService
        +Debts DebtService
        +Reports ReportService
        +Import CsvImportService
        +Stock StockService
        +Backup BackupService
        +AppServices(dataFolder, settings, clock)
        +SettingsPath(dataFolder)$ string
        +SaveSettings()
    }

    class AppSettings {
        +Language string
        +BackupFolder string?
        +LastBackupAt DateTime?
        +Load(path)$ AppSettings
        +Save(path)
    }

    class Db {
        +SchemaVersion$ int = 4
        +FilePath string
        +OpenConnection() SqliteConnection
        +Read() Session
        +Write() Session
        +EnsureCreated()
    }

    class Session {
        <<IDisposable>>
        +Exec(sql, params) int
        +Scalar~T~(sql, params) T
        +Query~T~(sql, map, params) List~T~
        +Insert(sql, params) long
        +Commit()
        +Dispose()
    }

    class ProductService {
        +List(includeInactive) List~Product~
        +Get(id) Product?
        +Save(Product) long
        +PriceHistory(productId) List~PriceChange~
        ~IdentityKey(name, company, size)$ string
        ~RecordPriceChange(Session, productId, oldPrice, newPrice, source, fileName)$
    }

    class SaleService {
        +Save(lines, tendered, customerId) long
        +Void(saleId, reason)
        +SalesBetween(from, toExclusive) List~SaleRecord~
    }

    class DebtService {
        +Customers() List~Customer~
        +Balances() List~CustomerBalance~
        +Balance(customerId) CustomerBalance?
        +SaveCustomer(Customer) long
        +RecordRepayment(customerId, amount, date) long
        +ReverseRepayment(repaymentId, reason)
        +Ledger(customerId) List~LedgerEntry~
        ~Reallocate(Session, customerId)$
    }

    class StockService {
        +Levels() List~StockLevel~
        +OnHand(productId) decimal
        +OnHandAll() Dictionary~long, decimal~
        +Movements(productId) List~StockMovement~
        +RecordStockIn(day, supplier, note, lines) long
        +Adjust(productId, counted, reason, note) decimal
        +StockIns() List~StockInRecord~
        +Suppliers() List~string~
    }

    class ReportService {
        +Totals(from, toExclusive) ReportTotals
        +ProductSales(from, toExclusive) List~ProductSales~
        +BestSellers(all, byQuantity, take)$ List~ProductSales~
        +SlowSellers(all, take)$ List~ProductSales~
    }

    class CsvImportService {
        +Preview(path) ImportPreview
        +Preview(bytes) ImportPreview
        +Apply(ImportPreview) int
    }

    class BackupService {
        +SetFolder(folder)
        +BackupNow() string
        +Copies() List~BackupCopy~
        +Restore(sourcePath) string
    }

    class ExportService {
        <<static>>
        +SavePdf(ReportDocument, path)$
        +SaveExcel(ReportDocument, path)$
    }

    class DemoData {
        <<static>>
        +Seed(AppServices)$
    }

    Db ..> Session : opens
    ProductService --> Db
    SaleService --> Db
    SaleService ..> DebtService : Reallocate on void
    DebtService --> Db
    StockService --> Db
    StockService --> ProductService
    ReportService --> Db
    CsvImportService --> Db
    CsvImportService --> ProductService
    BackupService --> Db
    BackupService --> AppSettings
    ExportService ..> ReportDocument
```

### Import and export types

```mermaid
classDiagram
    class ImportKind {
        <<enumeration>>
        New
        PriceChange
        NoChange
    }
    class ImportPreview {
        +Rows List~ImportRow~
        +Errors List~ImportError~
        +RowsRead int
        +FileName string
    }
    class ImportRow {
        +Row int
        +Name string
        +Company string
        +Size string
        +Category Category
        +Unit string
        +Price long
        +SoldByWeight bool
        +Kind ImportKind
        +OldPrice long
    }
    class ImportError {
        +Row int
        +Message string
    }
    class ReportDocument {
        +Title string
        +Period string
        +Figures List~ReportFigure~
        +Tables List~ReportTable~
    }
    class ReportFigure {
        <<record>>
        +Label string
        +Value string
        +Note string
    }
    class ReportTable {
        +Title string
        +Headers string[]
        +Numeric bool[]
        +Rows List~object[]~
        +Footnote string?
    }
    class BackupCopy {
        <<record>>
        +Path string
        +CreatedAt DateTime
        +SizeBytes long
    }

    ImportPreview *-- ImportRow
    ImportPreview *-- ImportError
    ImportRow --> ImportKind
    ReportDocument *-- ReportFigure
    ReportDocument *-- ReportTable
```

## 3. Domain models (`Models/Models.cs`)

```mermaid
classDiagram
    class Product {
        +Id long
        +Name string
        +Company string
        +Size string
        +Category Category
        +Unit string
        +Price long
        +SoldByWeight bool
        +Active bool
        +PriceChangedAt DateTime?
        +FullName string
        +FormatFullName(name, size, company)$ string
    }
    class Category {
        <<enumeration>>
        Pesticide
        Herbicide
        Fertilizer
        Seed
        Other
    }
    class Customer {
        +Id long
        +Name string
        +Address string
        +Phone string?
        +Display string
    }
    class CustomerBalance {
        +Customer Customer
        +Balance long
    }
    class SaleLineInput {
        <<record>>
        +ProductId long
        +Name string
        +Unit string
        +SoldByWeight bool
        +Quantity decimal
        +UnitPrice long
        +ListPrice long
        +LineTotal long
    }
    class SaleRecord {
        +Id long
        +SoldAt DateTime
        +Total long
        +AmountPaid long
        +CustomerId long?
        +CustomerName string?
        +Voided bool
        +VoidReason string?
        +Items List~SaleItemRow~
        +Owed long
    }
    class SaleItemRow {
        +ProductId long
        +Name string
        +Unit string
        +SoldByWeight bool
        +Quantity decimal
        +UnitPrice long
        +LineTotal long
        +Describe() string
    }
    class LedgerKind {
        <<enumeration>>
        CreditSale
        Repayment
    }
    class LedgerEntry {
        +Kind LedgerKind
        +Id long
        +Date DateTime
        +SaleTotal long
        +SalePaid long
        +Amount long
        +Cancelled bool
        +Reason string?
        +ItemsText string
        +BalanceAfter long
    }
    class PriceSource {
        <<enumeration>>
        Added
        Edited
        CsvImport
    }
    class PriceChange {
        +Id long
        +At DateTime
        +OldPrice long?
        +NewPrice long
        +Source PriceSource
        +FileName string
    }
    class StockMoveKind {
        <<enumeration>>
        Sale
        Void
        StockIn
        Adjustment
    }
    class AdjustReason {
        <<enumeration>>
        Damaged
        CountWrong
        Expired
        Other
    }
    class StockLevel {
        +Product Product
        +OnHand decimal
        +LastStockIn DateTime?
        +IsOut bool
    }
    class StockMovement {
        +Id long
        +At DateTime
        +Kind StockMoveKind
        +Change decimal
        +After decimal
        +SaleId long?
        +CustomerName string?
        +Supplier string
        +Reason AdjustReason?
        +Note string
    }
    class StockInLineInput {
        <<record>>
        +ProductId long
        +Quantity decimal
    }
    class StockInRecord {
        +Id long
        +ReceivedAt DateTime
        +Supplier string
        +Note string
        +Lines List~StockInLine~
    }
    class StockInLine {
        +ProductId long
        +Name string
        +Quantity decimal
        +After decimal
        +Before decimal
    }
    class ProductSales {
        +ProductId long
        +Name string
        +Unit string
        +SoldByWeight bool
        +Quantity decimal
        +Amount long
    }
    class ReportTotals {
        +TotalSold long
        +CashAtSale long
        +Repayments long
        +CashReceived long
        +CreditGiven long
        +CreditSales int
        +SalesCount int
        +VoidedCount int
    }

    Product --> Category
    CustomerBalance --> Customer
    SaleRecord *-- SaleItemRow
    LedgerEntry --> LedgerKind
    PriceChange --> PriceSource
    StockLevel --> Product
    StockMovement --> StockMoveKind
    StockMovement --> AdjustReason
    StockInRecord *-- StockInLine
```

Static helpers in the same file: `Categories`, `Units` (bottle / bag codes and their display names) and `AdjustReasons` (database codes and display names).

## 4. View models

```mermaid
classDiagram
    class ObservableObject {
        <<abstract>>
        +PropertyChanged event
        #Set~T~(ref field, value, name) bool
        #OnPropertyChanged(name)
    }
    class IPage {
        <<interface>>
        +Activate()
    }
    class Section {
        <<enumeration>>
        Sales
        Prices
        Stock
        Debts
        Reports
        Backup
    }
    class MainViewModel {
        +SalesPage SalesViewModel
        +PricesPage PricesViewModel
        +StockPage StockViewModel
        +StockInsPage StockInsViewModel
        +DebtsPage DebtsViewModel
        +ReportsPage ReportsViewModel
        +BackupPage BackupViewModel
        +NavigateCommand RelayCommand
        +Navigate(Section)
        +OpenCustomer(customerId)
        +OpenStockIns()
        +DataReplaced()
        +UpdateBackupNote()
        +StartBackgroundBackup()
    }
    class SalesViewModel {
        +ProductsView ListCollectionView
        +Cart ObservableCollection~CartLine~
        +Customers ObservableCollection~CustomerOption~
        +AddToCartCommand RelayCommand
        +RemoveLineCommand RelayCommand
        +ClearCartCommand RelayCommand
        +SaveCommand RelayCommand
        +NewCustomerCommand RelayCommand
        +AddOne(ProductRow)
        +ClearAfterRestore()
    }
    class CartLine {
        +Product Product
        +ListPrice long
        +AddQuantity(more)
        +ToInput() SaleLineInput
    }
    class PricesViewModel {
        +View ListCollectionView
        +AddCommand RelayCommand
        +EditCommand RelayCommand
        +HistoryCommand RelayCommand
        +ImportCommand RelayCommand
        +Edit()
    }
    class StockViewModel {
        +View ListCollectionView
        +CategoryOptions List~FilterOption~
        +Movements List~MovementRow~
        +ShowHistoryCommand RelayCommand
        +AdjustCommand RelayCommand
        +RecordInCommand RelayCommand
    }
    class StockInsViewModel {
        +View ListCollectionView
        +ShowCurrentCommand RelayCommand
        +RecordInCommand RelayCommand
    }
    class DebtsViewModel {
        +View ListCollectionView
        +OpenCommand RelayCommand
    }
    class CustomerDetailViewModel {
        +Rows BulkObservableCollection~LedgerRow~
        +BackCommand RelayCommand
        +EditCommand RelayCommand
        +RecordRepaymentCommand RelayCommand
        +ReverseCommand RelayCommand
    }
    class ReportsViewModel {
        +Best BulkObservableCollection~BestRow~
        +Slow BulkObservableCollection~BestRow~
        +DaySales BulkObservableCollection~DaySaleRow~
        +WeekBars BulkObservableCollection~DayBar~
        +PrevCommand RelayCommand
        +NextCommand RelayCommand
        +ExportExcelCommand RelayCommand
        +ExportPdfCommand RelayCommand
        +SortBest(byQty)
        +Refresh()
        +BuildDocument() ReportDocument
    }
    class BackupViewModel {
        +Copies BulkObservableCollection~BackupCopyRow~
        +ChooseFolderCommand RelayCommand
        +BackupNowCommand RelayCommand
        +RestoreCommand RelayCommand
        +RestoreFromFileCommand RelayCommand
        +AutoBackupFinished(error)
    }

    ObservableObject <|-- MainViewModel
    ObservableObject <|-- SalesViewModel
    ObservableObject <|-- CartLine
    ObservableObject <|-- PricesViewModel
    ObservableObject <|-- StockViewModel
    ObservableObject <|-- StockInsViewModel
    ObservableObject <|-- DebtsViewModel
    ObservableObject <|-- CustomerDetailViewModel
    ObservableObject <|-- ReportsViewModel
    ObservableObject <|-- BackupViewModel

    IPage <|.. SalesViewModel
    IPage <|.. PricesViewModel
    IPage <|.. StockViewModel
    IPage <|.. StockInsViewModel
    IPage <|.. DebtsViewModel
    IPage <|.. CustomerDetailViewModel
    IPage <|.. ReportsViewModel
    IPage <|.. BackupViewModel

    MainViewModel --> Section
    MainViewModel *-- SalesViewModel
    MainViewModel *-- PricesViewModel
    MainViewModel *-- StockViewModel
    MainViewModel *-- StockInsViewModel
    MainViewModel *-- DebtsViewModel
    MainViewModel *-- ReportsViewModel
    MainViewModel *-- BackupViewModel
    MainViewModel ..> CustomerDetailViewModel : OpenCustomer()
    SalesViewModel *-- CartLine
```

Row classes used by the grids (`ProductRow`, `StockRow`, `MovementRow`, `StockInRow`, `DebtRow`, `LedgerRow`, `DaySaleRow`, `BestRow`, `DayBar`, `BackupCopyRow`, `CustomerOption`, `SizeOption`, `FilterOption`) wrap a model and add display text and search keys. They are left out of the diagram.

## 5. Infrastructure and dialogs

```mermaid
classDiagram
    class RelayCommand {
        <<ICommand>>
        +RelayCommand(run, can)
        +CanExecute(parameter) bool
        +Execute(parameter)
    }
    class BulkObservableCollection~T~ {
        +ReplaceAll(items)
    }
    class DomainException {
        +DomainException(message)
    }
    class Fmt {
        <<static>>
        +Money(value)$ string
        +Ks(value)$ string
        +Qty(value)$ string
        +LineTotal(unitPrice, quantity)$ long
        +ToMilli(qty)$ long
        +FromMilli(milli)$ decimal
        +DbTime(t)$ string
        +ParseDbTime(s)$ DateTime
        +SearchKey(text)$ string
        +WeekStart(d)$ DateTime
    }
    class Loc {
        <<static>>
        +Culture$ CultureInfo
        +IsBurmese$ bool
        +Get(key)$ string
        +F(key, args)$ string
        +Date(value, patternKey)$ string
    }
    class Dialogs {
        <<static>>
        +Confirm(title, message, keepText, actionText)$ bool
        +AskReason(title, summary, ...)$ string?
        +Error(message)$
        +Info(title, message)$
        +EditCustomer(Customer?)$ long?
        +EditProduct(Product?)$ long?
        +ImportCsv()$ bool
        +PriceHistory(Product)$
        +RecordRepayment(CustomerBalance)$ bool
        +RecordStockIn()$ long?
        +AdjustStock(productId?)$ long?
    }

    Exception <|-- DomainException
    ObservableCollection~T~ <|-- BulkObservableCollection~T~
```

- **`DomainException`** is thrown by services when a rule is broken (for example "a credit sale needs a customer"). View models catch it and show the message with `Dialogs.Error`.
- **`Loc`** reads `Resources/Strings.resx` (English) and `Strings.my.resx` (Burmese). XAML uses it through the `{l:Tr Key}` markup extension (`TrExtension`).
- **`Fmt`** keeps all number and date formats in one place, with Latin digits in both languages.

## Main flow: saving a sale

```mermaid
sequenceDiagram
    actor User
    participant V as SalesView
    participant VM as SalesViewModel
    participant S as SaleService
    participant DB as Db / Session

    User->>V: Save sale
    V->>VM: SaveCommand
    VM->>S: Save(cart lines, tendered, customerId)
    S->>S: validate quantities, prices, customer
    S->>DB: Write() (begin transaction)
    S->>DB: SUM stock_movements per product (refuse if the cart takes more than is in stock)
    S->>DB: INSERT sales
    loop each cart line
        S->>DB: INSERT sale_items
        S->>DB: INSERT stock_movements (kind = sale, negative)
    end
    opt part unpaid
        S->>DB: INSERT debts
        S->>DB: DebtService.Reallocate(customer)
    end
    S->>DB: Commit()
    S-->>VM: sale id
    VM->>VM: clear cart, show "Sale #0042 saved: 25,000 Ks"
```
