# Store POS: status and what is left

Last updated: 13 Oct 2026.

## Done
- **Screens:** all screens from design.md: Sales, Prices, Debts, Customer detail, and Reports (daily and weekly). There is also a Backup and language screen (the last sidebar item).
- **Dialogs:** Confirm, Void sale, Reverse repayment, Import CSV, Record repayment, Add/Edit product, New/Edit customer, Message.
- **Data:** SQLite with the 7 PRD tables. Money is stored in whole kyat and quantities in thousandths. Debts are paid off oldest first, and a sale with its debt is saved in one transaction.
- **Reports:** Excel export (.xlsx) and PDF export. Exports run in the background, so the window never freezes.
- **Backup:** an automatic copy once a day, "Back up now", and restore. Restore saves a safety copy of the current data first.
- **Screen text:** all of it is in `StorePOS/Resources/Strings.resx` (English) and `Strings.my.resx` (Burmese), with the same 294 keys.
- **Checked on screen in both languages** (rendered screenshots of every screen and dialog):
  - Digits stay Latin in Burmese.
  - Nothing is clipped.
  - Headers wrap.
  - Burmese text in the PDF is shaped correctly.
- **Tests:** `dotnet test StorePOS.Tests` runs 38 tests and all pass. They cover:
  - the PRD "done when" checks for debts and for weekly report totals;
  - CSV import, backup and restore, Excel and PDF export;
  - **speed:** with 5,000 products, a search takes 2 ms per keystroke on Sales and 8 ms on Prices (the limit is 500 ms).
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
   - "Walk-in" = လက်လီဝယ်သူ
   - "Reports" = စာရင်းချုပ်
   - "Reverse" = ပြန်ရုပ်
   - "Qty" = ပမာဏ (shortened so it fits the narrow report columns)
2. **Install Inno Setup 6 and run `.\build-installer.ps1`** to get the single installer file. The script is ready, but it has not been compiled because Inno Setup is not installed on this laptop.
3. **Test on the shop laptop** at 1366 × 768 and 100% scaling, using the real CSV price list (PRD Feature 2 "done when").
4. **Commit to git.** Nothing is committed yet. `.gitignore` is in place. The untracked `Untitled.md` and `Untitled (1).pdf` in the repo root need a decision: commit them or delete them.
5. **Milestone 5 trial:** run the app next to the ledger book for two weeks and compare the totals.
6. **Optional:**
   - Add an app icon.
   - The weekly report toolbar takes two lines in Burmese, because the week text is long. It works, but a shorter week format would keep it on one line.

## Deliberate differences from design.md
- A 5th sidebar item, **Backup**, which also holds the language switch.
- An **Edit customer** button on Customer detail, and a **Reverse…** button in repayment rows.
- The cart **Remove** column is 96 wide instead of 84, so the word is never clipped.
- Narrow number columns and the date boxes use the spec width as a minimum, so longer Burmese labels can grow them.

## Where things are
- `StorePOS/Services/`: business logic (sales, debts, reports, CSV import, backup, export).
- `StorePOS/ViewModels/`, `Views/`, `Dialogs/`, and `Themes/Theme.xaml` (the design tokens).
- `StorePOS.Tests/`: the tests. `installer/StorePOS.iss` and `build-installer.ps1`: the installer.
- Shop data: `%LOCALAPPDATA%\StorePOS\store.db`, with `settings.json` (language and backup folder) in the same folder. Backups go to `Documents\StorePOS Backups` by default.
