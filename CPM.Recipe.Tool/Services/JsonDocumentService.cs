using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CPM.Recipe.Tool.Services
{
    /// <summary>JSON 文件的原始格式特征（编码 / 缩进 / 换行 / 是否压缩），用于写回时保持一致。</summary>
    public sealed class JsonFileFormat
    {
        public Encoding Encoding { get; set; } = new UTF8Encoding(false);
        public bool HasBom { get; set; }
        public char IndentChar { get; set; } = ' ';
        public int IndentSize { get; set; } = 2;
        public bool Compact { get; set; }
        public string NewLine { get; set; } = "\r\n";
        public bool TrailingNewLine { get; set; }

        /// <summary>原文件是否把非 ASCII 字符写成 \uXXXX 转义（Pen/Polish/Roller 等文件如此）。</summary>
        public bool EscapeNonAscii { get; set; }

        public string Describe()
        {
            var enc = HasBom ? "UTF-8 BOM" : (Encoding?.WebName?.ToUpperInvariant() ?? "UTF-8");
            var indent = Compact ? "压缩(无缩进)" : $"缩进 {IndentSize} {(IndentChar == '\t' ? "Tab" : "空格")}";
            var nl = NewLine == "\n" ? "LF" : "CRLF";
            var esc = EscapeNonAscii ? " · \\u 转义" : "";
            return $"{enc} · {indent} · {nl} · {esc}";
        }
    }

    public sealed class JsonDocument
    {
        public string Path { get; set; }
        public JToken Root { get; set; }
        public string RawText { get; set; }
        public JsonFileFormat Format { get; set; }
    }

    public sealed class FileEditResult
    {
        public string FilePath { get; set; }
        public int TotalHits { get; set; }
        public bool Success { get; set; } = true;
        public bool Skipped { get; set; }
        public string Error { get; set; }
        public string BackupPath { get; set; }
        public Dictionary<long, int> HitsById { get; } = new Dictionary<long, int>();
    }

    /// <summary>
    /// JSON 读写服务：编码/缩进/换行探测、原子写、写前备份、按 Id 跨文件替换对象。
    /// </summary>
    public static class JsonDocumentService
    {
        // ============ 读取 ============
        public static JsonDocument Load(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("文件不存在", filePath);

            var bytes = File.ReadAllBytes(filePath);
            var encoding = DetectEncoding(bytes, out var hasBom);
            var text = File.ReadAllText(filePath, encoding);

            return new JsonDocument
            {
                Path = filePath,
                Root = ParseRoot(text, filePath),
                RawText = text,
                Format = DetectFormat(text, encoding, hasBom)
            };
        }

        private static JToken ParseRoot(string text, string filePath)
        {
            try
            {
                return JToken.Parse(text);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"JSON 解析失败: {filePath}\n{ex.Message}", ex);
            }
        }

        private static Encoding DetectEncoding(byte[] bytes, out bool hasBom)
        {
            hasBom = false;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                hasBom = true;
                return new UTF8Encoding(true);
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                hasBom = true;
                return Encoding.Unicode;
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                hasBom = true;
                return Encoding.BigEndianUnicode;
            }

            // 无 BOM：先按严格 UTF-8 试解码，失败则按系统 ANSI（中文系统即 GBK/GB18030）处理，避免预览乱码
            try
            {
                new UTF8Encoding(false, true).GetString(bytes);
                return new UTF8Encoding(false);
            }
            catch (DecoderFallbackException)
            {
                try { return Encoding.GetEncoding(936); }
                catch { return Encoding.Default; }
            }
        }

        public static JsonFileFormat DetectFormat(string text, Encoding encoding, bool hasBom)
        {
            var fmt = new JsonFileFormat
            {
                Encoding = encoding,
                HasBom = hasBom,
                NewLine = text.Contains("\r\n") ? "\r\n" : "\n",
                TrailingNewLine = text.EndsWith("\n", StringComparison.Ordinal),
                Compact = !text.Contains("\n")
            };

            // 原文件若通篇为 ASCII 且含 \uXXXX，说明用的是 Unicode 转义风格；写回必须保持，否则未修改行也会变。
            fmt.EscapeNonAscii = !ContainsNonAscii(text)
                && text.IndexOf("\\u", StringComparison.Ordinal) >= 0;

            if (!fmt.Compact)
            {
                foreach (var line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                {
                    if (line.Length == 0 || line.Trim().Length == 0) continue;
                    if (line[0] == ' ' || line[0] == '\t')
                    {
                        var c = line[0];
                        int n = 0;
                        while (n < line.Length && line[n] == c) n++;
                        fmt.IndentChar = c;
                        fmt.IndentSize = Math.Max(1, n);
                        break;
                    }
                    // 第一行无缩进（如 "{"），继续找下一行
                }
            }
            return fmt;
        }

        // ============ 序列化（保留缩进/换行风格） ============
        public static string Serialize(JToken token, JsonFileFormat fmt)
        {
            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            using (var jw = new JsonTextWriter(sw))
            {
                jw.Formatting = fmt.Compact ? Formatting.None : Formatting.Indented;
                jw.Indentation = Math.Max(1, fmt.IndentSize);
                jw.IndentChar = fmt.IndentChar == '\t' ? '\t' : ' ';
                jw.StringEscapeHandling = fmt.EscapeNonAscii
                    ? StringEscapeHandling.EscapeNonAscii
                    : StringEscapeHandling.Default;
                token.WriteTo(jw);
            }

            var text = sb.ToString().Replace("\r\n", "\n");
            if (fmt.EscapeNonAscii)
            {
                // 与原写入器保持一致：\u 十六进制一律大写，且 & < > 也写成 \u 转义。
                text = Regex.Replace(text, @"\\u([0-9a-fA-F]{4})",
                    m => "\\u" + m.Groups[1].Value.ToUpperInvariant());
                text = text.Replace("&", "\\u0026").Replace("<", "\\u003C").Replace(">", "\\u003E");
            }
            if (fmt.NewLine == "\r\n") text = text.Replace("\n", "\r\n");
            if (fmt.TrailingNewLine) text = text.TrimEnd('\r', '\n') + fmt.NewLine;
            return text;
        }

        // ============ 写入（先备份，再原子替换） ============
        public static void SaveAtomic(string filePath, string text, JsonFileFormat fmt, string backupPath)
        {
            var dir = System.IO.Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir)) throw new IOException("无效的文件路径: " + filePath);

            var tmp = System.IO.Path.Combine(dir, "." + System.IO.Path.GetFileName(filePath) + "."
                + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp");

            var encoding = fmt?.Encoding ?? new UTF8Encoding(false);
            File.WriteAllText(tmp, text, encoding);

            try
            {
                if (File.Exists(filePath))
                {
                    if (!string.IsNullOrEmpty(backupPath))
                    {
                        var bdir = System.IO.Path.GetDirectoryName(backupPath);
                        if (!string.IsNullOrEmpty(bdir)) Directory.CreateDirectory(bdir);
                        if (File.Exists(backupPath)) File.Delete(backupPath);
                    }
                    // File.Replace 在同一卷内为原子替换，并把旧内容移动到备份路径
                    File.Replace(tmp, filePath, string.IsNullOrEmpty(backupPath) ? null : backupPath, true);
                }
                else
                {
                    if (!string.IsNullOrEmpty(backupPath)) { /* 文件不存在时无需备份 */ }
                    File.Move(tmp, filePath);
                }
            }
            catch (Exception)
            {
                // 回退路径：先备份旧文件，再用临时文件覆盖，最后清理临时文件
                TryDelete(tmp);
                File.WriteAllText(tmp, text, encoding);
                if (File.Exists(filePath) && !string.IsNullOrEmpty(backupPath))
                {
                    var bdir = System.IO.Path.GetDirectoryName(backupPath);
                    if (!string.IsNullOrEmpty(bdir)) Directory.CreateDirectory(bdir);
                    File.Copy(filePath, backupPath, true);
                }
                File.Copy(tmp, filePath, true);
                TryDelete(tmp);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        // ============ 数据集合/Id 解析 ============
        /// <summary>
        /// 找到当前 JSON 的主数据数组（优先 root["Data"]，否则第一个"对象数组"属性；根节点为数组时返回根）。
        /// propertyName 为空字符串表示根节点即数组。
        /// </summary>
        public static JArray GetPrimaryArray(JToken root, out string propertyName)
        {
            propertyName = null;
            if (root is JObject obj)
            {
                var dataProp = obj.Property("Data");
                if (dataProp?.Value is JArray dataArr) { propertyName = dataProp.Name; return dataArr; }

                foreach (var p in obj.Properties())
                {
                    if (p.Value is JArray arr && arr.Count > 0 && arr[0] is JObject)
                    {
                        propertyName = p.Name;
                        return arr;
                    }
                }
                foreach (var p in obj.Properties())
                {
                    if (p.Value is JArray arr2)
                    {
                        propertyName = p.Name;
                        return arr2;
                    }
                }
                return null;
            }
            if (root is JArray rootArr)
            {
                propertyName = "";
                return rootArr;
            }
            return null;
        }

        public static JArray FindArrayByProperty(JToken root, string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return root as JArray;
            if (root is JObject obj && obj[propertyName] is JArray arr)
                return arr;
            return null;
        }

        private static bool ContainsNonAscii(string text)
        {
            foreach (var ch in text)
                if (ch > 0x7F) return true;
            return false;
        }

        public static bool TryGetId(JObject obj, out long id)
        {
            id = 0;
            if (obj == null) return false;
            var token = obj["Id"];
            if (token == null || token.Type == JTokenType.Null) return false;
            if (token.Type == JTokenType.Integer) { id = token.Value<long>(); return true; }
            if (token.Type == JTokenType.Float)
            {
                var d = token.Value<double>();
                if (d == Math.Floor(d)) { id = (long)d; return true; }
                return false;
            }
            return long.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
        }

        /// <summary>
        /// 在单个文件中按 Id 替换所有匹配对象（同 Id 多次出现时全部替换，调用方负责提示）。
        /// 命中数为 0 时不写文件。
        /// </summary>
        public static FileEditResult ApplyEdits(string filePath, string arrayProperty,
            IList<JObject> edits, Func<string, string> backupPathBuilder)
        {
            var result = new FileEditResult { FilePath = filePath };
            try
            {
                var doc = Load(filePath);
                var arr = FindArrayByProperty(doc.Root, arrayProperty);
                if (arr == null)
                {
                    result.Skipped = true;
                    return result;
                }

                foreach (var edit in edits)
                {
                    long id;
                    if (!TryGetId(edit, out id)) continue;

                    int hits = 0;
                    for (int i = 0; i < arr.Count; i++)
                    {
                        if (arr[i] is JObject existing && TryGetId(existing, out var existId) && existId == id)
                        {
                            arr[i] = edit.DeepClone();
                            hits++;
                        }
                    }
                    if (hits > 0)
                    {
                        result.HitsById[id] = hits;
                        result.TotalHits += hits;
                    }
                }

                if (result.TotalHits == 0) return result;

                var text = Serialize(doc.Root, doc.Format);
                var backupPath = backupPathBuilder == null ? null : backupPathBuilder(filePath);
                SaveAtomic(filePath, text, doc.Format, backupPath);
                result.BackupPath = backupPath;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 在单个文件的“主数据数组”中删除所有指定 Id 的对象（同一文件多处出现全部删除）。
        /// 命中数为 0 时不写文件；写回沿用原文件编码/缩进并走备份 + 原子替换。
        /// </summary>
        public static FileEditResult RemoveByIds(string filePath, ICollection<long> ids,
            Func<string, string> backupPathBuilder)
        {
            var result = new FileEditResult { FilePath = filePath };
            try
            {
                if (ids == null || ids.Count == 0) { result.Skipped = true; return result; }

                var doc = Load(filePath);
                string prop;
                var arr = GetPrimaryArray(doc.Root, out prop);
                if (arr == null) { result.Skipped = true; return result; }

                foreach (var id in ids)
                {
                    var hits = 0;
                    for (var i = arr.Count - 1; i >= 0; i--)
                    {
                        if (arr[i] is JObject existing && TryGetId(existing, out var existId) && existId == id)
                        {
                            arr.RemoveAt(i);
                            hits++;
                        }
                    }
                    if (hits > 0)
                    {
                        result.HitsById[id] = hits;
                        result.TotalHits += hits;
                    }
                }

                if (result.TotalHits == 0) return result;

                var text = Serialize(doc.Root, doc.Format);
                var backupPath = backupPathBuilder == null ? null : backupPathBuilder(filePath);
                SaveAtomic(filePath, text, doc.Format, backupPath);
                result.BackupPath = backupPath;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }
            return result;
        }

        /// <summary>按原文件编码风格写回整份 JSON（用于既有"保存到迁移目录"路径，修复编码丢失问题）。</summary>
        public static void SaveWithOriginalFormat(string sourcePathForFormat, string destPath, JToken root)
        {
            JsonFileFormat fmt;
            if (File.Exists(sourcePathForFormat))
            {
                var bytes = File.ReadAllBytes(sourcePathForFormat);
                var enc = DetectEncoding(bytes, out var hasBom);
                fmt = DetectFormat(File.ReadAllText(sourcePathForFormat, enc), enc, hasBom);
            }
            else
            {
                fmt = new JsonFileFormat();
            }

            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(destPath, Serialize(root, fmt), fmt.Encoding);
        }

        /// <summary>
        /// 重排单个文件主数据数组的 Id：按数组顺序从 1 开始重写所有对象的 Id，
        /// 保持位置不变、其他属性不变；重排后备份 + 原子写回。
        /// 没有主数据数组、数组为空、或无 Id 对象时不写文件。
        /// </summary>
        public static FileEditResult ResequenceIds(string filePath, Func<string, string> backupPathBuilder)
        {
            var result = new FileEditResult { FilePath = filePath };
            try
            {
                var doc = Load(filePath);
                string prop;
                var arr = GetPrimaryArray(doc.Root, out prop);
                if (arr == null) { result.Skipped = true; return result; }
                if (arr.Count == 0) { result.Skipped = true; return result; }

                var changed = false;
                for (int i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is JObject obj && obj["Id"] != null)
                    {
                        var newId = i + 1;
                        var existing = obj["Id"];
                        var existingLong = TryGetId(obj, out var curId) ? curId : (long?)null;
                        if (existingLong != newId)
                        {
                            obj["Id"] = new JValue(newId);
                            changed = true;
                            result.TotalHits++;
                        }
                    }
                }

                if (!changed) { result.Skipped = true; return result; }

                var text = Serialize(doc.Root, doc.Format);
                var backupPath = backupPathBuilder == null ? null : backupPathBuilder(filePath);
                SaveAtomic(filePath, text, doc.Format, backupPath);
                result.BackupPath = backupPath;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }
            return result;
        }
    }
}
