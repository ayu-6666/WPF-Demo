using System.Windows;
using System.Windows.Input;
using WxChatApp.Services;

namespace WxChatApp.Views;

public partial class InputDialog : Window
{
    public InputDialog(string title, string prompt, string placeholder = "", string initial = "")
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        PromptText.Text = prompt;
        ValueTextBox.Tag = placeholder;
        ValueTextBox.Text = initial;
        Loaded += (_, _) =>
        {
            ValueTextBox.Focus();
            if (initial.Length > 0) ValueTextBox.SelectAll();
        };
    }

    public string Value { get; private set; } = "";

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var text = ValueTextBox.Text.Trim();
        if (text.Length == 0)
        {
            ErrorText.Text = Loc.T("Dialog.EmptyInput");
            return;
        }
        Value = text;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Value_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Ok_Click(sender, e);
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
