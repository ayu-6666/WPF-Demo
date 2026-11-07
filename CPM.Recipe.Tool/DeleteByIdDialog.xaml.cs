using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CPM.Recipe.Tool.Services;

namespace CPM.Recipe.Tool
{
    /// <summary>删除对话框：可从当前文件的 ID 列表选择，也可手工输入。</summary>
    public partial class DeleteByIdDialog : Window
    {
        public long Id { get; private set; } = -1;

        public DeleteByIdDialog(IEnumerable<long> knownIds, long? preselected)
        {
            InitializeComponent();

            Title = L.T("dlg.delete.title");
            TitleText.Text = Title;
            LabelText.Text = L.T("dlg.delete.label");
            HintText.Text = L.T("dlg.delete.hint");
            CancelBtn.Content = L.T("dlg.delete.cancel");
            OkBtn.Content = L.T("dlg.delete.ok");
            CloseBtn.ToolTip = L.T("tip.close");

            var ids = (knownIds ?? Enumerable.Empty<long>())
                .Distinct()
                .OrderBy(x => x)
                .Select(x => x.ToString(CultureInfo.InvariantCulture))
                .ToList();

            IdCombo.ItemsSource = ids;
            IdCombo.ToolTip = L.T("dlg.delete.suggest");
            if (preselected.HasValue)
                IdCombo.Text = preselected.Value.ToString(CultureInfo.InvariantCulture);
            else if (ids.Count > 0)
                IdCombo.Text = ids[0];

            Loaded += (s, e) => { IdCombo.Focus(); };
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            long id;
            if (!long.TryParse((IdCombo.Text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
            {
                MessageDialog.Warning(this, L.T("msg.deleteInvalidId"), L.T("msg.title.warning"));
                IdCombo.Focus();
                return;
            }

            Id = id;
            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
    }
}
