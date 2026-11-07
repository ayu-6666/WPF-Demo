using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CPM.Recipe.Tool.Models;
using CPM.Recipe.Tool.Services;

namespace CPM.Recipe.Tool
{
    /// <summary>
    /// EC 记录编辑/新增弹框。
    /// TemplateKey 决定 TemplateType / Words：Enum → 1/1 并显示 Option 下拉；Text → 2 或 3/2 且隐藏 Option。
    /// 修改模式按当前类别 + 当前 SheetName 整批处理：每个属性前的勾选框决定该属性是否写入，
    /// 未勾选的属性保持各文件原值。新增：可指定在该类别每个文件中新增的数量。
    /// </summary>
    public partial class EcEditDialog : Window
    {
        private const string KeyEnum = "Enum";
        private const string KeyText = "Text";

        private readonly List<EcEntry> _entries;
        private readonly bool _isNew;
        private readonly bool _isBatch;
        private readonly string _sheetName;
        private readonly string _categoryName;
        private readonly int _categoryFileCount;
        private readonly List<string> _optionChoices = new List<string>();
        private int _templateType;
        private int _textTemplateType = 2;
        private string _currentKey = KeyText;
        private string _enumOptionsText = "";
        private bool _suspendLink;
        private string _class = "";

        /// <summary>确定后回传的字段值。</summary>
        public EcFieldValues Result { get; private set; }

        /// <summary>新增模式下的批量数量。</summary>
        public int Count { get; private set; } = 1;

        public EcEditDialog(IList<EcEntry> entries, string sheetName, bool isNew, int templateType = 2,
                            long nextEcId = 0, IEnumerable<EcEntry> optionSource = null,
                            string categoryName = null, int categoryFileCount = 0)
        {
            InitializeComponent();

            _entries = entries?.ToList() ?? new List<EcEntry>();
            _isNew = isNew;
            _isBatch = !isNew && _entries.Count > 1;
            _sheetName = sheetName ?? "";
            _categoryName = categoryName ?? "";
            _categoryFileCount = Math.Max(categoryFileCount, 1);
            _templateType = templateType;
            // Text 状态的 TemplateType 固定为 3
            _textTemplateType = 3;

            CancelBtn.Content = L.T("dlg.edit.cancel");
            OkBtn.Content = L.T("dlg.edit.ok");
            CloseBtn.ToolTip = L.T("tip.close");
            OptionHint.Text = L.T("ec.dlg.optionHint");
            ECIDBox.ToolTip = L.T("ec.dlg.ecidHint");
            CountLabel.Text = L.T("ec.dlg.count");
            CountHint.Text = L.T("ec.dlg.countHint");
            CountRow.Visibility = isNew ? Visibility.Visible : Visibility.Collapsed;

            // 属性勾选框：新增模式不需要（新增时全部字段都会写入）；修改模式默认不勾选
            var checks = new[] { ChkName, ChkUnit, ChkPlcLink, ChkTemplateKey, ChkOption,
                                 ChkSetValue, ChkNewValue, ChkDefaultValue, ChkMin, ChkMax, ChkDiscription };
            foreach (var chk in checks)
            {
                chk.ToolTip = L.T("ec.dlg.fieldChkTip");
                chk.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
                chk.IsChecked = false;
            }

            // TemplateKey 下拉：Enum / Text 为 JSON 数据值，不随界面语言翻译
            TemplateKeyCombo.ItemsSource = new[] { KeyEnum, KeyText };

            var source = new List<EcEntry>();
            if (optionSource != null)
                foreach (var e in optionSource)
                    if (e != null) source.Add(e);
            BuildOptionChoices(source);
            OptionCombo.ItemsSource = _optionChoices;

            if (isNew)
            {
                var title = L.T("ec.dlg.newTitle", _categoryName);
                Title = title;
                TitleText.Text = title;
                ModeHint.Text = L.T("ec.dlg.newHint", _sheetName, _categoryName, _categoryFileCount);
                HintText.Text = L.T("ec.dlg.newFooter", _categoryName, _categoryFileCount);
                ECIDBox.Text = (nextEcId > 0 ? nextEcId : EcConfigService.NextEcId(source))
                    .ToString(CultureInfo.InvariantCulture);
                Fill(new EcFieldValues { TemplateType = templateType });
                SelectTemplateKey(EcConfigService.DeriveTemplateKey(templateType));
            }
            else
            {
                var first = _entries[0];
                _class = first.Class ?? "";
                // Text 状态统一按 TemplateType=3 展示（历史数据中的 2 视为 3）
                _templateType = first.TemplateType == 1 ? 1 : 3;
                _textTemplateType = 3;
                var title = _isBatch
                    ? L.T("ec.dlg.batchTitle", _entries.Count, _categoryName)
                    : L.T("ec.dlg.editTitle", _categoryName);
                Title = title;
                TitleText.Text = title;
                ModeHint.Text = L.T("ec.dlg.batchHint", _categoryName, _categoryFileCount, _sheetName, _entries.Count);
                HintText.Text = L.T("ec.dlg.batchFooter");
                ECIDBox.Text = first.ECID.ToString(CultureInfo.InvariantCulture);

                Fill(new EcFieldValues
                {
                    Name = first.Name,
                    Unit = first.Unit,
                    SetValue = first.SetValue,
                    NewValue = first.NewValue,
                    DefaultValue = first.DefaultValue,
                    Min = first.Min,
                    Max = first.Max,
                    TemplateType = first.TemplateType,
                    Discription = first.Discription,
                    PLCLink = first.PLCLink,
                    Options = first.Options
                });

                var key = string.IsNullOrWhiteSpace(first.TemplateKey)
                    ? EcConfigService.DeriveTemplateKey(first.TemplateType)
                    : first.TemplateKey;
                SelectTemplateKey(key);
            }
        }

        /// <summary>Option 下拉候选值：按文件中真实数据的 Option 数组去重，用 | 连接展示。</summary>
        private void BuildOptionChoices(IEnumerable<EcEntry> source)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in _entries)
                AddOptionChoice(e, seen);
            foreach (var e in source)
                AddOptionChoice(e, seen);
        }

        private void AddOptionChoice(EcEntry entry, HashSet<string> seen)
        {
            if (entry?.Options == null || entry.Options.Length == 0) return;
            var text = string.Join(" | ", entry.Options);
            if (seen.Add(text)) _optionChoices.Add(text);
        }

        private void SelectTemplateKey(string key)
        {
            var normalized = string.Equals(key, KeyEnum, StringComparison.OrdinalIgnoreCase) ? KeyEnum : KeyText;
            _currentKey = normalized;
            _suspendLink = true;
            TemplateKeyCombo.SelectedItem = normalized;
            _suspendLink = false;
            ApplyTemplateKey(normalized);
        }

        /// <summary>切换 TemplateKey 时即时生效：Enum 显示 Option 并取默认候选；Text 隐藏并清空 Option。</summary>
        private void ApplyTemplateKey(string key)
        {
            if (key == KeyEnum)
            {
                _templateType = 1;
                OptionRow.Visibility = Visibility.Visible;
                if (string.IsNullOrWhiteSpace(OptionCombo.Text))
                {
                    if (!string.IsNullOrWhiteSpace(_enumOptionsText))
                        OptionCombo.Text = _enumOptionsText;
                    else if (_optionChoices.Count > 0)
                        OptionCombo.SelectedIndex = 0;
                }
            }
            else
            {
                _templateType = _textTemplateType;   // Text 固定为 3
                OptionRow.Visibility = Visibility.Collapsed;
                OptionCombo.Text = "";
            }
        }

        private void OnTemplateKeyChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suspendLink) return;
            var key = TemplateKeyCombo.SelectedItem as string;
            if (key == null) return;
            if (_currentKey == KeyEnum && key != KeyEnum)
                _enumOptionsText = OptionCombo.Text ?? "";
            _currentKey = key;
            ApplyTemplateKey(key);
        }

        private void Fill(EcFieldValues values)
        {
            NameBox.Text = values.Name ?? "";
            UnitBox.Text = values.Unit ?? "";
            SetValueBox.Text = values.SetValue ?? "";
            NewValueBox.Text = values.NewValue ?? "";
            DefaultValueBox.Text = values.DefaultValue ?? "";
            MinBox.Text = values.Min ?? "";
            MaxBox.Text = values.Max ?? "";
            DiscriptionBox.Text = values.Discription ?? "";
            PlcLinkBox.Text = values.PLCLink ?? "";
            OptionCombo.Text = string.Join(" | ", values.Options ?? new string[0]);
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            ErrorSummary.Text = "";

            var values = new EcFieldValues
            {
                Name = NameBox.Text.Trim(),
                Unit = UnitBox.Text,
                SetValue = SetValueBox.Text,
                NewValue = NewValueBox.Text,
                DefaultValue = DefaultValueBox.Text,
                Min = MinBox.Text,
                Max = MaxBox.Text,
                Class = _class,
                TemplateType = _templateType,
                Discription = DiscriptionBox.Text,
                PLCLink = PlcLinkBox.Text,
                Options = _currentKey == KeyEnum
                    ? EcConfigService.ParseOptionText(OptionCombo.Text)
                    : new string[0]
            };

            if (_isNew)
            {
                if (string.IsNullOrWhiteSpace(values.Name))
                {
                    ErrorSummary.Text = L.T("ec.dlg.nameRequired");
                    return;
                }

                if (!int.TryParse(CountBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                    || count < 1 || count > 500)
                {
                    ErrorSummary.Text = L.T("ec.dlg.countInvalid");
                    return;
                }

                Count = count;
                Result = values;
                DialogResult = true;
                return;
            }

            // 修改模式：只有勾选的属性参与本次批量写入，未勾选的保持各文件原值
            var applied = new HashSet<string>(StringComparer.Ordinal);
            AddApplied(applied, ChkName, "Name");
            AddApplied(applied, ChkUnit, "Unit");
            AddApplied(applied, ChkPlcLink, "PLCLink");
            AddApplied(applied, ChkTemplateKey, "TemplateType");
            AddApplied(applied, ChkOption, "Option");
            AddApplied(applied, ChkSetValue, "SetValue");
            AddApplied(applied, ChkNewValue, "NewValue");
            AddApplied(applied, ChkDefaultValue, "DefaultValue");
            AddApplied(applied, ChkMin, "Min");
            AddApplied(applied, ChkMax, "Max");
            AddApplied(applied, ChkDiscription, "Discription");

            // TemplateKey 勾选为 Text 时 Option 不显示，统一清空，避免 Text 行残留 Option 数组
            if (applied.Contains("TemplateType") && _currentKey != KeyEnum)
                applied.Add("Option");

            if (applied.Count == 0)
            {
                ErrorSummary.Text = L.T("ec.dlg.noFieldSelected");
                return;
            }

            if (applied.Contains("Name") && string.IsNullOrWhiteSpace(values.Name))
            {
                ErrorSummary.Text = L.T("ec.dlg.nameRequired");
                return;
            }

            values.AppliedFields = applied;
            Result = values;
            DialogResult = true;
        }

        private static void AddApplied(HashSet<string> applied, CheckBox checkBox, string field)
        {
            if (checkBox != null && checkBox.IsChecked == true)
                applied.Add(field);
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
    }
}
