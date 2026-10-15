using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Cmp.UTools.GUI.Models
{
    public class TabularData
    {
        public List<string> Columns { get; } = new List<string>();
        public List<Dictionary<string, string>> Rows { get; } = new List<Dictionary<string, string>>();

        public void EnsureColumn(string name)
        {
            if (!Columns.Contains(name)) Columns.Add(name);
        }

        public void AddRow(Dictionary<string, string> row)
        {
            // ensure all keys exist in Columns
            foreach (var k in row.Keys)
                EnsureColumn(k);
            Rows.Add(row);
        }

        public DataTable ToDataTable()
        {
            var dt = new DataTable();
            foreach (var c in Columns) dt.Columns.Add(c, typeof(string));
            foreach (var r in Rows)
            {
                var dr = dt.NewRow();
                foreach (var c in Columns)
                {
                    r.TryGetValue(c, out var v);
                    dr[c] = v ?? string.Empty;
                }
                dt.Rows.Add(dr);
            }
            return dt;
        }
    }
}
