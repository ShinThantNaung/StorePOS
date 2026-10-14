# Store POS: UI design spec (WPF)

Offline desktop POS for a small family farm supply store in Myanmar (pesticides, herbicides, fertilizers, seeds). Two non-technical users, no login. C# / WPF / SQLite, Windows laptop at 1366 × 768. Interface language is Burmese; the labels below are English placeholders and must come from one resource file.

Visual mockups: https://claude.ai/artifact/Hso5oqgTZthF7Wf2QxJK6a (11 artboards, same account only).

All sizes are WPF device-independent units (1 unit = 1 px at 100% scaling). Screens are drawn at 1366 × 700, which is the inside of a maximized window after the title bar and taskbar.

## 1. Style rules

- Plain, classic Windows business software. White and light grey surfaces, dark grey text.
- ONE accent colour, used only for the primary button, the current sidebar item, the selected table row, and selected toggle buttons.
- No gradients, shadows, animations, rounded cards, decorative icons, illustrations or dark mode.
- 1 px grey borders. Corner radius 2 on buttons, inputs and tags. 0 on panels and tables.
- Tables have a grey header, 1 px row lines and alternating row colours.
- Smallest text is 16. Buttons and inputs are at least 44 tall.
- Voided and reversed items are shown by a "VOIDED" tag, strikethrough and secondary text colour, never by red. Errors and warnings use bold text, not new colours.

## 2. Colours

Define as `SolidColorBrush` resources in `Theme.xaml`.

| Key | Hex | Used for |
| --- | --- | --- |
| `Accent` | `#2E5E87` | Primary button, current sidebar item, selected toggle |
| `AccentHover` | `#244A6B` | Primary button hover and pressed |
| `AccentTint` | `#D5DFE7` | Selected table row, opened sale in a list |
| `Text` | `#1F1F1F` | All main text and line icons |
| `TextSecondary` | `#545454` | Labels, hints, notes, voided and reversed rows |
| `Grey500` | `#767676` | Input and button borders, placeholder text, disabled text, chart bars |
| `Grey400` | `#B3B3B3` | Panel and table outlines, header dividers |
| `Grey300` | `#D6D6D6` | Hover on buttons, sidebar items, header buttons |
| `Grey200` | `#E5E5E5` | Sidebar, status bar, table header, button face, row lines |
| `Grey100` | `#F0F0F0` | Window and dialog background |
| `Grey50` | `#F7F7F7` | Alternate table rows |
| `White` | `#FFFFFF` | Tables, inputs, panels |

Green alternative if preferred: `Accent #3D6B45`, `AccentHover #31573A`, `AccentTint #D8E1DA`.

Contrast: Text on White 16.5:1, TextSecondary on Grey100 6.6:1, White on Accent 6.8:1, Grey500 on White 4.5:1.

## 3. Type

Font: `FontFamily="Segoe UI, Myanmar Text"`. Both ship with Windows 10 and 11, and WPF falls back per character, so Latin text and digits use Segoe UI and Burmese uses Myanmar Text. Burmese text must be Unicode, not Zawgyi.

| Size | Weight | Used for |
| --- | --- | --- |
| 36 | Bold | Sale total on the Sales screen |
| 28 | Semibold | Report figures, total owed, customer balance |
| 24 | Semibold | Dialog titles, customer name, "Balance owed" / "Change" amount |
| 20 | Semibold | Section headings, "Total" label, amount paid box |
| 18 | Semibold | Sidebar items, large action buttons |
| 16 | Semibold | Buttons, table headers, emphasis |
| 16 | Regular | Everything else: body, table cells, inputs, labels, status bar |

Line height: English 1.5 × size. Burmese needs about 1.7 × size (27 at size 16). Every single-line control is therefore 44 tall. Set `LineStackingStrategy="BlockLineHeight"` with an explicit `LineHeight` on wrapped text.

Numbers: money and quantities are right-aligned with thousands separators (78,500). Table headers say "(Ks)". Totals say "Ks" after the number. Quantities of weight products show decimals (2.5 kg).

## 4. Spacing and sizes

Spacing scale: 4, 8, 12, 16, 24.

| Value | Used for |
| --- | --- |
| 4 | Field to its hint line |
| 8 | Between related controls: label and field, buttons in a row, heading and its table |
| 12 | Between blocks stacked in a column |
| 16 | Screen padding, gap between side-by-side panels, padding inside panels |
| 24 | Dialog padding, gap before a second filter group |
| 10 / 8 | Table cell padding: 10 left and right, 8 top and bottom when text wraps |

| Element | Size |
| --- | --- |
| Header | 48 tall, shop name inset 20 |
| Sidebar | 176 wide, items 56 tall, text inset 20 |
| Status bar | 32 tall, text inset 16, shows today's date |
| Button | 44 tall, min width 96, padding 0 20 |
| Large action button | 48 tall, min width 200 to 220 (Save sale, Record repayment), font 18 |
| Button inside a table row | 36 tall |
| Icon-only button | 44 × 44, with `AutomationProperties.Name` |
| Text box, dropdown, date box | 44 tall |
| Number box inside a table row | 36 tall |
| Table header | 40 tall |
| Table row minimum | 40 (lists and reports), 48 (debt list), 52 (cart). Rows grow when text wraps. |
| Borders | 1 everywhere |
| Keyboard focus | 2 outline in `Text` colour, 1 away from the control |
| Chart bars | 24 tall in 48 tall rows, `Grey500`, value printed after each bar |

## 5. Component rules

- **Primary button**: `Accent` fill, `Accent` border, white text. Only ONE per screen or dialog.
- **Default button**: `Grey200` fill, `Grey500` border, `Text`. Hover `Grey300`. Disabled: text `Grey500`, fill `#F0F0F0`, border `Grey400`.
- **Toggle group** (category filter, Show filter, Daily/Weekly): buttons side by side. The selected one uses the Accent fill and white text. Set the pressed state for screen readers.
- **Sortable column header**: a flat button with `▲` or `▼` after the label for the sorted column. Hover `Grey300`.
- **Table**: white, 1 px `Grey400` outline, header `Grey200`. Rows alternate `White` and `Grey50`. The selected row is `AccentTint`. Row bottom line `Grey200`.
- **Text box**: white, 1 px `Grey500`, radius 2, padding 0 10. Placeholder `Grey500`.
- **Voided tag**: text "VOIDED", 1 px `TextSecondary` border, radius 2, semibold, padding 0 6.
- **Dialogs**: modal, centred on the main window, `Grey100` background, 24 padding, buttons right-aligned with 8 gap. Primary action on the far right. Cancel to its left.

## 6. Shell (on every main screen)

```
+-----------------------------------------------------+
| Header 48: Shin Thant - Pesticide and Herbicide ... |
+---------+-------------------------------------------+
| Sidebar | Content (padding 16)                      |
| 176     |                                           |
| Sales   |                                           |
| Prices  |                                           |
| Stock   |                                           |
| Debts   |                                           |
| Reports |                                           |
+---------+-------------------------------------------+
| Status bar 32: Tuesday, 13 October 2026             |
+-----------------------------------------------------+
```

- Grid rows: header `48`, content `*`, status bar `32`. Grid columns: sidebar `176`, content `*`.
- Header (`Controls/ShopHeader`): the shop name, "Shin Thant - Pesticide and Herbicide Store" / "ရှင်းသန့် - စိုက်ပျိုးရေးစျေးဆိုင်", size 20 semibold, text inset 20 (in line with the sidebar text). `Grey200` background, 1 px `Grey400` bottom border. Spans both columns. No accent colour, and no ellipsis.
- Sidebar: `Grey200` background, 1 px `Grey400` right border, 8 top padding. Five items in this order: Sales, Prices, Stock, Debts, Reports. Each is 56 tall, size 18 semibold. The current item has the `Accent` fill and white text. Hover `Grey300`.
- Customer detail counts as part of Debts, and Stock-in history counts as part of Stock, so the parent item stays marked.

## 7. Screens

### 7.1 Sales (main screen)

Two panels side by side: Products (456 wide), gap 16, Cart (`*`).

**Products panel (left)**
- Heading "Products" (20 semibold).
- Row: label "Search" + text box (placeholder "Type part of the name"), filters the list as the user types.
- Product table: Name `*` | Unit `84` | Price (Ks) `100`. Clicking a row selects it (`AccentTint`).
- Under the table, a white 1 px panel with 12 padding: "Selected: <name>", then a row with label "Quantity", number box (88 wide, right-aligned, default 1), the unit text, and an "Add to cart" button on the right. Hint line "Whole numbers only for this product." For weight products the hint becomes "You can type decimals, for example 2.5" and the quantity box accepts decimals.
- Double-click on a product row also adds it with quantity 1.

**Cart panel (right)**
- Heading "Cart" with the item count on the right in `TextSecondary`.
- Cart table: Item `*` | Quantity `120` | Unit price (Ks) `128` | Amount (Ks) `112` | Remove `84`.
  - Quantity and Unit price are editable 36 tall boxes in the row. The quantity box is 60 wide followed by the unit text.
  - If the unit price differs from the list price, a second line under the item name shows "Changed, list price 8,500" in `TextSecondary`. The list price in the database does not change.
  - Amount = unit price × quantity, rounded to whole kyat.
- Total bar: 64 tall, white, 1 px border, 16 padding. "Total" (20 semibold) on the left, the sale total (36 bold, right-aligned) with "Ks".
- Payment grid (3 columns `132` | `200` | `*`, row gap 12):
  - Row 1: label "Amount paid" | amount box (168 wide, size 20 semibold) + "Ks" | result box.
  - The result box is a 44 tall white panel. When paid ≥ total it reads "Change to give" + amount. When paid < total it reads "Balance owed" + amount. Amount is 24 bold.
  - Row 2: label "Customer" | dropdown (fills) + "New customer" button | hint text below.
  - The customer is optional when paid in full (hint "Optional: the sale is paid in full.") and required when a balance is owed (hint in semibold: "Required: part of this sale is unpaid."). "Save sale" is blocked until a customer is chosen.
- Button row: "Clear cart" (default) on the left, "Save sale" (primary, 48 tall, min 220) on the right.
- "Clear cart" opens the Confirm dialog (7.6).

Saving a sale stores date, time, items, prices, total and amount paid in one SQLite transaction, then empties the cart. Prices are copied into the sale.

### 7.2 Prices

- Toolbar row 1: label "Search" + text box (360 wide, placeholder "Type part of the name"). On the right: "Add product", "Edit", "Price history" (opens 7.3b for the selected row), "Import CSV" (opens 7.3).
- Toolbar row 2: label "Category" + toggle buttons All, Pesticide, Herbicide, Fertilizer, Seed, Other. On the right, "Showing N of M products" in `TextSecondary`.
- Table: Name `*` | Category `180` | Unit `200` | Price (Ks) `140` | Price changed `150` (date of the last price change, "9 Oct 2026"). Name, Category, Price and Price changed headers are sortable buttons. Click again to reverse. Rows are selectable (`AccentTint`). Weight products show the unit as "kg (sold by weight)".
- Empty state: "No products match. Check the spelling, or choose All."
- Search is case-insensitive, matches any part of the name, and combines with the category filter.
- "Edit" acts on the selected row. Deactivating is done in the edit dialog (not designed yet).

### 7.3 Import CSV dialog (1240 × 640)

Title "Import products from CSV" (24 semibold). Three stacked blocks with 20 gap:

1. "1. Choose the file" (18 semibold): label "File", text box with the path, "Choose file…" button. Hint under: "Columns: name, category, unit, price, sold by weight (yes or no). Save the file as 'CSV UTF-8' so Burmese names come in correctly."
2. "2. Check the rows" (18 semibold), with a summary on the right: "52 rows read: 9 new products, 31 price changes, 9 with no change, 3 with errors". Below it two tables side by side (`*` | `352`):
   - Left, "Rows to import (49)": Row `52` | Name `*` | Category `108` | Unit `112` | Price (Ks) `96` | What changes `168`. The last column reads "New product", "Price 15,000 → 16,000" (semibold) or "No change" (regular).
   - Right, "Rows with errors (3)": Row `52` | What is wrong `*`. Plain-language messages, for example "Price '12,5OO' is not a number. A letter O was typed instead of a zero."
3. Footer: hint "Rows with errors are not imported. Fix them in the file and import it again." Then "Cancel" and the primary button "Import 49 rows" (min 180). The count is live.

Import adds new products and updates the price of existing ones (match on name). Rows that cannot be read are listed, never skipped silently. Every new product and every price that changes is written to the price history with the import date, time and file name.

### 7.3b Price history dialog (640 × 580)

Every price change is recorded with its date and time, and nothing in the history is ever edited or deleted.

- Title "Price history" (24 semibold), then the product name (20 semibold) and "Herbicide, sold by the bottle" in `TextSecondary`.
- White panel (10 × 16 padding): "Price now" with the price (28 semibold, "Ks"), and on the right "since 9 Oct 2026, 8:05 AM".
- Table, newest first: Date `176` ("9 Oct 2026, 8:05 AM") | Price (Ks) `104` (semibold) | Change `104` ("+1,000", "−500", or "First price") | How it changed `*` ("Changed in Edit product", "CSV import, prices-aug-2026.csv", "Added by CSV import, …").
- Note: "Past sales keep the price they were sold at. A price changed for one sale only is not listed here."
- One button, "Close" (min 120).

When a price changes:

| Source | Recorded as |
| --- | --- |
| Product added (Add product or CSV import) | First price |
| Price edited in the Edit product dialog | Changed in Edit product |
| Price updated by CSV import | CSV import, with the file name |
| Price changed on one cart line at the Sales screen | Not a price change. Stored only in that sale's line. |

Suggested table:

| Table | Key fields | Purpose |
| --- | --- | --- |
| price_changes | product, date and time, old price (empty for the first price), new price, source (added, edited, csv_import), file name | One row per price change. The product's current price and "Price changed" date come from its newest row. |

Write the price change row in the same SQLite transaction as the product update.

### 7.4 Debts

**List**
- Summary panel (white, 12 × 16 padding, 64 gap): "Total owed to the store" with the amount (28 semibold, "Ks"), and "Customers who still owe" with the count.
- Toolbar: label "Search" + text box (360, placeholder "Name or village"), label "Show" + toggle buttons "Still owes" / "Paid in full", count on the right.
- Table: Name `*` | Address `*` | Still owes (Ks) `200` (sorted descending, amount semibold) | Open `140`. The "Open" button in each row is 36 tall and goes to the customer detail.
- Empty state: "No customers match. Check the spelling."

**Customer detail**
- "Back to debt list" button at the top left.
- Header panel (white, 16 padding): name (24 semibold) and address (`TextSecondary`) on the left. On the right "Still owes" with the balance (28 semibold), then the large primary button "Record repayment" (48 tall, min 200).
- Section heading "History, newest first" with a count on the right.
- One ledger table: Date `140` | Details `*` | On credit (Ks) `150` | Repaid (Ks) `140` | Balance (Ks) `150`.
  - Credit sale rows: Details has a semibold line "Credit sale #0412: total 78,500, paid 50,000" and a second line with the items bought. "On credit" shows the unpaid part.
  - Repayment rows: Details "Repayment". "Repaid" shows the amount.
  - A reversed repayment is struck through, in `TextSecondary`, with the reason in Details.
  - Balance is the running balance after each row, semibold.
- The balance always comes from the database (original amounts minus allocations), never typed.

**Record repayment dialog (520 × 420)**
- Title "Record repayment" (24 semibold), customer name and village in `TextSecondary`.
- Form grid (`120` | `*`, row gap 12): "Still owes" (20 semibold), "Date" (text box 220 + calendar icon button), "Amount" (box 180, size 20 semibold, + "Ks"), "After this" (shows "N Ks still owed" or "Paid in full", or "Check the amount").
- If the amount exceeds the balance, show in semibold: "The amount is more than this customer owes."
- Note: "The money pays off the oldest sales first."
- Buttons bottom right: "Cancel", primary "Save repayment" (min 180).

### 7.5 Reports

- Toolbar: Daily / Weekly toggle (two joined buttons, min 110 each, the selected one in `Accent`). Then a picker: Daily shows label "Date" + date box (200) + previous/next day icon buttons. Weekly shows label "Week" + box (280, "Mon 5 Oct to Sun 11 Oct 2026") + previous/next week buttons. The next button is disabled when it would go into the future. On the right: "Save as Excel", "Save as PDF".
- Four figure panels in a row (4 × `*`, 16 gap, white, 10 × 16 padding): label, value (28 semibold), one-line note in `TextSecondary`.
  - Total sold: "All sales, paid or not"
  - Cash received: "Includes N Ks debt repayments"
  - Credit given: "Unpaid part of N sales"
  - Number of sales: "N voided sale(s) not counted"
- Lower area, three columns: `420` | `330` | `*`, gap 16.
  - **Best sellers (top 10)**: # `36` | Product `*` | Qty `84` | Ks `104`. Qty and Ks headers are sort buttons (default: by Ks). Weight products show "12.5 kg".
  - **Slow sellers**: Product `*` | Qty `64` | Ks `92`, lowest first, includes products with no sales. Footnote "Lowest first. Products with no sales are included."
  - **Daily view, third column "Sales on <date>"**: Time `88` | Customer `*` | Total `96` | expand button `44` (32 × 32, chevron). Rows show "Walk-in" or the customer name. A credit sale shows "Owes N" under the name. Expanding a row (`AccentTint`) lists each item with its amount, the payment line ("Paid N, owes N") and a "Void this sale" button (36 tall) that opens 7.6. A voided row shows the strikethrough total, the "VOIDED" tag and the reason, and has no void button.
  - **Weekly view, third column "Sales per day (Ks)"**: seven rows (Mon to Sun) in white. Each row has a label `92`, then a grey bar (24 tall, width proportional to the largest day, max 170) and the amount after it. One colour only.
- Voided sales are excluded from every figure.
- The weekly total must equal the sum of the seven daily reports.

### 7.6 Void sale and Confirm dialogs

**Void sale (560 × 460)**
- Title "Void this sale?" (24 semibold).
- White 1 px summary panel (grid `96` | `*`): Time, Customer, Items, Total.
- Paragraph: "The sale stays in the list, marked VOIDED, and is left out of all reports. This cannot be undone. If the sale was wrong, enter it again after voiding."
- Label "Reason (required)" (semibold) and a text box, full width (placeholder "For example: wrong quantity entered").
- Buttons bottom right: "Cancel", and primary "Void sale" (min 150). "Void sale" stays disabled until a reason is typed.

**Confirm (480 × 220)**: used for destructive actions such as Clear cart and reversing a repayment.
- Title as a question ("Clear the cart?"), one sentence of consequence ("All 4 items will be removed. This sale has not been saved, so nothing is recorded.").
- Buttons bottom right: the safe choice first and focused by default ("Keep items"), then the primary destructive action named for what it does ("Clear cart"). Never "OK" or "Yes".
- Same pattern for reversing a repayment (also asks for a reason) and deactivating a product.

### 7.7 Stock

Stock is counted per product in the product's own unit (bottles, bags, packs, kg). Every change is a row in a stock movement log, and the number in stock is always the sum of that log, never a typed-in figure.

**What changes stock**

| Event | Change | Recorded automatically? |
| --- | --- | --- |
| Sale saved | minus the quantity of each line | Yes, in the same transaction as the sale |
| Sale voided | plus the quantity back | Yes, when the void is saved |
| Stock in recorded | plus the quantity of each line | Yes, from the Record stock in dialog |
| Count adjusted | plus or minus the difference | Yes, from the Adjust stock count dialog |

A sale is still allowed when stock would go below zero (the shelf may be right and the app wrong). The product then shows "OUT" and a negative number until someone records a stock in or adjusts the count.

**Current stock view** (sidebar item Stock)
- Toolbar row 1: joined toggle "Current stock" (selected) / "Stock-in history" (min 150 each). On the right: "Adjust count" (default) and "Record stock in" (primary, 48 tall, min 200).
- Toolbar row 2: label "Search" + text box (300), label "Category" + dropdown (190: All, Pesticide, Herbicide, Fertilizer, Seed, Other), label "Show" + toggles "All" / "Out of stock", and on the right "Showing N of M, K out of stock".
- Below, two panels: stock table `*` | gap 16 | history panel `540`.
- Stock table: Product `*` | Unit `76` | In stock `120` (right, semibold) | Last stock in `128` (date). A product at zero or below shows an "OUT" tag (1 px `Text` border, radius 2, semibold) before the number. Clicking a row selects it (`AccentTint`) and fills the history panel.
- History panel:
  - White header panel (12 × 16 padding): product name (20 semibold), then two columns: "In stock now" with the number and unit (28 semibold), and "Last stock in" with the date and time (semibold) and "+36 from Shwe Pyi Trading".
  - Heading "Stock movements, newest first" (18 semibold).
  - Table: Date `150` ("13 Oct, 11:20 AM") | What happened `*` | Change `76` (+36 / −2) | After `76` (semibold). Examples of "What happened": "Sale #0412, U Aung Myint", "Sale #0395 voided, stock returned", "Count adjusted: 1 bottle leaking", "Stock in from Shwe Pyi Trading". Stock-in rows are semibold.
- Empty state: "No products match. Check the spelling, or choose All."

**Stock-in history view**
- Same toolbar row 1 with "Stock-in history" selected; only "Record stock in" on the right.
- Row 2: label "Search" + text box (360, placeholder "Product or supplier"), count on the right ("7 stock-ins").
- Table, newest first: Date `170` (date semibold, time on a second line in `TextSecondary`) | From `260` (supplier, with the note such as "Invoice SP-1189" on a second line) | Products received `*` ("5 products: Glyphosate herbicide 1 L × 36, …") | expand `56`.
- Expanding a row (`AccentTint`) shows a small table under the Products column: Product `*` | Added `96` (+36, semibold) | Before `96` | After `96`.

**Record stock in dialog (960 × 640)**
- Title "Record stock in" (24 semibold).
- Form grid (`112` | `300` | `112` | `*`, row gap 12): Date (date box + calendar button) and From (editable dropdown of earlier suppliers, plus "Not given") on one row; Note (optional, full width, placeholder "Optional, for example the invoice number") on the next.
- White add panel (12 padding): label "Product" + search box (fills), label "Quantity" + number box (100), "Add" button. Hint under: "Bags, bottles and packs: whole numbers. Products sold by kg: decimals allowed, for example 25.5."
- Lines table: Product `*` | Unit `84` | In stock now `120` | Adding `132` (editable 36 tall box, semibold) | After `100` (semibold, live) | Remove `92`.
- Footer: "N products. Stock goes up as soon as you save." on the left; "Cancel" and primary "Save stock in" (min 180) on the right.
- One stock in = one delivery = many lines. Saved in one SQLite transaction with the date and time chosen.

**Adjust stock count dialog (560 × 520)**
- Title "Adjust stock count" (24 semibold), subtitle "Use this when the shelf count does not match the app."
- Form grid (`150` | `*`, row gap 12): Product (dropdown with search), "App says" (20 semibold, number and unit), "Counted on shelf" (number box 120, size 20 semibold, + unit), "Difference" (semibold, live: "−2 bottles", "No change"), Reason (dropdown: Damaged or leaking, Count was wrong, Expired, Other), Note (optional).
- Hint: "The old number stays in the stock history. Nothing is deleted."
- Buttons: "Cancel", primary "Save count" (min 160).
- Also used for the first-time setup: count every product once and save each count.

**Suggested tables**

| Table | Key fields | Purpose |
| --- | --- | --- |
| stock_ins | date and time, supplier (text, optional), note | One row per delivery |
| stock_in_items | stock_in, product, quantity | Lines of each delivery |
| stock_movements | product, date and time, change (decimal, + or −), kind (sale, void, stock_in, adjustment), link to the sale, stock_in or adjustment, reason | Every change to stock. Current stock = SUM(change) per product. |

## 8. Navigation and behaviour summary

| From | Action | To |
| --- | --- | --- |
| Sidebar | Click item | That screen. The current item is marked. |
| Sales | Clear cart | Confirm dialog |
| Prices | Import CSV | Import CSV dialog |
| Prices | Price history (selected row) | Price history dialog |
| Debts list | Open (row) | Customer detail |
| Customer detail | Record repayment | Record repayment dialog |
| Customer detail | Back to debt list | Debts list |
| Reports | Daily / Weekly | Switch the report type |
| Reports (daily) | Expand a sale, then Void this sale | Void sale dialog |
| Stock | Current stock / Stock-in history | Switch the view |
| Stock | Record stock in | Record stock in dialog |
| Stock | Adjust count | Adjust stock count dialog |

## 9. Burmese checklist

- Use `lang="my"` / `xml:lang="my"` on Burmese text so WPF picks the right shaping.
- Labels and buttons use `MinWidth`, not fixed `Width`, so longer Burmese words can grow the control. Keep buttons single-line and let the layout move.
- Table text columns wrap (`TextWrapping="Wrap"`); number columns do not.
- Do not truncate with an ellipsis. Burmese stacked characters can be cut mid-syllable.
- Search must work with Burmese and English names. Store text as UTF-8 / Unicode (no Zawgyi).
- Keep all screen text in one resource file (`Strings.resx`) so it can be translated or switched.

## 10. Not designed yet

- Add / Edit product dialog (name, category, unit, price, sold by weight, active). Unit is a dropdown, never free text, with only "Bottle" (ပုလင်း) and "Bag" (အိတ်); a new product starts on Bottle. The unit is stored as the code `bottle` or `bag` and shown in the interface language everywhere (Sales, cart, Prices, Stock, CSV preview, reports). A product saved earlier with another typed unit keeps showing that unit; editing it asks for Bottle or Bag.
- New customer dialog (name, address, optional phone).
- Backup and restore screen (daily backup folder, "Back up now", "Restore from copy").
- Reverse repayment dialog (can reuse the Void sale dialog pattern).- Stock shown on the Sales screen (an "In stock" column in the product list, and a warning when a sale takes stock below zero).

## 11. Suggested WPF structure

- `Themes/Theme.xaml`: colour brushes, font sizes, and implicit styles for `Button`, `TextBox`, `ComboBox`, `DataGrid`, `RadioButton`/`ToggleButton`. Style keys: `PrimaryButton`, `ToggleFilterButton`, `SortHeaderButton`, `MoneyTextBox`.
- `Views/`: `SalesView`, `PricesView`, `StockView`, `StockInHistoryView`, `DebtsView`, `CustomerDetailView`, `ReportsView`. `Dialogs/`: `ImportCsvDialog`, `PriceHistoryDialog`, `RecordStockInDialog`, `AdjustStockDialog`, `RepaymentDialog`, `VoidSaleDialog`, `ConfirmDialog`.
- `MainWindow`: sidebar `ListBox` or radio buttons bound to the current view, content `ContentControl`, status bar `StatusBar` bound to today's date. Window `MinWidth=1280`, `MinHeight=700`, start maximized.
- Use `DataGrid` with `CanUserAddRows=False`, `HeadersVisibility=Column`, `GridLinesVisibility=Horizontal`, `AlternationCount=2`, `RowHeight=40`.
- MVVM with plain `INotifyPropertyChanged`. Money is `long` or `decimal` in whole kyat; quantity is `decimal`.
