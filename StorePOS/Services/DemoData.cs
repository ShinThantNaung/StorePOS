using StorePOS.Models;

namespace StorePOS.Services;

/// <summary>
/// Sample shop data for trying the app (--demo) and for screenshots. Only ever written to a
/// separate demo data folder, never to the shop's real database.
/// </summary>
public static class DemoData
{
    public static void Seed(AppServices s)
    {
        (string Name, string Company, string Size, Category Cat, string Unit, long Price, bool Weight)[] products =
        [
            ("2,4-D herbicide", "Green Valley", "1 L", Category.Herbicide, "bottle", 12000, false),
            ("Butachlor herbicide", "Golden Harvest", "1 L", Category.Herbicide, "bottle", 13500, false),
            ("Chlorpyrifos insecticide", "Green Valley", "500 ml", Category.Pesticide, "bottle", 9000, false),
            ("Cypermethrin insecticide", "Shwe Lal", "250 ml", Category.Pesticide, "bottle", 6000, false),
            ("Glyphosate herbicide", "Green Valley", "1 L", Category.Herbicide, "bottle", 15000, false),
            ("Glyphosate herbicide", "Golden Harvest", "1 L", Category.Herbicide, "bottle", 14500, false),
            ("Glyphosate herbicide", "Green Valley", "500 ml", Category.Herbicide, "bottle", 8000, false),
            ("Green gram seed", "", "1 kg", Category.Seed, "pack", 5000, false),
            ("Imidacloprid insecticide", "Shwe Lal", "100 ml", Category.Pesticide, "bottle", 4500, false),
            ("Knapsack sprayer", "", "16 L", Category.Other, "piece", 45000, false),
            ("Mancozeb fungicide", "Golden Harvest", "1 kg", Category.Pesticide, "pack", 11000, false),
            ("NPK 15-15-15 fertilizer", "Golden Harvest", "50 kg", Category.Fertilizer, "bag", 125000, false),
            ("NPK 15-15-15 fertilizer (loose)", "Golden Harvest", "", Category.Fertilizer, "kg", 2600, true),
            ("Paraquat herbicide", "Shwe Lal", "500 ml", Category.Herbicide, "bottle", 8500, false),
            ("Potash fertilizer", "Golden Harvest", "50 kg", Category.Fertilizer, "bag", 110000, false),
            ("Rice seed", "", "1 kg", Category.Seed, "pack", 3500, false),
            ("Urea fertilizer", "Green Valley", "50 kg", Category.Fertilizer, "bag", 105000, false),
            ("Urea fertilizer (loose)", "", "", Category.Fertilizer, "kg", 2200, true),
            ("ယူရီးယား မြေဩဇာ", "ရွှေလယ်", "၅၀ ကီလို", Category.Fertilizer, "အိတ်", 104000, false),
            ("စပါးမျိုး", "", "၁ ကီလို", Category.Seed, "ထုပ်", 3600, false),
        ];
        var list = products.Select(p => new Product
        {
            Name = p.Name, Company = p.Company, Size = p.Size, Category = p.Cat, Unit = p.Unit, Price = p.Price,
            SoldByWeight = p.Weight, Active = true,
        }).ToList();

        // Price history, all before the first sale: four products started cheaper, a CSV price list
        // raised them, and Glyphosate was changed once more in Edit product.
        var today = s.Today;
        var current = today.AddDays(-60).AddHours(9);
        var catalog = new ProductService(s.Db, () => current);
        (string Name, string Company, string Size, long First, long FromCsv)[] rises =
        [
            ("Glyphosate herbicide", "Green Valley", "1 L", 13500, 14000),
            ("Paraquat herbicide", "Shwe Lal", "500 ml", 8000, 8500),
            ("Urea fertilizer", "Green Valley", "50 kg", 100000, 105000),
            ("NPK 15-15-15 fertilizer", "Golden Harvest", "50 kg", 120000, 125000),
        ];
        Product Exact(string name, string company, string size) =>
            list.First(p => p.Name == name && p.Company == company && p.Size == size);
        foreach (var p in list)
        {
            var final = p.Price;
            var rise = rises.FirstOrDefault(r => r.Name == p.Name && r.Company == p.Company && r.Size == p.Size);
            if (rise.Name != null) p.Price = rise.First;
            p.Id = catalog.Save(p);
            p.Price = final;
        }

        current = today.AddDays(-40).AddHours(8);
        var csv = string.Join(Environment.NewLine, rises.Select(r =>
        {
            var p = Exact(r.Name, r.Company, r.Size);
            return $"{r.Name},{Categories.Code(p.Category)},{p.Unit},{r.FromCsv},no,{r.Company},{r.Size}";
        }));
        var import = new CsvImportService(s.Db, catalog, () => current);
        var preview = import.Preview(System.Text.Encoding.UTF8.GetBytes(csv));
        preview.FileName = "prices-sep-2026.csv";
        import.Apply(preview);

        current = today.AddDays(-25).AddHours(8).AddMinutes(5);
        catalog.Save(Exact("Glyphosate herbicide", "Green Valley", "1 L"));

        (string Name, string Address)[] customers =
        [
            ("U Aung Myint", "Hlaing village"), ("Daw Khin Mya", "Kyauk Taing village"), ("Ko Htet Aung", "Kan Gyi village"),
            ("Ko Zaw Min", "Thayet Kone village"), ("U Myo Win", "Ywar Thit village"), ("Daw Thida", "Sin Gaung village"),
            ("U Tun Tun", "Nyaung Pin village"), ("Daw Aye Aye", "Kan Gyi village"),
        ];
        var customerIds = customers.Select(c => s.Debts.SaveCustomer(new Customer { Name = c.Name, Address = c.Address })).ToList();

        current = s.Clock();
        var sales = new SaleService(s.Db, () => current);
        var debts = new DebtService(s.Db, () => current);
        var stock = new StockService(s.Db, s.Products, () => current);
        var rng = new Random(7);
        long? toVoid = null;

        // Opening stock, counted the evening before the first sale. The sprayers run out.
        Product Find(string name) => list.First(p => p.Name.StartsWith(name, StringComparison.Ordinal));
        current = today.AddDays(-21).AddHours(17);
        foreach (var p in list)
            stock.Adjust(p.Id, p.SoldByWeight ? 150 : p.Name.StartsWith("Knapsack") ? 30 : 60, AdjustReason.CountWrong, "Opening count");

        // Deliveries, by how many days before today they came in.
        var deliveries = new Dictionary<int, (string Supplier, string Note, (string Name, decimal Qty)[] Lines)>
        {
            [15] = ("Shwe Pyi Trading", "Invoice SP-1162", [("2,4-D", 12), ("Butachlor", 18)]),
            [9] = ("Aye Mya Agro Supply", "Invoice AM-2198", [("Potash", 10), ("Mancozeb", 20), ("Urea fertilizer (loose)", 100)]),
            [5] = ("", "", [("Rice seed", 150)]),
            [4] = ("Shwe Pyi Trading", "Invoice SP-1189",
                [("Glyphosate herbicide", 36), ("Paraquat", 24), ("Cypermethrin", 36), ("Chlorpyrifos", 12), ("Imidacloprid", 24)]),
            [1] = ("Aye Mya Agro Supply", "Invoice AM-2231", [("Urea fertilizer", 20), ("NPK 15-15-15 fertilizer", 15)]),
        };

        for (var day = today.AddDays(-20); day <= today; day = day.AddDays(1))
        {
            var daysAgo = (today - day).Days;
            if (deliveries.TryGetValue(daysAgo, out var delivery))
            {
                current = day.AddHours(7).AddMinutes(45);
                stock.RecordStockIn(day, delivery.Supplier, delivery.Note,
                    delivery.Lines.Select(l => new StockInLineInput(Find(l.Name).Id, l.Qty)).ToList());
            }
            if (daysAgo == 3)
            {
                current = day.AddHours(7).AddMinutes(50);
                var paraquat = Find("Paraquat");
                var shelf = stock.OnHand(paraquat.Id);
                if (shelf >= 1) stock.Adjust(paraquat.Id, shelf - 1, AdjustReason.Damaged, "1 bottle leaking");
            }

            var count = rng.Next(4, 10);
            var minute = 8 * 60;
            for (var n = 0; n < count; n++)
            {
                minute += rng.Next(15, 60);
                current = day.AddMinutes(minute);
                if (day == today && current > s.Clock()) current = s.Clock().AddMinutes(-(count - n) * 7);

                // A sale never takes more than is in stock, so a product that runs out stays at zero.
                var lines = Enumerable.Range(0, rng.Next(1, 4))
                    .Select(_ => list[rng.Next(list.Count)]).DistinctBy(p => p.Id)
                    .Select(p => new SaleLineInput(p.Id, p.FullName, p.Unit, p.SoldByWeight,
                        Math.Min(stock.OnHand(p.Id), p.SoldByWeight ? rng.Next(1, 10) * 0.5m : rng.Next(1, 5)), p.Price, p.Price))
                    .Where(l => l.Quantity > 0)
                    .ToList();
                if (lines.Count == 0) continue;
                var total = lines.Sum(l => l.LineTotal);
                long? customer = null;
                long tendered;
                if (rng.NextDouble() < 0.25)
                {
                    customer = customerIds[rng.Next(customerIds.Count)];
                    tendered = total * rng.Next(0, 8) / 10 / 1000 * 1000;
                }
                else
                {
                    customer = rng.NextDouble() < 0.2 ? customerIds[rng.Next(customerIds.Count)] : null;
                    tendered = (total + 4999) / 5000 * 5000;
                }
                var id = sales.Save(lines, tendered, customer);
                if (day == today && n == 2) toVoid = id;
            }

            if ((today - day).Days % 4 == 1)
            {
                var who = customerIds[rng.Next(customerIds.Count)];
                var balance = s.Debts.Balance(who)?.Balance ?? 0;
                if (balance > 0)
                {
                    current = day.AddHours(17);
                    debts.RecordRepayment(who, Math.Min(balance, rng.Next(2, 6) * 10000), day);
                }
            }
        }

        if (toVoid != null)
        {
            current = s.Clock();
            try { sales.Void(toVoid.Value, "Wrong quantity entered"); }
            catch (Infrastructure.DomainException) { /* already repaid; leave it */ }
        }
    }
}
