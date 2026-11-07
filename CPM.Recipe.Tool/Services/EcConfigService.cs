using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CPM.Recipe.Tool.Models;
using Newtonsoft.Json.Linq;

namespace CPM.Recipe.Tool.Services
{
    /// <summary>
    /// EC Configuration 数据服务：扫描 ChamberConfig、按 SheetName 分组、保持原格式写回（备份 + 原子写）。
    /// 只读取/写入 Dry、Pen、Roller、Polish 前缀的 *Config.json；Scheduler/Application 等其它文件不参与。
    /// </summary>
    public static class EcConfigService
    {
        public const string BackupFolderName = "_backup";

        /// <summary>左侧文件类型固定为四类（与 ChamberConfig 文件名前缀一致）。</summary>
        public static readonly string[] Categories = { "Dry", "Pen", "Roller", "Polish" };

        // ============ 扫描 / 分组 ============
        public static List<EcCategory> Scan(string root)
        {
            var categories = Categories
                .Select(c => new EcCategory { Name = c })
                .ToList();

            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return categories;

            foreach (var file in Directory.EnumerateFiles(root, "*Config.json", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                if (IsUnderBackup(file)) continue;

                var category = categories.FirstOrDefault(c =>
                    name.StartsWith(c.Name, StringComparison.OrdinalIgnoreCase)
                    && name.Length > c.Name.Length
                    && IsLetterOrSeparator(name[c.Name.Length]));
                if (category == null) continue;

                category.Files.Add(new EcFileItem { FullPath = file, FileName = name });
            }

            foreach (var c in categories)
            {
                var sorted = c.Files.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase).ToList();
                c.Files.Clear();
                c.Files.AddRange(sorted);
            }

            return categories;
        }

        /// <summary>DryA1Config → 下一个字符必须是字母/数字/分隔符，避免把 DryXxx 当作匹配；实际用前缀即可。</summary>
        private static bool IsLetterOrSeparator(char ch)
            => char.IsLetter(ch) || char.IsDigit(ch) || ch == '_' || ch == ' ' || ch == '-';

        private static bool IsUnderBackup(string file)
        {
            var sep = Path.DirectorySeparatorChar;
            return file.IndexOf(sep + BackupFolderName + sep, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ============ 读取 ============
        public static EcFileData Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("文件不存在", filePath);

            var doc = JsonDocumentService.Load(filePath);
            var array = doc.Root as JArray;
            if (array == null)
                array = JsonDocumentService.GetPrimaryArray(doc.Root, out _);
            if (array == null)
                throw new InvalidDataException("根节点不是 JSON 数组，无法作为 EC 配置读取");

            var data = new EcFileData
            {
                FilePath = filePath,
                Array = array,
                Format = doc.Format
            };

            for (var i = 0; i < array.Count; i++)
            {
                if (!(array[i] is JObject obj)) continue;
                var entry = EcEntry.FromJson(obj, i);
                data.Entries.Add(entry);

                var sheet = entry.SheetName ?? "";
                if (sheet.Length > 0 && !data.Sheets.Contains(sheet))
                    data.Sheets.Add(sheet);
            }

            return data;
        }

        // ============ 写回 ============
        /// <summary>按原编码/缩进/换行序列化并原子写回；返回备份文件路径。</summary>
        public static string Save(string filePath, JArray array, JsonFileFormat format, string chamberRoot)
        {
            var root = string.IsNullOrWhiteSpace(chamberRoot)
                ? (Path.GetDirectoryName(filePath) ?? "")
                : chamberRoot;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var backup = Path.Combine(root, BackupFolderName, "EC", timestamp, Path.GetFileName(filePath));

            var text = JsonDocumentService.Serialize(array, format);
            JsonDocumentService.SaveAtomic(filePath, text, format, backup);
            return backup;
        }

        // ============ TemplateType 自动推导 ============
        /// <summary>TemplateType：1=Enum，2/3=Text（与 ChamberConfig 现有数据一致）。</summary>
        public static string DeriveTemplateKey(int templateType)
            => templateType == 1 ? "Enum" : "Text";

        /// <summary>
        /// Words 由 TemplateType 自动带出：1 → 1，2/3 → 2。
        /// 说明：真实数据中的 Words 并不完全唯一（同类型存在 1/2/0），此处按主流取值推导，
        /// 仅在用户修改 TemplateType 或新增行时生效；未改类型时保留原值。
        /// </summary>
        public static int DeriveWords(int templateType)
            => templateType == 1 ? 1 : 2;

        // ============ 字段值解析 ============
        public static string[] ParseOptionToken(JToken token)
        {
            if (token is JArray arr)
                return arr.Select(x => x == null || x.Type == JTokenType.Null ? "" : x.ToString())
                          .Where(s => s.Length > 0)
                          .ToArray();
            if (token == null || token.Type == JTokenType.Null) return new string[0];
            var single = token.ToString();
            return single.Length == 0 ? new string[0] : new[] { single };
        }

        public static string BuildOptionText(JToken token)
            => string.Join(" | ", ParseOptionToken(token));

        /// <summary>解析弹框 Option 输入：支持 | 或换行分隔（下拉候选用 | 连接展示）。</summary>
        public static string[] ParseOptionText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new string[0];
            return text.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split(new[] { '\n', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();
        }

        // ============ 新增 / 修改 ============
        public static long NextEcId(IEnumerable<EcEntry> entries)
        {
            var max = 0L;
            if (entries != null)
                foreach (var e in entries)
                    if (e.ECID > max) max = e.ECID;
            return max <= 0 ? 2 : max + 2;
        }

        /// <summary>把弹框字段写入已有 JSON 对象；属性顺序保持原样。批量模式只写勾选字段。</summary>
        public static void Apply(JObject target, EcFieldValues values, bool isNew = false)
        {
            if (target == null || values == null) return;
            var applied = values.AppliedFields;
            bool Use(string field) => isNew || applied == null || applied.Contains(field);

            if (Use("Name")) target["Name"] = values.Name ?? "";
            if (Use("Unit")) target["Unit"] = values.Unit ?? "";
            if (Use("SetValue")) target["SetValue"] = values.SetValue ?? "";
            if (Use("NewValue")) target["NewValue"] = values.NewValue ?? "";
            if (Use("DefaultValue")) target["DefaultValue"] = values.DefaultValue ?? "";
            if (Use("Min")) target["Min"] = values.Min ?? "";
            if (Use("Max")) target["Max"] = values.Max ?? "";
            if (Use("Class")) target["Class"] = values.Class ?? "";

            if (Use("TemplateType"))
            {
                var oldType = target["TemplateType"] == null ? null : target["TemplateType"].ToString();
                target["TemplateType"] = values.TemplateType;
                var changed = isNew || oldType == null || oldType != values.TemplateType.ToString();
                if (changed)
                {
                    target["Words"] = DeriveWords(values.TemplateType);
                    target["TemplateKey"] = DeriveTemplateKey(values.TemplateType);
                }
            }

            if (Use("Option"))
                target["Option"] = new JArray((values.Options ?? new string[0]).Cast<object>().ToArray());

            if (Use("Discription")) target["Discription"] = values.Discription ?? "";
            if (Use("PLCLink")) target["PLCLink"] = values.PLCLink ?? "";
        }

        /// <summary>按真实 JSON 字段顺序创建一条新记录（ECID 由调用方分配）。</summary>
        public static JObject CreateEntry(long ecid, string sheetName, EcFieldValues values)
        {
            values = values ?? new EcFieldValues();
            return new JObject
            {
                ["ECID"] = ecid,
                ["Name"] = values.Name ?? "",
                ["Unit"] = values.Unit ?? "",
                ["SetValue"] = values.SetValue ?? "",
                ["NewValue"] = values.NewValue ?? "",
                ["DefaultValue"] = values.DefaultValue ?? "",
                ["Min"] = values.Min ?? "",
                ["Max"] = values.Max ?? "",
                ["Class"] = values.Class ?? "",
                ["TemplateType"] = values.TemplateType,
                ["Option"] = new JArray((values.Options ?? new string[0]).Cast<object>().ToArray()),
                ["Discription"] = values.Discription ?? "",
                ["PLCLink"] = values.PLCLink ?? "",
                ["SheetName"] = sheetName ?? "",
                ["Words"] = DeriveWords(values.TemplateType),
                ["TemplateKey"] = DeriveTemplateKey(values.TemplateType)
            };
        }
    }
}
