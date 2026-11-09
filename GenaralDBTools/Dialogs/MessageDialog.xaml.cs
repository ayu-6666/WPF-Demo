using System.Windows;
using System.Windows.Media;

namespace GenaralDBTools.Dialogs;

public enum MessageDialogKind { Info, Warning, Error, Confirm }

public partial class MessageDialog : Window
{
    public bool Confirmed { get; private set; }

    public MessageDialog(string title, string message, string? details, MessageDialogKind kind, bool confirm, string confirmText = "确定")
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;

        var (icon, color) = kind switch
        {
            MessageDialogKind.Info => ("i", "#FF2563EB"),
            MessageDialogKind.Warning => ("!", "#FFD97706"),
            _ => ("!", "#FFDC2626")
        };
        IconText.Text = icon;
        IconBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));

        if (string.IsNullOrWhiteSpace(details))
        {
            DetailExpander.Visibility = Visibility.Collapsed;
        }
        else
        {
            DetailsBox.Text = details;
        }

        if (confirm)
        {
            OkButton.Visibility = Visibility.Collapsed;
            ConfirmButton.Visibility = Visibility.Visible;
            ConfirmButton.Content = confirmText;
            CancelButton.Visibility = Visibility.Visible;
        }
        else
        {
            OkButton.Visibility = Visibility.Visible;
            ConfirmButton.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
    }
}
