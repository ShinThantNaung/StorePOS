# Store POS: Product Requirements Document

Oct 13, 2026 · updated Oct 14, 2026 · @Shin Thant

An offline desktop POS for our family farm supply store that lets two people record sales, track customer debts and stock, and see daily and weekly results, with no internet needed.

**Changes on Oct 14:** added Feature 5 (Stock) and a dated price history (PR8 to PR9). Stock tracking moved from out of scope to in scope. Opening a bag to sell it loose stays out of scope.

## Problem and goals

Sales are written in a ledger book today. Entries are sometimes forgotten, and daily or weekly totals and best sellers are hard to work out by hand. Nobody knows exactly how much of each product is left without counting the shelf. This app replaces the ledger.

- Record a sale in under a minute, with the total calculated automatically.
- Record every unpaid balance against a named customer so no debt is lost.
- Show daily and weekly sales, earnings and best sellers without any manual adding.
- Show how much of each product is in stock, and when stock last came in.
- Keep a dated record of every price change.
- Run fully offline on one laptop.

**Success after one month:** no sale lives only on paper, both users can read the weekly report without help, and the stock numbers in the app match a shelf count.

## Users and constraints

The only users are my father and mother, who run the counter, the stock and the books themselves. There are no employees, so the app has no login, roles or permissions.

- **Hardware:** one laptop (1366 × 768). No receipt printer and no barcode scanner, so every item is found by name or category.
- **Internet:** not guaranteed, so nothing may depend on it. All data lives in a local SQLite file.
- **Platform:** a Windows desktop application built with C# .NET and WPF, using SQLite for storage.
- **Skills:** non-technical users, so screens must be simple and use large, clear text.
- **Product data:** product information comes on paper lists and images. I will type it into a CSV file, which the app imports.

**Design:** screens, sizes, colours and behaviour are in `DESIGN.md`, with mockups at https://claude.ai/artifact/Hso5oqgTZthF7Wf2QxJK6a.

## Scope

Version 1 covers the features the family asked for and nothing more.

**In scope**

- Transaction engine (build a sale, price it, save it)
- Price list with search and filters, CSV import, and a dated price history
- Customer debt records with repayments
- Daily and weekly sales reports with best and worst sellers
- Stock tracking: current stock per product, stock coming in, stock going out with each sale, and count corrections
- Local backup and restore of the database

**Out of scope for version 1**

- Logins, user roles or permissions
- Cloud sync or any feature that needs internet
- Barcode scanning and receipt printing
- Supplier purchasing (purchase orders, purchase cost, payments to suppliers) and profit reports
- Opening a bag to sell it loose (moving stock from a bag product to a loose product)
- Low-stock alerts and reorder levels
- Due dates or reminders on debts

## Feature 1: Transaction engine

A sale is a cart of items that is priced automatically and saved with the time it happened. This is the screen used most, so it must be the fastest.

1. **TX1:** Search or pick a product and add it to the cart with a quantity. Quantities are whole numbers. Products marked as sold by weight, such as some fertilizers, also accept decimals (for example 2.5 kg).
2. **TX2:** Each line shows unit price × quantity = line total, rounded to the nearest whole kyat. The cart shows the sale total, updated live.
3. **TX3:** A line's price can be changed for that sale only (for example a negotiated price). The product's list price does not change, and this is not recorded as a price change (PR8).
4. **TX4:** Enter the amount paid. The app shows the change to give back, or the unpaid balance if less than the total was paid.
5. **TX5:** If a balance remains, the app requires a customer and creates a debt record (Feature 3) in the same step.
6. **TX6:** Saving stores the date, time, items, prices, total and amount paid, and takes each item's quantity out of stock (Feature 5). Prices are copied into the sale, so later price changes never alter past sales.
7. **TX7:** A saved sale cannot be edited. A mistake is fixed by voiding the sale with a short reason and entering a new one. Voiding puts the items back into stock. Voided sales stay visible but are left out of reports.

**Done when:** a sale with three items and a part payment can be saved in under a minute, appears in the day's report, and lowers the stock of all three products.

## Feature 2: Price display

One screen lists every product with its current price, so either parent can answer a customer's question in seconds. The same product list feeds the sale screen.

1. **PR1:** Show all products in a table with name, category, unit or pack size, price, and the date the price last changed.
2. **PR2:** A search box matches any part of the name as the user types, ignoring capital letters.
3. **PR3:** Filter by category (pesticide, herbicide, fertilizer, seed, other). Filters and search can be combined.
4. **PR4:** Sort by name, category, price or price-changed date.
5. **PR5:** Add, edit and deactivate a product. A deactivated product disappears from the sale screen but stays in past sales, reports and stock history.
6. **PR6:** Import products from a CSV file that I prepare from the paper price lists and images. The file has the columns name, category, unit, price and sold by weight (yes or no), and is saved as UTF-8 so Burmese names import correctly. The app shows a preview, then adds new products and updates the price of existing ones. Rows it cannot read are listed, not skipped silently.
7. **PR7:** Product names may be in Burmese or English. Both are stored and searched correctly.
8. **PR8:** Every price change is recorded with its date and time, the old price, the new price, and where it came from: the product was added, the price was edited, or a CSV import (with the file name). Price history is never edited or deleted.
9. **PR9:** A Price history window shows one product's current price, the date it took effect, and every earlier price, newest first.

**Done when:** the full CSV price list is imported, a product can be found by typing part of its name, and after a second import with new prices each changed product shows both prices with their dates in its price history.

## Feature 3: Debt records

When a customer pays only part of a sale, the app keeps a record of who owes what, what they bought, and every repayment until the balance reaches zero.

1. **DB1:** Each debtor has a customer record with name and address. Phone number is optional. An existing customer is picked from a list instead of being typed again.
2. **DB2:** A debt is created automatically from a sale with an unpaid balance (TX5). It links to that sale, so the items bought and their prices are always visible.
3. **DB3:** Record a repayment against a customer with the date and amount. The balance drops by that amount, and one repayment can be spread across several of that customer's open debts, oldest first.
4. **DB4:** The debt list shows each customer's name, address and total still owed. A summary at the top shows the total owed to the store.
5. **DB5:** Search by name or address. Filter between customers who still owe and customers who have paid in full.
6. **DB6:** A customer's page shows every sale on credit and every repayment, newest first, and the running balance.
7. **DB7:** Repayments are never deleted. A wrong repayment is reversed with a reason, like a voided sale.

**Done when:** a part-paid sale creates a debt, two later repayments clear it, and the customer's history shows all three events.

## Feature 4: Daily and weekly reports

Every saved sale and repayment is logged automatically, and the report screen turns that log into totals and rankings with no manual adding.

1. **RP1:** A daily report for any chosen date and a weekly report for any chosen week. A week runs Monday to Sunday.
2. **RP2:** Each report shows the metrics in the table below.
3. **RP3:** Best sellers: products ranked by quantity sold and by sales amount, top 10.
4. **RP4:** Slow sellers: products with the lowest sales in the period, including products with no sales at all.
5. **RP5:** A bar chart of sales per day for the weekly report, and a list of the day's sales that opens each sale's items.
6. **RP6:** Voided sales are excluded from every figure.
7. **RP7:** A report can be exported to Excel or PDF so it can be kept or shown to others.

| Metric | Meaning |
| --- | --- |
| Total sold | Sum of all sale totals in the period, whether or not they were paid. This is what the family calls "earned". |
| Cash received | Payments taken at sale time plus debt repayments received in the period |
| Credit given | Unpaid balances created by sales in the period |
| Number of sales | Count of saved, non-voided sales |

**Done when:** the weekly report's total matches the sum of the seven daily reports.

## Feature 5: Stock

The app knows how much of each product is in the shop. Stock goes up when a delivery is recorded and goes down with every sale, and every change is kept with its date and time.

1. **ST1:** Stock is counted per product in that product's own unit (bottles, packs, bags, or kg for products sold by weight). Weight products accept decimals.
2. **ST2:** The Stock screen lists every product with its current stock and the date stock last came in. It can be searched by name, filtered by category, and filtered to show only products that are out of stock.
3. **ST3:** Record stock in: one delivery has a date and time, an optional supplier name, an optional note (for example the invoice number), and one or more products with the quantity received. Saving adds the quantities to stock.
4. **ST4:** A stock-in history lists every delivery, newest first, with its date and time, supplier and products. Opening a delivery shows each product's quantity added and its stock before and after.
5. **ST5:** Saving a sale takes its quantities out of stock, and voiding a sale puts them back (TX6, TX7). This happens in the same step as the sale or void.
6. **ST6:** Adjust count: when the shelf count does not match the app, the user enters the number counted and picks a reason (damaged or leaking, count was wrong, expired, other) with an optional note. The app records the difference.
7. **ST7:** Selecting a product shows its stock movements, newest first: date and time, what happened (sale, voided sale, stock in, adjustment), the change, and the stock left after it.
8. **ST8:** A sale is allowed even if the app shows too little stock, because the shelf may be right and the app wrong. The product is then marked as out of stock until stock is recorded or the count is corrected.
9. **ST9:** Stock movements are never edited or deleted. A mistake is fixed with an adjustment.
10. **ST10:** First-time setup: before go-live, each product's opening stock is entered once with Adjust count (reason "count was wrong").

**Done when:** after entering opening stock, recording one delivery, saving three sales and voiding one of them, each product's stock in the app matches a shelf count, and its stock history shows every one of those events with the right date and time.

## Data model

Eleven SQLite tables hold everything. Reports, debt balances, current prices and current stock are calculated from the logs, so there is no separate summary data to keep in sync.

| Table | Key fields | Purpose |
| --- | --- | --- |
| products | name, category, unit, price, sold by weight, active | The price list |
| price\_changes | product, date and time, old price, new price, source (added, edited, CSV import), file name | Every price change (PR8) |
| customers | name, address, phone (optional) | People who buy on credit |
| sales | date and time, total, amount paid, customer (optional), status, void reason | One row per sale |
| sale\_items | sale, product, quantity, unit price, line total | Items in each sale, with the price at that moment |
| debts | customer, sale, original amount | One row per sale with an unpaid balance |
| repayments | customer, amount, date, reversed flag and reason | Money received against debts |
| repayment\_allocations | repayment, debt, amount | Which debts each repayment cleared |
| stock\_ins | date and time, supplier (optional), note | One row per delivery |
| stock\_in\_items | stock in, product, quantity | Products in each delivery |
| stock\_movements | product, date and time, change (+ or −), kind (sale, void, stock in, adjustment), link to the sale, stock in or adjustment, reason, note | Every change to stock |

Money is stored as whole kyat amounts to avoid rounding errors. Quantities, including stock, are stored with decimals so a weight such as 2.5 kg is kept exactly. A debt's remaining balance is its original amount minus its allocations, never a typed-in number. A product's current stock is the sum of its stock movements, never a typed-in number. The product's price and its price\_changes row are written together.

## Non-functional requirements

- **Offline:** every feature works with no internet, and the app never tries to connect.
- **Data safety:** a sale, its stock movements and any debt it creates are saved completely or not at all, so a power cut mid-sale cannot leave half a record. The same applies to a void, a stock in, a count adjustment and a price change. This uses SQLite transactions.
- **History:** sales, repayments, price changes and stock movements are never deleted. Mistakes are fixed by a void, a reversal or an adjustment, each with a reason.
- **Backups:** the app copies the database to a chosen folder or USB drive once a day and on request, and can restore from any copy. One laptop failure must not erase the shop's records.
- **Speed:** search results appear within half a second with at least 5,000 products. Starting the app takes under 5 seconds. Current stock for all products loads within one second with a year of stock movements.
- **Ease of use:** large text, few buttons per screen, and a confirmation before any void, reversal or adjustment. No training should be needed beyond a short walkthrough.
- **Language:** the interface is in Burmese. If a screen cannot be shown properly in Burmese in version 1, that screen uses English. Burmese text must be Unicode (not Zawgyi) and must display and search correctly on the laptop. All screen text is kept in one place so it can be translated or switched later.
- **Install:** one installer file, with no database server or other software to set up.

## Milestones

Build in this order, so the shop can start using the app before every feature is finished.

1. **Products:** database, product list, search, filters, CSV import and price history.
2. **Sales:** the transaction engine with paying in full.
3. **Stock:** opening stock, stock in, stock taken out by sales and returned by voids, adjustments and the stock history.
4. **Debts:** part payments, customer records and repayments.
5. **Reports:** daily and weekly reports with best and slow sellers.
6. **Safety and trial:** backup and restore. Count every product and enter the opening stock, then run the app next to the ledger book for two weeks. Compare sales totals with the ledger and stock numbers with a shelf count before dropping the ledger.
