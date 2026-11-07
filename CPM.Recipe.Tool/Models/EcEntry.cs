using System;
using System.Collections.Generic;
using System.Linq;
using CPM.Recipe.Tool.Services;
using Newtonsoft.Json.Linq;

namespace CPM.Recipe.Tool.Models
{
    /// <summary>EC Configuration 左侧的文件类型（按文件名前缀分组：Dry / Pen / Roller / Polish）。</summary>
    public sealed class EcCategory
    {
        public string Name { get; set; }
        public List<EcFileItem> Files { get; } = new List<EcFileItem>();

        public string Display => Name;
        public string CountText => "(" + Files.Count + ")";
    }

    /// <summary>某个文件类型下的一个 ChamberConfig 文件。</summary>
    public sealed class EcFileItem
    {
        public string FullPath { get; set; }
        public string FileName { get; set; }

        public string Display => FileName;
        public override string ToString() => FileName;
    }

    /// <summary>
    /// EC 数据行（只读快照，来源于 JSON 数组中的 JObject）。
    /// 写回时通过 <see cref="Source"/> 定位原对象，写盘后整表重新加载，避免索引错位。
    /// </summary>
    public sealed class EcEntry
    {
        public long ECID { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public string SetValue { get; set; }
        public string NewValue { get; set; }
        public string DefaultValue { get; set; }
        public string Min { get; set; }
        public string Max { get; set; }
        public string Class { get; set; }
        public int TemplateType { get; set; }
        public string OptionText { get; set; }
        public string Discription { get; set; }
        public string PLCLink { get; set; }
        public string SheetName { get; set; }
        public int? Words { get; set; }
        public string TemplateKey { get; set; }

        /// <summary>原始 JSON 对象（属性顺序保持不变）。</summary>
        public JObject Source { get; set; }

        /// <summary>在文件根数组中的索引（删除/替换时使用）。</summary>
        public int Index { get; set; }

        public string[] Options { get; set; } = new string[0];

        public static EcEntry FromJson(JObject obj, int index)
        {
            return new EcEntry
            {
                ECID = ReadLong(obj, "ECID"),
                Name = ReadString(obj, "Name"),
                Unit = ReadString(obj, "Unit"),
                SetValue = ReadString(obj, "SetValue"),
                NewValue = ReadString(obj, "NewValue"),
                DefaultValue = ReadString(obj, "DefaultValue"),
                Min = ReadString(obj, "Min"),
                Max = ReadString(obj, "Max"),
                Class = ReadString(obj, "Class"),
                TemplateType = (int)ReadLong(obj, "TemplateType"),
                OptionText = EcConfigService.BuildOptionText(obj["Option"]),
                Discription = ReadString(obj, "Discription"),
                PLCLink = ReadString(obj, "PLCLink"),
                SheetName = ReadString(obj, "SheetName"),
                Words = obj["Words"] == null || obj["Words"].Type == JTokenType.Null
                    ? (int?)null : (int)ReadLong(obj, "Words"),
                TemplateKey = ReadString(obj, "TemplateKey"),
                Source = obj,
                Index = index,
                Options = EcConfigService.ParseOptionToken(obj["Option"])
            };
        }

        private static string ReadString(JObject obj, string name)
        {
            var t = obj[name];
            return t == null || t.Type == JTokenType.Null ? "" : t.ToString();
        }

        private static long ReadLong(JObject obj, string name)
        {
            var t = obj[name];
            if (t == null || t.Type == JTokenType.Null) return 0;
            long v;
            return long.TryParse(t.ToString(), out v) ? v : 0;
        }
    }

    /// <summary>编辑弹框回传的字段值；批量模式下只应用 <see cref="AppliedFields"/> 中勾选的字段。</summary>
    public sealed class EcFieldValues
    {
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";
        public string SetValue { get; set; } = "";
        public string NewValue { get; set; } = "";
        public string DefaultValue { get; set; } = "";
        public string Min { get; set; } = "";
        public string Max { get; set; } = "";
        public string Class { get; set; } = "";
        public int TemplateType { get; set; } = 2;
        public string Discription { get; set; } = "";
        public string PLCLink { get; set; } = "";
        public string[] Options { get; set; } = new string[0];

        /// <summary>null = 全部应用（单行编辑/新增）；否则只应用集合中的字段名。</summary>
        public HashSet<string> AppliedFields { get; set; }
    }

    /// <summary>一个 ChamberConfig 文件的加载结果。</summary>
    public sealed class EcFileData
    {
        public string FilePath { get; set; }
        public JArray Array { get; set; }
        public JsonFileFormat Format { get; set; }
        public List<EcEntry> Entries { get; } = new List<EcEntry>();
        public List<string> Sheets { get; } = new List<string>();
    }
}
