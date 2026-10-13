using System.IO.Compression;
using System.Security;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StorePOS.Infrastructure;

namespace StorePOS.Services;

public sealed record ReportFigure(string Label, string Value, string Note);

/// <summary>A table to export. Cells are string, long (kyat) or decimal (quantity).</summary>
public sealed class ReportTable
{
    public required string Title { get; init; }
    public required string[] Headers { get; init; }
    public required bool[] Numeric { get; init; }
    public List<object[]> Rows { get; } = [];
    public string? Footnote { get; init; }
}

public sealed class ReportDocument
{
    public required string Title { get; init; }
    public required string Period { get; init; }
    public List<ReportFigure> Figures { get; } = [];
    public List<ReportTable> Tables { get; } = [];
}

/// <summary>RP7: Excel (.xlsx written directly, no extra library) and PDF (QuestPDF).</summary>
public static class ExportService
{
    private const string Font = "Segoe UI";
    private const string BurmeseFont = "Myanmar Text";

    static ExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // QuestPDF no longer reads system fonts by itself; register the two Windows fonts the
        // interface uses so Latin text and Burmese both come out correctly in the PDF.
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        foreach (var file in new[] { "segoeui.ttf", "seguisb.ttf", "segoeuib.ttf", "mmrtext.ttf", "mmrtextb.ttf" })
        {
            var path = Path.Combine(fonts, file);
            if (!File.Exists(path)) continue;
            using var stream = File.OpenRead(path);
            QuestPDF.Drawing.FontManager.RegisterFontFromStream(stream);
        }
        // Never refuse to make the report because a font is missing; use what is available.
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
    }

    public static void SavePdf(ReportDocument doc, string path)
    {
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Font, BurmeseFont).FontColor("#1F1F1F"));

            page.Header().Column(col =>
            {
                col.Item().Text(doc.Title).FontSize(18).SemiBold();
                col.Item().PaddingBottom(12).Text(doc.Period).FontColor("#545454");
            });

            page.Content().Column(col =>
            {
                col.Spacing(14);
                col.Item().Row(row =>
                {
                    row.Spacing(8);
                    foreach (var f in doc.Figures)
                    {
                        row.RelativeItem().Border(0.75f).BorderColor("#B3B3B3").Padding(8).Column(c =>
                        {
                            c.Item().Text(f.Label).FontColor("#545454");
                            c.Item().Text(f.Value).FontSize(15).SemiBold();
                            c.Item().Text(f.Note).FontSize(9).FontColor("#545454");
                        });
                    }
                });

                foreach (var table in doc.Tables)
                {
                    // Keep a heading together with at least the start of its table.
                    col.Item().EnsureSpace(110).Column(c =>
                    {
                        c.Item().PaddingBottom(4).Text(table.Title).FontSize(13).SemiBold();
                        c.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                for (var i = 0; i < table.Headers.Length; i++)
                                {
                                    if (table.Numeric[i]) cd.ConstantColumn(table.Headers[i] == "#" ? 32 : 90);
                                    else cd.RelativeColumn();
                                }
                            });
                            t.Header(h =>
                            {
                                for (var i = 0; i < table.Headers.Length; i++)
                                {
                                    var cell = h.Cell().Background("#E5E5E5").Border(0.5f).BorderColor("#B3B3B3")
                                        .PaddingVertical(4).PaddingHorizontal(6);
                                    (table.Numeric[i] ? cell.AlignRight() : cell.AlignLeft())
                                        .Text(table.Headers[i]).SemiBold();
                                }
                            });
                            var n = 0;
                            foreach (var r in table.Rows)
                            {
                                var bg = n++ % 2 == 0 ? "#FFFFFF" : "#F7F7F7";
                                for (var i = 0; i < r.Length; i++)
                                {
                                    var cell = t.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E5E5")
                                        .PaddingVertical(3).PaddingHorizontal(6);
                                    (table.Numeric[i] ? cell.AlignRight() : cell.AlignLeft()).Text(CellText(r[i]));
                                }
                            }
                        });
                        if (table.Footnote != null)
                            c.Item().PaddingTop(4).Text(table.Footnote).FontSize(9).FontColor("#545454");
                    });
                }
            });

            page.Footer().AlignRight().Text(t =>
            {
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        })).GeneratePdf(path);
    }

    private static string CellText(object? v) => v switch
    {
        long l => Fmt.Money(l),
        int i => Fmt.Money(i),
        decimal d => Fmt.Qty(d),
        null => "",
        _ => v.ToString() ?? "",
    };

    public static void SaveExcel(ReportDocument doc, string path)
    {
        var rows = new List<string>();
        var r = 0;

        string Row(params string[] cells) => $"<row r=\"{++r}\">{string.Concat(cells)}</row>";
        string Ref(int col) => $"{(char)('A' + col)}{r + 1}";
        string Str(int col, string text, int style = 0) =>
            $"<c r=\"{Ref(col)}\" t=\"inlineStr\"{(style > 0 ? $" s=\"{style}\"" : "")}><is><t xml:space=\"preserve\">{SecurityElement.Escape(text)}</t></is></c>";
        string Cell(int col, object? v) => v switch
        {
            long l => $"<c r=\"{Ref(col)}\" s=\"2\"><v>{l}</v></c>",
            int i => $"<c r=\"{Ref(col)}\" s=\"2\"><v>{i}</v></c>",
            decimal d => $"<c r=\"{Ref(col)}\"><v>{d.ToString(System.Globalization.CultureInfo.InvariantCulture)}</v></c>",
            _ => Str(col, v?.ToString() ?? ""),
        };

        rows.Add(Row(Str(0, doc.Title, 1)));
        rows.Add(Row(Str(0, doc.Period)));
        r++;
        foreach (var f in doc.Figures) rows.Add(Row(Str(0, f.Label, 1), Str(1, f.Value), Str(2, f.Note)));
        foreach (var table in doc.Tables)
        {
            r++;
            rows.Add(Row(Str(0, table.Title, 1)));
            rows.Add(Row(table.Headers.Select((h, i) => Str(i, h, 1)).ToArray()));
            foreach (var data in table.Rows) rows.Add(Row(data.Select((v, i) => Cell(i, v)).ToArray()));
            if (table.Footnote != null) rows.Add(Row(Str(0, table.Footnote)));
        }

        var sheet = $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
            <cols><col min="1" max="1" width="40" customWidth="1"/><col min="2" max="5" width="22" customWidth="1"/></cols>
            <sheetData>{string.Concat(rows)}</sheetData>
            </worksheet>
            """;

        var sheetName = SecurityElement.Escape(doc.Title.Length > 31 ? doc.Title[..31] : doc.Title)!
            .Replace("/", " ").Replace("\\", " ").Replace(":", " ").Replace("?", " ").Replace("*", " ")
            .Replace("[", "(").Replace("]", ")");

        var files = new Dictionary<string, string>
        {
            ["[Content_Types].xml"] = """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                <Default Extension="xml" ContentType="application/xml"/>
                <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                </Types>
                """,
            ["_rels/.rels"] = """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """,
            ["xl/workbook.xml"] = $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                <sheets><sheet name="{sheetName}" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """,
            ["xl/_rels/workbook.xml.rels"] = """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """,
            ["xl/styles.xml"] = """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                <fonts count="2"><font><sz val="11"/><name val="Segoe UI"/></font><font><b/><sz val="11"/><name val="Segoe UI"/></font></fonts>
                <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
                <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
                <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
                <cellXfs count="3">
                <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
                <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>
                <xf numFmtId="3" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
                </cellXfs>
                </styleSheet>
                """,
            ["xl/worksheets/sheet1.xml"] = sheet,
        };

        var tmp = path + ".part";
        using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in files)
            {
                using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
                var bytes = new UTF8Encoding(false).GetBytes(content.TrimStart());
                stream.Write(bytes);
            }
        }
        File.Move(tmp, path, overwrite: true);
    }
}
