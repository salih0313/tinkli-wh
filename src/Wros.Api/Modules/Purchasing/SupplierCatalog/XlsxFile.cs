using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Wros.Api.Shared.Errors;
namespace Wros.Api.Modules.Purchasing.SupplierCatalog;

// Bounded SpreadsheetML reader. No Excel process, formula evaluation, macros or external resource access.
public static class XlsxFile
{
    public const string InvalidCellPrefix = "[WROS invalid spreadsheet cell] ";
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public sealed record Sheet(string[] Headers, (int Number, string[] Cells)[] Rows);
    public static Sheet Read(Stream stream, SupplierImportOptions limits)
    {
        try
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
            if (zip.Entries.Count > 512 || zip.Entries.Sum(e => e.Length) > limits.MaxExpandedBytes ||
                zip.Entries.Any(e => e.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || e.FullName.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase)))
                throw Invalid("Workbook exceeds processing limits or contains macros/external links.");
            if (zip.Entries.GroupBy(e => e.FullName, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) throw Invalid("Duplicate workbook parts are unsupported.");
            XDocument Xml(string path)
            {
                var entry = zip.GetEntry(path) ?? throw Invalid("Required workbook part is missing.");
                using var input = entry.Open();
                using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = limits.MaxExpandedBytes });
                return XDocument.Load(reader);
            }
            foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
                if (Xml(entry.FullName).Descendants().Any(x => string.Equals((string?)x.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase)))
                    throw Invalid("Remove external links and hyperlinks before importing.");
            if (Xml("[Content_Types].xml").Descendants().Any(x => ((string?)x.Attribute("ContentType"))?.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) == true)) throw Invalid("Macro-enabled workbooks are unsupported.");
            var workbook = Xml("xl/workbook.xml");
            var first = workbook.Descendants(S + "sheet").FirstOrDefault() ?? throw Invalid("Workbook has no worksheet.");
            var relation = Xml("xl/_rels/workbook.xml.rels").Descendants().SingleOrDefault(x => (string?)x.Attribute("Id") == (string?)first.Attribute(R + "id")) ?? throw Invalid("Invalid worksheet relationship.");
            var target = (string?)relation.Attribute("Target") ?? "";
            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            if (!path.StartsWith("xl/worksheets/", StringComparison.Ordinal) || path.Contains("..") || path.Contains('\\')) throw Invalid("Unsupported worksheet path.");
            var shared = zip.GetEntry("xl/sharedStrings.xml") is null ? [] : Xml("xl/sharedStrings.xml").Descendants(S + "si").Select(x => string.Concat(x.Descendants(S + "t").Select(t => t.Value))).ToArray();
            var styles = zip.GetEntry("xl/styles.xml") is null ? null : Xml("xl/styles.xml");
            var percentFormats = styles?.Descendants(S + "numFmt").Where(x => ((string?)x.Attribute("formatCode"))?.Contains('%') == true).Select(x => (string?)x.Attribute("numFmtId")).ToHashSet() ?? [];
            percentFormats.UnionWith(["9", "10"]);
            var cellStyles = styles?.Root?.Element(S + "cellXfs")?.Elements(S + "xf").Select(x => percentFormats.Contains((string?)x.Attribute("numFmtId"))).ToArray() ?? [];
            var rows = new List<(int Number, string[] Cells)>();
            foreach (var row in Xml(path).Descendants(S + "sheetData").Elements(S + "row"))
            {
                if (rows.Count > limits.MaxRows) throw Invalid($"At most {limits.MaxRows} data rows are supported.");
                var values = new string[limits.MaxColumns]; Array.Fill(values, "");
                var used = new HashSet<int>(); var nextColumn = 0;
                foreach (var cell in row.Elements(S + "c"))
                {
                    var formula = cell.Element(S + "f") is not null;
                    var reference = (string?)cell.Attribute("r");
                    var column = reference is null ? nextColumn : Column(reference);
                    if (column >= limits.MaxColumns || column < 0 || !used.Add(column)) throw Invalid("Too many columns or invalid cell references.");
                    nextColumn = column + 1;
                    var type = (string?)cell.Attribute("t");
                    var value = type == "inlineStr" ? string.Concat(cell.Descendants(S + "t").Select(x => x.Value)) : cell.Element(S + "v")?.Value ?? "";
                    if (type == "s") value = int.TryParse(value, out var index) && index >= 0 && index < shared.Length ? shared[index] : throw Invalid("Invalid shared string reference.");
                    if (type == "b") value = value == "1" ? "TRUE" : "FALSE";
                    if (type == "e") value = InvalidCellPrefix + "Excel cell error. Correct the source value.";
                    if (formula) value = InvalidCellPrefix + "Formula cell. Paste calculated values before importing.";
                    // Safe extra boolean/date/formula columns can be ignored by mapping. Formula/cache values are never evaluated or consumed.
                    if (int.TryParse((string?)cell.Attribute("s"), out var style) && style >= 0 && style < cellStyles.Length && cellStyles[style] && decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage)) value = (percentage * 100).ToString(CultureInfo.InvariantCulture) + "%";
                    if (value.Length > 2000) throw Invalid("A spreadsheet cell exceeds 2,000 characters.");
                    values[column] = value.Trim();
                }
                if (values.All(string.IsNullOrWhiteSpace)) continue;
                rows.Add((int.TryParse((string?)row.Attribute("r"), out var n) ? n : rows.Count + 1, values));
            }
            if (rows.Count < 2) throw Invalid("Include a header row and at least one data row on the first worksheet.");
            if (rows.Any(x => x.Number < 1) || rows.Select(x => x.Number).Distinct().Count() != rows.Count) throw Invalid("Worksheet contains invalid or duplicate row numbers.");
            var width = rows.Max(x => Array.FindLastIndex(x.Cells, v => !string.IsNullOrEmpty(v))) + 1;
            var headers = rows[0].Cells.Take(width).Select((v, i) => string.IsNullOrEmpty(v) ? $"Column {i + 1}" : v).ToArray();
            return new(headers, rows.Skip(1).Select(x => (x.Number, x.Cells.Take(width).ToArray())).ToArray());
        }
        catch (Exception error) when (error is InvalidDataException or XmlException or FormatException or OverflowException or InvalidOperationException or ArgumentException)
        { throw Invalid("Invalid or unsupported .xlsx workbook. Use the template and paste values only."); }
    }
    private static int Column(string reference)
    {
        var number = 0;
        foreach (var c in reference.TakeWhile(char.IsLetter)) { if (c is < 'A' or > 'Z' || number > 16384) return -1; number = number * 26 + c - 'A' + 1; }
        return number - 1;
    }
    public static byte[] Template()
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Write(string name, string xml) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(xml); }
            Write("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Write("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Write("xl/workbook.xml", $"<workbook xmlns=\"{S}\" xmlns:r=\"{R}\"><sheets><sheet name=\"Supplier stock\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Write("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            var headers = new[] { "Supplier Product Code", "Product Name", "Brand", "Barcode", "RSP/MRP", "Supplier Purchase Price", "Discount Type", "Discount Value", "Supplier Available Quantity" };
            var sheet = new XDocument(new XElement(S + "worksheet", new XElement(S + "sheetData", new XElement(S + "row", new XAttribute("r", 1), headers.Select((h, i) => new XElement(S + "c", new XAttribute("r", $"{(char)('A' + i)}1"), new XAttribute("t", "inlineStr"), new XElement(S + "is", new XElement(S + "t", h))))))));
            Write("xl/worksheets/sheet1.xml", sheet.ToString());
        }
        return output.ToArray();
    }
    private static ApiProblemException Invalid(string message) => new(400, message);
}
