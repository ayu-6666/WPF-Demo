using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CPM.Recipe.Tool.Models;
using CPM.Recipe.Tool.Services;

namespace CPM.Recipe.Tool
{
    /// <summary>
    /// 动态字段编辑对话框：按 JSON 对象实际字段生成可编辑表单
    /// （字符串/数字/布尔用输入控件，字符串数组按行编辑，对象数组用子表编辑，嵌套对象按只读 JSON 展示）。
    /// 新增模式下 Id 可编辑，并额外提供“插入索引”。
    /// </summary>
    public partial class EditItemDialog : Window
    {
        private readonly JObject _original;
        private readonly List<FieldEditor> _fields = new List<FieldEditor>();
        private readonly bool _isNew;
        private readonly int _maxInsertIndex;
        private readonly long _autoId;

        /// <summary>确定后由调用方写回内存/文件的对象（属性顺序与原对象一致）。</summary>
        public JObject Result { get; private set; }

        /// <summary>新增模式下的插入索引（-1 表示末尾）。</summary>
        public int InsertIndex { get; private set; } = -1;

        public EditItemDialog(JObject obj, string context, bool isNew = false, int maxInsertIndex = 0, long autoId = -1)
        {
            InitializeComponent();

            _original = obj?.DeepClone() as JObject ?? new JObject();
            _isNew = isNew;
            _maxInsertIndex = maxInsertIndex;
            _autoId = autoId;

            long id;
            string title;
            if (isNew)
            {
                title = L.T("dlg.edit.newTitle");
            }
            else
            {
                title = JsonDocumentService.TryGetId(_original, out id)
                    ? L.T("dlg.edit.titleWithId", id)
                    : L.T("dlg.edit.title");
            }
            Title = title;
            TitleText.Text = title;

            CancelBtn.Content = L.T("dlg.edit.cancel");
            OkBtn.Content = L.T("dlg.edit.ok");
            CloseBtn.ToolTip = L.T("tip.close");

            var hint = isNew ? L.T("dlg.edit.insertHint") : L.T("dlg.edit.modifyHint");
            if (!isNew && !string.IsNullOrEmpty(context)) hint = L.T("dlg.edit.source", context) + hint;
            HintText.Text = hint;

            IndexRow.Visibility = isNew ? Visibility.Visible : Visibility.Collapsed;
            IndexLabel.Text = L.T("dlg.edit.indexLabel");
            IndexTip.Text = L.T("dlg.edit.indexTip");
            IndexBox.Text = "-1";

            BuildForm();
        }

        // ======================= 表单构建 =======================
        private void BuildForm()
        {
            foreach (var p in _original.Properties())
            {
                // Id 与 TemplateKey 由程序维护：新增模式 Id 不显示（自动分配）；
                // 修改模式 Id 以只读形式展示（跨文件替换正是按 Id 定位），TemplateKey 始终不显示（按 Type 推导）
                if (IsSystemProperty(p.Name, "Id"))
                {
                    if (!_isNew) _fields.Add(CreateField(p.Name, p.Value, true));
                    continue;
                }
                if (IsSystemProperty(p.Name, "TemplateKey")) continue;
                _fields.Add(CreateField(p.Name, p.Value));
            }
        }

        private static bool IsSystemProperty(string name, string expected)
            => string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

        private FieldEditor CreateField(string name, JToken value, bool readOnlyDisplay = false)
        {
            var isType = IsSystemProperty(name, "Type");
            var isId = IsSystemProperty(name, "Id");
            var isRequired = !readOnlyDisplay && (isType || IsSystemProperty(name, "Name"));

            var f = new FieldEditor
            {
                Name = name,
                Original = value,
                Kind = value.Type,
                Required = isRequired,
                ReadOnly = readOnlyDisplay
            };

            // ---- 左列标题 ----
            var label = new TextBlock
            {
                Text = isRequired ? name + "  *" : name,
                FontSize = 12.5,
                Foreground = BrushOf("TextMain"),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            ToolTipService.SetToolTip(label, isType ? L.T("dlg.edit.typeTip") : (isId ? L.T("dlg.edit.idHint") : name));

            // ---- 右列编辑器 ----
            var editorHost = new StackPanel();
            Control editor = null;

            if (value.Type == JTokenType.Boolean)
            {
                f.Check = new CheckBox { IsChecked = value.Value<bool>(), Content = L.T("dlg.edit.boolHint") };
                editor = f.Check;
            }
            else if (value is JArray arr)
            {
                if (arr.Count == 0)
                {
                    f.ScalarArrayType = JTokenType.String;
                    f.Text = CreateMultilineText("");
                    editor = f.Text;
                }
                else if (arr.All(x => x is JObject))
                {
                    editor = CreateStepsGrid(f, arr);
                }
                else if (arr.All(x => !(x is JContainer)))
                {
                    f.ScalarArrayType = arr.FirstOrDefault(x => x.Type != JTokenType.Null)?.Type ?? JTokenType.String;
                    f.Text = CreateMultilineText(string.Join(Environment.NewLine, arr.Select(x => x.Type == JTokenType.Null ? "" : x.ToString())));
                    editor = f.Text;
                }
                else
                {
                    f.Text = CreateMultilineText(arr.ToString());
                    f.Text.IsReadOnly = true;
                    f.Text.Foreground = BrushOf("TextSub");
                    editor = f.Text;
                }
            }
            else if (value is JObject)
            {
                f.Text = CreateMultilineText(value.ToString());
                f.Text.IsReadOnly = true;
                f.Text.Foreground = BrushOf("TextSub");
                ToolTipService.SetToolTip(f.Text, L.T("dlg.edit.nestedTip"));
                editor = f.Text;
            }
            else if (isType)
            {
                // Type 用下拉枚举；取值来自 SectionTypeCatalog（现有数据实际使用的类型集合）
                var items = SectionTypeCatalog.Types.ToList();
                var current = value.Type == JTokenType.Null ? "" : value.ToString();
                if (current.Length > 0 && !items.Any(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase)))
                    items.Insert(0, current);
                if (current.Length == 0) items.Insert(0, "");   // 保留历史空值，避免保存时被改成 int

                var combo = new ComboBox
                {
                    ItemsSource = items,
                    MinHeight = 34,
                    IsEditable = false
                };
                combo.SelectedItem = items.FirstOrDefault(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase));
                if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
                f.Combo = combo;
                editor = combo;
            }
            else
            {
                var text = value.Type == JTokenType.Null ? "" : value.ToString();
                f.Text = CreateSingleText(text, readOnlyDisplay);
                editor = f.Text;
            }

            // 修改场景：每个属性前加启用复选框，默认不勾选；未勾选时编辑器置灰禁用。
            // 只有勾选的属性才会在保存时写回，未勾选的保持文件原值。
            f.Editor = editor;
            if (!_isNew && !readOnlyDisplay)
            {
                var enable = new CheckBox
                {
                    IsChecked = false,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                    ToolTip = L.T("dlg.edit.enableTip")
                };
                f.Enable = enable;
                editor.IsEnabled = false;
                editor.Opacity = 0.55;
                enable.Checked += (s, e) => { editor.IsEnabled = true; editor.Opacity = 1.0; ClearErrors(); };
                enable.Unchecked += (s, e) => { editor.IsEnabled = false; editor.Opacity = 0.55; };
            }

            editorHost.Children.Add(editor);

            var err = new TextBlock
            {
                Text = "",
                FontSize = 11.5,
                Foreground = BrushOf("ErrorText"),
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            f.Error = err;
            editorHost.Children.Add(err);

            var hasEnable = f.Enable != null;
            var row = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            if (hasEnable) row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelCol = hasEnable ? 1 : 0;
            Grid.SetColumn(label, labelCol);
            Grid.SetColumn(editorHost, labelCol + 1);
            if (hasEnable)
            {
                Grid.SetColumn(f.Enable, 0);
                row.Children.Add(f.Enable);
            }
            row.Children.Add(label);
            row.Children.Add(editorHost);

            FormPanel.Children.Add(row);
            return f;
        }

        private TextBox CreateSingleText(string text, bool readOnly)
        {
            var tb = new TextBox { Text = text ?? "", IsReadOnly = readOnly, MinHeight = 34 };
            if (readOnly) tb.Foreground = BrushOf("TextSub");
            return tb;
        }

        private TextBox CreateMultilineText(string text)
        {
            return new TextBox
            {
                Text = text ?? "",
                AcceptsReturn = true,
                Height = 86,
                VerticalContentAlignment = VerticalAlignment.Top,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12
            };
        }

        private DataGrid CreateStepsGrid(FieldEditor f, JArray arr)
        {
            var columns = new List<string>();
            foreach (var o in arr.OfType<JObject>())
                foreach (var p in o.Properties())
                    if (!columns.Contains(p.Name)) columns.Add(p.Name);

            var table = new DataTable();
            foreach (var c in columns) table.Columns.Add(c, typeof(string));

            var valueTypes = new Dictionary<string, JTokenType>();
            var containerColumns = new HashSet<string>();
            foreach (var o in arr.OfType<JObject>())
            {
                foreach (var p in o.Properties())
                {
                    if (p.Value is JContainer) containerColumns.Add(p.Name);
                    if (!valueTypes.ContainsKey(p.Name) && !(p.Value is JContainer))
                        valueTypes[p.Name] = p.Value.Type;
                }
            }

            foreach (var o in arr.OfType<JObject>())
            {
                var r = table.NewRow();
                foreach (var c in columns)
                {
                    var t = o[c];
                    r[c] = t == null || t.Type == JTokenType.Null ? "" :
                        (t is JContainer ? t.ToString(Formatting.None) : t.ToString());
                }
                table.Rows.Add(r);
            }

            f.GridTable = table;
            f.GridColumns = columns;
            f.ColumnValueTypes = valueTypes;
            f.ContainerColumns = containerColumns;

            var grid = new DataGrid
            {
                ItemsSource = table.DefaultView,
                AutoGenerateColumns = false,
                IsReadOnly = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                SelectionUnit = DataGridSelectionUnit.Cell,
                Height = Math.Min(250, 34 + 30 * Math.Max(3, Math.Min(arr.Count, 7))),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            foreach (var c in columns)
            {
                var isIdCol = string.Equals(c, "Id", StringComparison.OrdinalIgnoreCase);
                var col = new DataGridTextColumn
                {
                    Header = c,
                    Binding = new Binding("[" + c + "]"),
                    IsReadOnly = isIdCol || containerColumns.Contains(c),
                    Width = isIdCol ? new DataGridLength(70) : new DataGridLength(1, DataGridLengthUnitType.Star)
                };
                grid.Columns.Add(col);
            }

            ToolTipService.SetToolTip(grid, L.T("dlg.edit.gridTip"));
            return grid;
        }

        // ======================= 校验与取值 =======================
        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            ClearErrors();

            // 修改场景：一个属性都没勾选时给出提示，避免静默无操作
            if (!_isNew && _fields.All(f => f.Enable == null || f.Enable.IsChecked != true))
            {
                ErrorSummary.Text = L.T("dlg.edit.noFieldSelected");
                return;
            }

            var errors = new List<string>();
            var values = new Dictionary<string, JToken>(StringComparer.Ordinal);

            foreach (var f in _fields)
            {
                // 修改场景：未勾选的属性不读取、不写回，保持文件原值
                if (!_isNew && f.Enable != null && f.Enable.IsChecked != true) continue;
                try
                {
                    var token = f.ReadToken();
                    if (token == null) continue;
                    values[f.Name] = token;
                }
                catch (FieldException ex)
                {
                    ShowFieldError(f, ex.Message);
                    errors.Add($"{f.Name}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    ShowFieldError(f, ex.Message);
                    errors.Add($"{f.Name}: {ex.Message}");
                }
            }

            // 必填校验（修改场景只校验勾选的属性）
            foreach (var f in _fields.Where(x => x.Required && (_isNew || x.Enable == null || x.Enable.IsChecked == true)))
            {
                var v = values.TryGetValue(f.Name, out var tok) ? tok : null;
                if (v == null || v.Type == JTokenType.Null || v.ToString().Trim().Length == 0)
                {
                    ShowFieldError(f, L.T("dlg.edit.required"));
                    errors.Add($"{f.Name}: {L.T("dlg.edit.required")}");
                }
            }

            // 按原属性顺序重建对象；Id / TemplateKey 由程序写入（不显示给用户）
            var obj = BuildOrderedObject(values);

            // Section 数值范围 / 下拉取值校验：修改场景只有勾选了相关属性时才校验，
            // 避免未参与本次修改的历史数据阻断保存。
            var semanticTouched = _isNew || _fields.Any(f => f.Enable != null && f.Enable.IsChecked == true &&
                (IsSystemProperty(f.Name, "Type") || IsSystemProperty(f.Name, "Default") ||
                 IsSystemProperty(f.Name, "Min") || IsSystemProperty(f.Name, "Max") ||
                 IsSystemProperty(f.Name, "Options")));

            if (semanticTouched)
            {
                var rangeError = ValidateRecipeSemantics(obj, out var rangeField);
                if (rangeError != null)
                {
                    var field = _fields.FirstOrDefault(x => string.Equals(x.Name, rangeField, StringComparison.OrdinalIgnoreCase));
                    if (field != null) ShowFieldError(field, rangeError);
                    errors.Add(rangeError);
                }
            }

            // 插入索引校验（新增模式）
            if (_isNew)
            {
                var raw = (IndexBox.Text ?? "").Trim();
                int idx = -1;
                if (raw.Length > 0 && !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out idx))
                    idx = int.MinValue;
                if (idx < -1 || idx > _maxInsertIndex)
                {
                    errors.Add(L.T("dlg.edit.indexInvalid", _maxInsertIndex));
                }
                else
                {
                    InsertIndex = idx;
                }
            }

            if (errors.Count > 0)
            {
                ErrorSummary.Text = L.T("dlg.edit.errors", errors.Count);
                return;
            }

            ErrorSummary.Text = "";
            Result = obj;
            DialogResult = true;
        }

        /// <summary>
        /// 按原对象属性顺序组装结果：Id 保持原值（新增时用自动分配值），
        /// TemplateKey 由 Type 经 SectionTypeCatalog 推导后写入（原对象无该属性则不新增）。
        /// </summary>
        private JObject BuildOrderedObject(Dictionary<string, JToken> values)
        {
            var typeValue = values.TryGetValue("Type", out var t)
                ? t.ToString()
                : (_original["Type"]?.ToString() ?? "");

            long originalId = 0;
            var hasOriginalId = JsonDocumentService.TryGetId(_original, out originalId);

            var obj = new JObject();
            var idWritten = false;

            foreach (var p in _original.Properties())
            {
                if (IsSystemProperty(p.Name, "Id"))
                {
                    var id = _isNew ? (_autoId > 0 ? _autoId : 1) : (hasOriginalId ? originalId : 0);
                    obj[p.Name] = new JValue(id);
                    idWritten = true;
                    continue;
                }
                if (IsSystemProperty(p.Name, "TemplateKey"))
                {
                    // templateKey 由 type 自动推导更新；修改场景未勾选 Type 时保持原值
                    obj[p.Name] = (_isNew || values.ContainsKey("Type"))
                        ? new JValue(SectionTypeCatalog.TemplateKeyFor(typeValue))
                        : p.Value?.DeepClone();
                    continue;
                }
                // 勾选的属性写入新值；未勾选的保持文件原值
                if (values.TryGetValue(p.Name, out var tok)) obj[p.Name] = tok;
                else obj[p.Name] = p.Value?.DeepClone();
            }

            // 原对象没有 Id 属性（新增场景）时补一个，保证后续可重排/按 Id 定位
            if (!idWritten && _isNew)
                obj.AddFirst(new JProperty("Id", _autoId > 0 ? _autoId : 1));

            return obj;
        }

        /// <summary>按 Recipe Section 语义补充校验：Default 需落在 [Min, Max] 内或属于 Options。</summary>
        private string ValidateRecipeSemantics(JObject obj, out string fieldName)
        {
            fieldName = "Default";
            if (_original.Property("Default") == null) return null;

            var type = obj["Type"]?.ToString() ?? "";
            var def = obj["Default"]?.ToString() ?? "";
            var minTok = obj["Min"];
            var maxTok = obj["Max"];

            var numeric = type.Equals("int", StringComparison.OrdinalIgnoreCase)
                          || type.Equals("float", StringComparison.OrdinalIgnoreCase)
                          || type.Equals("double", StringComparison.OrdinalIgnoreCase);

            if (numeric && minTok != null && maxTok != null)
            {
                long min, max;
                if (TryLong(minTok, out min) && TryLong(maxTok, out max) && max > min && def.Trim().Length > 0)
                {
                    double d;
                    if (!double.TryParse(def, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                        return L.T("dlg.edit.defaultNumber", def);
                    if (d < min || d > max) return L.T("dlg.edit.defaultRange", min, max, d);
                }
            }

            var options = obj["Options"] as JArray;
            if (options != null && options.Count > 0 && def.Trim().Length > 0)
            {
                var texts = options.Select(o => o.ToString()).ToList();
                if (!texts.Contains(def))
                {
                    // 兼容 "1:Vent" 这类带编码的选项：允许只写编码或只写名称
                    var ok = texts.Any(t =>
                    {
                        var parts = t.Split(':');
                        return (parts.Length > 1 && (parts[0].Trim() == def.Trim() || parts[1].Trim() == def.Trim()));
                    });
                    if (!ok)
                        return L.T("dlg.edit.defaultOptions", string.Join(" / ", texts.Take(6)));
                }
            }

            return null;
        }

        private static bool TryLong(JToken token, out long value)
        {
            value = 0;
            if (token == null || token.Type == JTokenType.Null) return false;
            if (token.Type == JTokenType.Integer) { value = token.Value<long>(); return true; }
            if (token.Type == JTokenType.String)
                return long.TryParse(token.ToString().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            return false;
        }

        private void ClearErrors()
        {
            ErrorSummary.Text = "";
            foreach (var f in _fields)
            {
                f.Error.Text = "";
                f.Error.Visibility = Visibility.Collapsed;
            }
        }

        private static void ShowFieldError(FieldEditor f, string message)
        {
            if (f == null) return;
            f.Error.Text = message;
            f.Error.Visibility = Visibility.Visible;
        }

        /// <summary>按当前主题取画刷（浅色主题的覆盖字典优先级更高）。</summary>
        private static Brush BrushOf(string key)
        {
            var res = Application.Current.TryFindResource(key) as Brush;
            return res ?? Brushes.Transparent;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Result = null;
            DialogResult = false;
        }

        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        // ======================= 内部类型 =======================
        private sealed class FieldException : Exception
        {
            public FieldException(string message) : base(message) { }
        }

        private sealed class FieldEditor
        {
            public string Name;
            public JToken Original;
            public JTokenType Kind;
            public bool Required;
            public bool ReadOnly;

            public TextBox Text;
            public CheckBox Check;
            public ComboBox Combo;
            public TextBlock Error;
            public CheckBox Enable;      // 修改场景：是否写回该属性
            public Control Editor;       // 对应编辑器，勾选开关时控制启用状态

            public JTokenType ScalarArrayType;
            public DataTable GridTable;
            public List<string> GridColumns;
            public Dictionary<string, JTokenType> ColumnValueTypes;
            public HashSet<string> ContainerColumns;

            public JToken ReadToken()
            {
                if (Kind == JTokenType.Object) return Original.DeepClone();

                if (Check != null) return new JValue(Check.IsChecked == true);

                if (Combo != null)
                {
                    var selected = Combo.SelectedItem?.ToString() ?? "";
                    return new JValue(selected.Trim());
                }

                if (GridTable != null) return ReadGrid();

                if (Original is JArray) return ReadScalarArray();

                // 标量
                var raw = Text?.Text ?? "";
                if (Kind == JTokenType.Null)
                    return raw.Length == 0 ? JValue.CreateNull() : new JValue(raw);

                switch (Kind)
                {
                    case JTokenType.Integer:
                    {
                        long v;
                        if (!long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                            throw new FieldException(L.T("dlg.edit.int", raw));
                        return new JValue(v);
                    }
                    case JTokenType.Float:
                    {
                        double v;
                        if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                            throw new FieldException(L.T("dlg.edit.number", raw));
                        return new JValue(v);
                    }
                    case JTokenType.Boolean:
                    {
                        var s = raw.Trim().ToLowerInvariant();
                        if (s == "true" || s == "1") return new JValue(true);
                        if (s == "false" || s == "0") return new JValue(false);
                        throw new FieldException(L.T("dlg.edit.bool"));
                    }
                    default:
                        return new JValue(raw);
                }
            }

            private JToken ReadScalarArray()
            {
                var lines = (Text?.Text ?? "").Replace("\r\n", "\n").Split('\n');
                var arr = new JArray();
                foreach (var line in lines)
                {
                    var s = line.Trim();
                    if (s.Length == 0) continue;
                    switch (ScalarArrayType)
                    {
                        case JTokenType.Integer:
                        {
                            long v;
                            if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                                throw new FieldException(L.T("dlg.edit.arrayInt", s));
                            arr.Add(new JValue(v));
                            break;
                        }
                        case JTokenType.Float:
                        {
                            double v;
                            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                                throw new FieldException(L.T("dlg.edit.arrayNumber", s));
                            arr.Add(new JValue(v));
                            break;
                        }
                        case JTokenType.Boolean:
                            arr.Add(new JValue(s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1"));
                            break;
                        default:
                            arr.Add(new JValue(s));
                            break;
                    }
                }
                return arr;
            }

            private JToken ReadGrid()
            {
                var arr = new JArray();
                foreach (DataRow row in GridTable.Rows)
                {
                    var obj = new JObject();
                    foreach (var col in GridColumns)
                    {
                        var s = (row[col] as string) ?? "";
                        if (ContainerColumns.Contains(col))
                        {
                            if (s.Trim().Length == 0) { obj[col] = JValue.CreateNull(); continue; }
                            try { obj[col] = JToken.Parse(s); }
                            catch (JsonException) { throw new FieldException(L.T("dlg.edit.gridJson", col)); }
                            continue;
                        }

                        var vt = ColumnValueTypes.TryGetValue(col, out var t) ? t : JTokenType.String;
                        if (vt == JTokenType.Integer)
                        {
                            long v;
                            if (!long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                                throw new FieldException(L.T("dlg.edit.gridInt", col, s));
                            obj[col] = new JValue(v);
                        }
                        else if (vt == JTokenType.Float)
                        {
                            double v;
                            if (!double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                                throw new FieldException(L.T("dlg.edit.gridNumber", col, s));
                            obj[col] = new JValue(v);
                        }
                        else if (vt == JTokenType.Boolean)
                        {
                            obj[col] = new JValue(s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1");
                        }
                        else
                        {
                            obj[col] = s.Length == 0 ? JValue.CreateNull() : new JValue(s);
                        }
                    }
                    arr.Add(obj);
                }
                return arr;
            }
        }
    }
}
