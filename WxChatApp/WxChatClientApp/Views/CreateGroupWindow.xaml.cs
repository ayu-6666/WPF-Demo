using System.Windows;
using System.Windows.Input;
using WxChatApp.Services;
using WxChatApp.ViewModels;

namespace WxChatApp.Views;

public partial class CreateGroupWindow : Window
{
    private readonly ShellViewModel _shell;

    public CreateGroupWindow(ShellViewModel shell)
    {
        InitializeComponent();
        _shell = shell;
        foreach (var contact in shell.Contacts)
        {
            contact.IsSelected = false;
        }
        ContactList.ItemsSource = shell.Contacts;
        Loaded += (_, _) => NameTextBox.Focus();
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        var name = NameTextBox.Text.Trim();
        if (name.Length == 0)
        {
            ErrorText.Text = Loc.T("Group.NameRequired");
            return;
        }
        var ids = _shell.Contacts.Where(c => c.IsSelected).Select(c => c.Id).ToList();
        if (ids.Count == 0)
        {
            ErrorText.Text = Loc.T("Group.MembersRequired");
            return;
        }
        CreateButton.IsEnabled = false;
        var ok = await _shell.CreateGroupAsync(name, ids);
        CreateButton.IsEnabled = true;
        if (ok)
        {
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
