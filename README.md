# Store POS

An offline point-of-sale app for a family farm supply store (pesticides, herbicides, fertilizer and seed). It replaces the paper ledger: it records sales, keeps track of customer debts and stock, and shows daily and weekly reports. It runs on one Windows laptop with no internet, and the whole interface is in Burmese or English.

Built with C# / .NET 10, WPF (MVVM) and SQLite.

## Features

- **Sales:** find products by name, company or size, build a cart, take cash, and save. If part of a sale is unpaid, it is recorded as a debt for a named customer. A sale can be voided with a reason, but never deleted.
- **Prices:** add and edit products, import a price list from CSV, and see the dated history of every price change.
- **Stock:** current stock for every product, deliveries ("stock in"), shelf-count adjustments with a reason, and a full movement history. Stock is the sum of its movements, so it can always be traced.
- **Debts:** each customer's balance and ledger. A repayment pays off the oldest debts first, and can be reversed with a reason.
- **Reports:** daily and weekly totals, cash received, credit given, best and slow sellers. Export to Excel (.xlsx) or PDF.
- **Backup:** an automatic copy once a day, "Back up now", and restore (a safety copy is taken first).
- **Language:** Burmese and English, switchable in the app. Numbers always use Latin digits.

## Requirements

- Windows 10 or 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build from source (the published app is self-contained and needs nothing installed)
- Optional: [Inno Setup 6](https://jrsoftware.org/isdl.php) to build the one-file installer

## Getting started

```powershell
# Run the app
dotnet run --project StorePOS

# Run with sample data, kept in a separate folder from the real shop data
dotnet run --project StorePOS -- --demo

# Run the tests
dotnet test StorePOS.Tests
```

### Command-line options

| Option | What it does |
|---|---|
| `--demo` | Uses a separate `Demo` data folder and fills it with sample data |
| `--data <folder>` | Uses another data folder instead of `%LOCALAPPDATA%\StorePOS` |
| `--screenshots <folder>` | Saves a screenshot of every screen and dialog, then exits (use with `--data <empty folder>`) |

## Building for the shop

```powershell
.\build-installer.ps1
```

The script:

1. runs the tests and stops if any fail;
2. publishes a self-contained `publish\StorePOS.exe`;
3. if Inno Setup 6 is installed, builds `installer-output\StorePOS-Setup-1.0.0.exe`.

The installer installs for the current Windows user only, so no administrator password is needed. Uninstalling never deletes the shop's data.

## Where the data lives

| What | Where |
|---|---|
| Database | `%LOCALAPPDATA%\StorePOS\store.db` (SQLite) |
| Settings (language, backup folder) | `%LOCALAPPDATA%\StorePOS\settings.json` |
| Backups | `Documents\StorePOS Backups` by default |

The database upgrades itself when the app opens an older file (schema version 4 today). Only one copy of the app can open the same data folder at a time.

## Importing a price list (CSV)

A UTF-8 file with one product per row. A header row is skipped.

| Column | Field | Required |
|---|---|---|
| 1 | Name | yes |
| 2 | Category: `pesticide`, `herbicide`, `fertilizer`, `seed` or `other`, or the name shown in the app's current language | yes |
| 3 | Unit, for example `bottle` or `bag` | yes |
| 4 | Price in kyat (whole number) | yes |
| 5 | Sold by weight: `yes` / `no` (empty means no) | no |
| 6 | Company | no |
| 7 | Size, for example `1 L` | no |

A product is matched by name + company + size. The import shows a preview of new products, price changes and errors before anything is saved. Each price change is recorded in the price history with the file name.

## Project layout

```
StorePOS/                 the WPF app
  Models/                 plain data classes (Product, SaleRecord, StockMovement, ...)
  Data/Db.cs              SQLite connection, schema and upgrades
  Services/               business rules: sales, debts, stock, reports, CSV import, backup, export
  ViewModels/             screen state and commands (MVVM)
  Views/  Dialogs/        XAML screens and dialogs
  Controls/               reusable controls (date box, product picker, shop header)
  Infrastructure/         formatting, translations, MVVM helpers
  Resources/              Strings.resx (English) and Strings.my.resx (Burmese)
  Themes/Theme.xaml       colours, fonts and sizes
  PRD.md  design.md       requirements and screen design
StorePOS.Tests/           xUnit tests, including speed tests
installer/StorePOS.iss    Inno Setup script
docs/                     ERD and class diagrams
```

## Design documents

- [Product requirements](StorePOS/PRD.md)
- [Screen design](StorePOS/design.md)
- [Entity relationship diagram](docs/ERD.md)
- [Low-level design: UML class diagrams](docs/LLD-class-diagram.md)
- [Status and what is left](TODO.md)

## Design rules worth knowing

- **Money** is stored in whole kyat as integers. **Quantities** are stored in thousandths, so products sold by weight can have decimals.
- **Every write that touches more than one row is one transaction.** A sale, its items, its stock movements and its debt are saved together or not at all.
- **Nothing is deleted.** Sales are voided and repayments are reversed, each with a reason, so the history is always complete.
- **Sales copy the product name and price**, so editing a product later never changes a past sale.
- **Stock can go below zero.** The shelf may be right and the app wrong, so a sale is never blocked; the product shows OUT until the count is fixed.

## Tests

`dotnet test StorePOS.Tests` covers sales and debts, reports, CSV import, backup and restore, Excel and PDF export, products, price history, stock, and speed (searching 5,000 products and loading a year of stock movements).
