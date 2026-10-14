# Store POS: status and what is left

Last updated: 14 Oct 2026.

## Done
- **Screens:** all screens from design.md: Sales, Prices, Debts, Customer detail, and Reports (daily and weekly). There is also a Backup and language screen (the last sidebar item).
- **Dialogs:** Confirm, Void sale, Reverse repayment, Import CSV, Record repayment, Add/Edit product, New/Edit customer, Message, Record stock in, Adjust stock count, Price history.
- **Stock (PRD Feature 5):** a Stock sidebar item with two views, Current stock and Stock-in history, plus the Record stock in and Adjust stock count dialogs (design.md 7.7).
  - Saving a sale takes stock out and voiding puts it back, in the same transaction as the sale or void. A sale may take stock below zero; the product then shows OUT.
  - Stock is always the sum of `stock_movements`. Nothing in it is edited or deleted.
  - Opening an older database adds the stock tables automatically (schema version 3).
- **Price history (PR8, PR9):** every price is recorded with its date and time, old and new price, and where it came from (Add product, Edit product, or a CSV import with the file name), in the same transaction as the product. Prices has a "Price changed" column (sortable) and a "Price history" button that opens design.md 7.3b. A price changed on one cart line is not a price change. Opening an older database adds the table (schema version 4).
- **Shop name header** across the top of every screen: "Shin Thant - Pesticide and Herbicide Store" / "ရှင်းသန့် - စိုက်ပျိုးရေးစျေးဆိုင်".
- **Unit** in Add/Edit product is a dropdown: Bottle (ပုလင်း) or Bag (အိတ်). Older products with another typed unit keep showing it until edited.
- **Company and size:** every product has an optional company and size. The same name may exist for another company or size. Sales and Prices filter by company (a search box) and size (a dropdown), and CSV import reads them as optional columns 6 and 7. Opening an older database adds the two columns automatically (schema version 2).
- **Data:** SQLite with all 11 PRD tables. Money is stored in whole kyat and quantities in thousandths. Debts are paid off oldest first, and a sale with its debt is saved in one transaction.
- **Reports:** Excel export (.xlsx) and PDF export. Exports run in the background, so the window never freezes.
- **Backup:** an automatic copy once a day, "Back up now", and restore. Restore saves a safety copy of the current data first.
- **Screen text:** all of it is in `StorePOS/Resources/Strings.resx` (English) and `Strings.my.resx` (Burmese), with the same 399 keys.
- **Checked on screen in both languages** (rendered screenshots of every screen and dialog):
  - Digits stay Latin in Burmese.
  - Nothing is clipped.
  - Headers wrap.
  - Burmese text in the PDF is shaped correctly.
- **Tests:** `dotnet test StorePOS.Tests` runs 59 tests and all pass. They cover:
  - the PRD "done when" checks for debts and for weekly report totals;
  - CSV import, backup and restore, Excel and PDF export;
  - company and size: uniqueness, filters, CSV columns, and upgrading an older database;
  - price history: adding, editing, CSV import with its file name, an out-of-date import preview, and a price changed for one sale only;
  - stock: the PRD Feature 5 "done when" check (opening count, delivery, three sales, one void), whole-number rules, deliveries, adjustments, and selling below zero;
  - **speed:** with 5,000 products, a search takes 2 ms per keystroke on Sales and 8 ms on Prices (the limit is 500 ms). With a year of stock movements (100,000), the Stock screen loads in about 0.2 s (the limit is 1 s).
- **Published build checked:** the window opens in 1.1–1.8 s (the limit is 5 s), Burmese works, and the automatic backup runs once a day.

## Build and run
- **Try the app safely with sample data:** `StorePOS.exe --demo`. The sample data is kept in a separate folder.
- **Shop build:** run `.\build-installer.ps1`. It runs the tests, then publishes `publish\StorePOS.exe` (self-contained, so no .NET install is needed on the laptop).
  - With **Inno Setup 6** (free, https://jrsoftware.org/isdl.php) installed, the same command also builds `installer-output\StorePOS-Setup-1.0.0.exe`. That is the "one installer file" from the PRD.
  - It installs for the current Windows user only, so no administrator password is needed.
  - Uninstalling never deletes the shop's data.
- **Screenshots for checking the UI:** `StorePOS.exe --screenshots <dir> --data <empty dir>`.

## Still to do
1. **Have a native speaker review the Burmese translations.** Claude drafted all of `Strings.my.resx`. Check these choices in particular:
   - "Stock" = လက်ကျန်, "Stock in" = ပစ္စည်းဝင်, "From" (supplier) = ပေးသွင်းသူ, "OUT" = ကုန်, "App says" = အက်ပ်တွင်
   - "Price changed" = ဈေး ပြောင်းသည့်နေ့, "How it changed" = ပြောင်းလဲပုံ, "First price" = ပထမ ဈေးနှုန်း
   - "Walk-in" = လက်လီဝယ်သူ
   - "Reports" = စာရင်းချုပ်
   - "Reverse" = ပြန်ရုပ်
   - "Qty" = ပမာဏ (shortened so it fits the narrow report columns)
   - "Company" = ကုမ္ပဏီ and "Size" = အရွယ်အစား, plus the new placeholders and error messages for them
2. **Install Inno Setup 6 and run `.\build-installer.ps1`** to get the single installer file. The script is ready, but it has not been compiled because Inno Setup is not installed on this laptop.
3. **Test on the shop laptop** at 1366 × 768 and 100% scaling, using the real CSV price list (PRD Feature 2 "done when").
4. **Commit to git.** Stock, price history, the shop header and the unit dropdown are not committed yet. The untracked `Untitled.md` and `Untitled (1).pdf` in the repo root need a decision: commit them or delete them.
5. **Enter the opening stock:** count every product once with Adjust count (reason "Count was wrong"), PRD ST10. Until then every product shows OUT.
6. **Milestone 6 trial:** run the app next to the ledger book for two weeks and compare the totals.
7. **Optional:**
   - Add an app icon.
   - The weekly report toolbar takes two lines in Burmese, because the week text is long. It works, but a shorter week format would keep it on one line.

## Deliberate differences from design.md
- A 5th sidebar item, **Backup**, which also holds the language switch.
- An **Edit customer** button on Customer detail, and a **Reverse…** button in repayment rows.
- The cart **Remove** column is 96 wide instead of 84, so the word is never clipped.
- Narrow number columns and the date boxes use the spec width as a minimum, so longer Burmese labels can grow them. This includes Change and After in the stock movements table.
- **Stock table:** products that are not on sale are listed only while some are still in stock, with the NOT ON SALE tag.
- **Record stock in:** Enter adds the product to the list. "Save stock in" is deliberately not the default button, so Enter never saves a delivery by accident. A delivery dated an earlier day is stamped with that day and the current time of day.
- **Adjust count:** the reason starts on "Count was wrong" (the opening-count case). A count equal to the app's number cannot be saved, because there is no difference to record.
- **Prices table:** design.md 7.2 has no Company and Size columns, but the app keeps them, so the fixed columns are narrower than the spec to make room for "Price changed": Company 160, Size 120, Category 140, Unit 160, Price 120, Price changed 140 (grows for Burmese). Name keeps about 300 at 1366 wide.
- **Price history dialog** is 720 × 620 instead of 640 × 580. 40 of the height is the window frame, and the extra width lets the Burmese "How it changed" text fit in two lines.
- **Price history after upgrading:** products from before schema version 4 have no recorded price yet, so their "Price changed" is empty until the price first changes. No date is made up.
- **Upgrading:** sales saved before stock tracking are not counted against stock, and voiding one of them does not add stock back.

## Where things are
- `StorePOS/Services/`: business logic (sales, debts, reports, CSV import, backup, export).
- `StorePOS/ViewModels/`, `Views/`, `Dialogs/`, and `Themes/Theme.xaml` (the design tokens).
- `StorePOS.Tests/`: the tests. `installer/StorePOS.iss` and `build-installer.ps1`: the installer.
- Shop data: `%LOCALAPPDATA%\StorePOS\store.db`, with `settings.json` (language and backup folder) in the same folder. Backups go to `Documents\StorePOS Backups` by default.
