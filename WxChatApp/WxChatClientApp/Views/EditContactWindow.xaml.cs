using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WxChatApp.Services;
using WxChatApp.ViewModels;

namespace WxChatApp.Views;

/// <summary>编辑好友备注/标签/分组，保存时真实写入后端并回读校验，失败保持窗口并提示。</summary>
public partial class EditContactWindow : Window
{
    private readonly ContactItemVm _contact;
    private readonly ShellViewModel _shell;
    private readonly List<string> _tags = new();

    public EditContactWindow(ContactItemVm contact, ShellViewModel shell)
    {
        InitializeComponent();
        _contact = contact;
        _shell = shell;
        RemarkBox.Text = contact.Remark;
        foreach (var tag in SplitTags(contact.Tag))
        {
            _tags.Add(tag);
        }
        RenderTags();

        var groups = shell.Contacts
            .Select(c => c.Group)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!string.IsNullOrWhiteSpace(contact.Group) &&
            !groups.Contains(contact.Group, StringComparer.OrdinalIgnoreCase))
        {
            groups.Add(contact.Group);
        }
        GroupCombo.ItemsSource = groups;
        GroupCombo.Text = contact.Group;
        Loaded += (_, _) => RemarkBox.Focus();
    }

    private static IEnumerable<string> SplitTags(string tag) =>
        (tag ?? "").Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0);

    private void RenderTags()
    {
        TagPanel.Children.Clear();
        foreach (var tag in _tags)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock
            {
                Text = tag,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("WxTextBrush")
            });
            var remove = new Button
            {
                Content = "\u00D7",
                FontSize = 12,
                Width = 18,
                Height = 18,
                Margin = new Thickness(4, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = Loc.T("Contact.TagRemove")
            };
            remove.Click += (_, _) =>
            {
                _tags.Remove(tag);
                RenderTags();
            };
            panel.Children.Add(remove);
            var chip = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xED, 0xED, 0xED)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 4, 2),
                Margin = new Thickness(0, 0, 6, 6),
                Child = panel
            };
            TagPanel.Children.Add(chip);
        }
    }

    private void AddTag_Click(object sender, RoutedEventArgs e) => AddTag();

    private void TagInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            AddTag();
        }
    }

    private void AddTag()
    {
        ErrorText.Text = "";
        var tag = TagInputBox.Text.Trim();
        if (tag.Length == 0)
        {
            return;
        }
        if (tag.Length > 20)
        {
            ErrorText.Text = Loc.T("Contact.TagTooLong");
            return;
        }
        if (_tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            TagInputBox.Text = "";
            return;
        }
        if (_tags.Count >= 5 || string.Join(",", _tags.Append(tag)).Length > 60)
        {
            ErrorText.Text = Loc.T("Contact.TagsTooMany");
            return;
        }
        _tags.Add(tag);
        TagInputBox.Text = "";
        RenderTags();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        var remark = RemarkBox.Text.Trim();
        var group = (GroupCombo.Text ?? "").Trim();
        var tag = string.Join(",", _tags);
        if (remark.Length > 30)
        {
            ErrorText.Text = Loc.T("Contact.ProfileFieldTooLong");
            return;
        }
        if (group.Length > 20)
        {
            ErrorText.Text = Loc.T("Contact.ProfileFieldTooLong");
            return;
        }
        SaveButton.IsEnabled = false;
        try
        {
            if (await _shell.UpdateContactProfileAsync(_contact.Id, remark, tag, group))
            {
                DialogResult = true;
                return;
            }
            ErrorText.Text = Loc.T("Contact.ProfileUpdateFail", "");
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }
}
