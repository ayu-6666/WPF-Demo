using Cmp.UTools.GUI.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Cmp.UTools.GUI.Services
{
    public class FileConversionService
    {
        public async Task<TabularData> ImportAsync(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            if (ext == ".csv") return await ImportCsvAsync(path);
            if (ext == ".json") return await ImportJsonAsync(path);
            if (ext == ".xml") return await ImportXmlAsync(path);
            if (ext == ".config") return await ImportConfigAsync(path);
            if (ext == ".xlsx" || ext == ".xls") return await ImportExcelAsync(path);
            throw new NotSupportedException("不支持的文件类型");
        }

        private Task<TabularData> ImportCsvAsync(string path)
        {
            return Task.Run(() =>
            {
                var td = new TabularData();
                using var reader = new StreamReader(path, Encoding.UTF8);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    IgnoreBlankLines = true,
                    BadDataFound = null
                });
                csv.Read();
                csv.ReadHeader();
                var headers = csv.HeaderRecord;
                foreach (var h in headers) td.EnsureColumn(h);
                while (csv.Read())
                {
                    var row = new Dictionary<string, string>();
                    foreach (var h in headers)
                    {
                        row[h] = csv.GetField(h);
                    }
                    td.AddRow(row);
                }
                return td;
            });
        }

        private Task<TabularData> ImportJsonAsync(string path)
        {
            return Task.Run(() =>
            {
                var td = new TabularData();
                var txt = File.ReadAllText(path);
                var token = JToken.Parse(txt);
                if (token is JArray arr)
                {
                    var allKeys = new HashSet<string>();
                    foreach (var it in arr.OfType<JObject>())
                        foreach (var p in it.Properties()) allKeys.Add(p.Name);
                    foreach (var k in allKeys) td.EnsureColumn(k);
                    foreach (var it in arr.OfType<JObject>())
                    {
                        var row = new Dictionary<string, string>();
                        foreach (var k in allKeys)
                        {
                            var t = it[k];
                            row[k] = t?.ToString() ?? string.Empty;
                        }
                        td.AddRow(row);
                    }
                }
                else if (token is JObject obj)
                {
                    var row = new Dictionary<string, string>();
                    foreach (var p in obj.Properties())
                    {
                        td.EnsureColumn(p.Name);
                        row[p.Name] = p.Value?.ToString() ?? string.Empty;
                    }
                    td.AddRow(row);
                }
                else
                {
                    throw new NotSupportedException("JSON 根节点必须是数组或对象");
                }
                return td;
            });
        }

        private Task<TabularData> ImportXmlAsync(string path)
        {
            return Task.Run(() =>
            {
                var td = new TabularData();
                var doc = System.Xml.Linq.XDocument.Load(path);
                var root = doc.Root;
                if (root == null) return td;
                var groups = root.Elements().GroupBy(x => x.Name.LocalName).Where(g => g.Count() > 1).ToList();
                var records = groups.FirstOrDefault()?.ToList() ?? root.Elements().ToList();
                foreach (var rec in records)
                {
                    var row = new Dictionary<string, string>();
                    foreach (var el in rec.Elements())
                    {
                        td.EnsureColumn(el.Name.LocalName);
                        row[el.Name.LocalName] = el.Value;
                    }
                    foreach (var attr in rec.Attributes())
                    {
                        td.EnsureColumn(attr.Name.LocalName);
                        row[attr.Name.LocalName] = attr.Value;
                    }
                    td.AddRow(row);
                }
                return td;
            });
        }

        private Task<TabularData> ImportConfigAsync(string path)
        {
            return Task.Run(() =>
            {
                var td = new TabularData();
                var doc = System.Xml.Linq.XDocument.Load(path);
                var appSettings = doc.Root?.Element("appSettings");
                if (appSettings != null)
                {
                    td.EnsureColumn("Key");
                    td.EnsureColumn("Value");
                    foreach (var add in appSettings.Elements("add"))
                    {
                        var k = add.Attribute("key")?.Value ?? string.Empty;
                        var v = add.Attribute("value")?.Value ?? string.Empty;
                        td.AddRow(new Dictionary<string, string> { ["Key"] = k, ["Value"] = v });
                    }
                }
                return td;
            });
        }

        private Task<TabularData> ImportExcelAsync(string path)
        {
            return Task.Run(() =>
            {
                var td = new TabularData();
                using var doc = SpreadsheetDocument.Open(path, false);
                var workbookPart = doc.WorkbookPart;
                if (workbookPart == null) return td;
                var sheets = workbookPart.Workbook.Sheets.Elements<Sheet>().ToList();
                if (!sheets.Any()) return td;
                var sheet = sheets.First();
                var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value);
                var rows = worksheetPart.Worksheet.GetFirstChild<SheetData>().Elements<Row>().ToList();
                if (!rows.Any()) return td;
                // header
                var header = rows.First();
                var headers = new List<string>();
                var sst = workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault()?.SharedStringTable;
                foreach (var cell in header.Elements<Cell>())
                {
                    var val = GetCellText(cell, sst);
                    headers.Add(string.IsNullOrEmpty(val) ? "" : val);
                    td.EnsureColumn(val ?? string.Empty);
                }
                // data rows
                foreach (var dataRow in rows.Skip(1))
                {
                    var row = new Dictionary<string, string>();
                    var cellElems = dataRow.Elements<Cell>().ToList();
                    for (int i = 0; i < headers.Count; i++)
                    {
                        var cell = cellElems.ElementAtOrDefault(i);
                        var v = cell != null ? GetCellText(cell, sst) : string.Empty;
                        row[headers[i]] = v ?? string.Empty;
                    }
                    td.AddRow(row);
                }
                return td;
            });
        }

        private static string GetCellText(Cell cell, SharedStringTable sst)
        {
            if (cell == null) return string.Empty;
            if (cell.DataType != null && cell.DataType == CellValues.SharedString)
            {
                if (int.TryParse(cell.CellValue?.Text, out var idx) && sst != null)
                {
                    return sst.ElementAt(idx).InnerText;
                }
            }
            return cell.CellValue?.Text ?? cell.InnerText ?? string.Empty;
        }

        public Task ExportCsvAsync(TabularData data, string path)
        {
            return Task.Run(() =>
            {
                using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                foreach (var c in data.Columns) csv.WriteField(c);
                csv.NextRecord();
                foreach (var r in data.Rows)
                {
                    foreach (var c in data.Columns)
                        csv.WriteField(r.TryGetValue(c, out var v) ? v : string.Empty);
                    csv.NextRecord();
                }
            });
        }

        public Task ExportJsonAsync(TabularData data, string path)
        {
            return Task.Run(() =>
            {
                var arr = new JArray();
                foreach (var r in data.Rows)
                {
                    var obj = new JObject();
                    foreach (var c in data.Columns)
                        obj[c] = r.TryGetValue(c, out var v) ? v : string.Empty;
                    arr.Add(obj);
                }
                File.WriteAllText(path, arr.ToString());
            });
        }

        public Task ExportXmlAsync(TabularData data, string path, string recordName = "Record", string rootName = "Root")
        {
            return Task.Run(() =>
            {
                var doc = new System.Xml.Linq.XDocument();
                var root = new System.Xml.Linq.XElement(rootName);
                foreach (var r in data.Rows)
                {
                    var rec = new System.Xml.Linq.XElement(recordName);
                    foreach (var c in data.Columns)
                        rec.Add(new System.Xml.Linq.XElement(c, r.TryGetValue(c, out var v) ? v : string.Empty));
                    root.Add(rec);
                }
                doc.Add(root);
                doc.Save(path);
            });
        }

        public Task ExportExcelAsync(TabularData data, string path)
        {
            return Task.Run(() =>
            {
                // create simple XLSX using Open XML SDK
                using var mem = new MemoryStream();
                using (var spreadsheet = SpreadsheetDocument.Create(mem, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
                {
                    var workbookPart = spreadsheet.AddWorkbookPart();
                    workbookPart.Workbook = new Workbook();
                    var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    var sheetData = new SheetData();
                    worksheetPart.Worksheet = new Worksheet(sheetData);

                    // header
                    var headerRow = new Row();
                    foreach (var h in data.Columns)
                    {
                        var cell = new Cell() { DataType = CellValues.InlineString };
                        cell.AppendChild(new InlineString(new Text(h ?? string.Empty)));
                        headerRow.AppendChild(cell);
                    }
                    sheetData.AppendChild(headerRow);

                    // rows
                    foreach (var r in data.Rows)
                    {
                        var row = new Row();
                        foreach (var c in data.Columns)
                        {
                            var v = r.TryGetValue(c, out var vv) ? vv : string.Empty;
                            var cell = new Cell() { DataType = CellValues.InlineString };
                            cell.AppendChild(new InlineString(new Text(v ?? string.Empty)));
                            row.AppendChild(cell);
                        }
                        sheetData.AppendChild(row);
                    }

                    var sheets = spreadsheet.WorkbookPart.Workbook.AppendChild(new Sheets());
                    var sheet = new Sheet()
                    {
                        Id = spreadsheet.WorkbookPart.GetIdOfPart(worksheetPart),
                        SheetId = 1,
                        Name = "Sheet1"
                    };
                    sheets.Append(sheet);
                    workbookPart.Workbook.Save();
                }
                File.WriteAllBytes(path, mem.ToArray());
            });
        }
    }
}
