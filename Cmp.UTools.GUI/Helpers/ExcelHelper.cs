using Aspose.Cells;
using Cmp.UTools.GUI.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Cmp.UTools.GUI.Helpers
{
    /// <summary>
    /// 基于 Aspose.Cells 的 Excel / CSV / JSON 与 TabularData 相互转换工具类。
    /// 说明：使用前请在项目中通过 NuGet 安装 Aspose.Cells 并配置许可（如果有）。
    /// </summary>
    public static class ExcelHelper
    {
        public static TabularData LoadFromExcel(string filePath, int sheetIndex = 0)
        {
            var wb = new Workbook(filePath);
            return FromWorkbook(wb, sheetIndex);
        }

        public static TabularData FromWorkbook(Workbook wb, int sheetIndex = 0)
        {
            var td = new TabularData();
            var ws = wb.Worksheets[sheetIndex];
            var cells = ws.Cells;
            var maxRow = cells.MaxDataRow; // 0-based index of last row with data
            var maxCol = cells.MaxDataColumn; // 0-based index

            if (maxRow < 0 || maxCol < 0)
                return td;

            // read header (first row)
            var headers = new List<string>();
            for (int c = 0; c <= maxCol; c++)
            {
                var h = cells[0, c].StringValue ?? string.Empty;
                if (string.IsNullOrWhiteSpace(h)) h = "Column" + c;
                headers.Add(h);
                td.EnsureColumn(h);
            }

            // read rows
            for (int r = 1; r <= maxRow; r++)
            {
                // if entire row empty, skip
                bool empty = true;
                var row = new Dictionary<string, string>();
                for (int c = 0; c < headers.Count; c++)
                {
                    var val = cells[r, c]?.StringValue ?? string.Empty;
                    if (!string.IsNullOrEmpty(val)) empty = false;
                    row[headers[c]] = val ?? string.Empty;
                }
                if (!empty) td.AddRow(row);
            }

            return td;
        }

        public static void SaveAsExcel(TabularData data, string filePath, string sheetName = "Sheet1")
        {
            var wb = ToWorkbook(data, sheetName);
            wb.Save(filePath, SaveFormat.Xlsx);
        }

        public static Workbook ToWorkbook(TabularData data, string sheetName = "Sheet1")
        {
            var wb = new Workbook();
            var ws = wb.Worksheets[0];
            ws.Name = sheetName;
            var cells = ws.Cells;

            // header
            for (int c = 0; c < data.Columns.Count; c++)
            {
                cells[0, c].PutValue(data.Columns[c]);
            }

            // rows
            for (int r = 0; r < data.Rows.Count; r++)
            {
                var row = data.Rows[r];
                for (int c = 0; c < data.Columns.Count; c++)
                {
                    var key = data.Columns[c];
                    row.TryGetValue(key, out var v);
                    cells[r + 1, c].PutValue(v ?? string.Empty);
                }
            }

            return wb;
        }

        public static void SaveAsCsv(TabularData data, string filePath, char delimiter = ',')
        {
            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            });

            foreach (var c in data.Columns) csv.WriteField(c);
            csv.NextRecord();

            foreach (var r in data.Rows)
            {
                foreach (var c in data.Columns)
                {
                    csv.WriteField(r.TryGetValue(c, out var v) ? v : string.Empty);
                }
                csv.NextRecord();
            }
        }

        public static TabularData LoadFromCsv(string filePath)
        {
            var td = new TabularData();
            using var reader = new StreamReader(filePath, Encoding.UTF8);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                IgnoreBlankLines = true
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
        }

        public static void SaveAsJson(TabularData data, string filePath)
        {
            var arr = new JArray();
            foreach (var r in data.Rows)
            {
                var obj = new JObject();
                foreach (var c in data.Columns)
                {
                    obj[c] = r.TryGetValue(c, out var v) ? v : string.Empty;
                }
                arr.Add(obj);
            }
            File.WriteAllText(filePath, arr.ToString());
        }

        public static TabularData LoadFromJson(string filePath)
        {
            var td = new TabularData();
            var txt = File.ReadAllText(filePath);
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
                        row[k] = it[k]?.ToString() ?? string.Empty;
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
        }
    }
}
