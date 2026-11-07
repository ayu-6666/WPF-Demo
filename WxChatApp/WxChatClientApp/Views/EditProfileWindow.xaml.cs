using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WxChatApp.Helpers;
using WxChatApp.Services;
using WxChatApp.ViewModels;

namespace WxChatApp.Views;

public partial class EditProfileWindow : Window
{
    private readonly ShellViewModel _shell;
    private string? _pendingAvatarUrl; // 本次会话里新上传的头像 URL（save 时一并提交）
    private string? _currentAvatarUrl; // 用户现在已经有的头像 URL（初始值）

    public EditProfileWindow(ShellViewModel shell)
    {
        InitializeComponent();
        _shell = shell;
        NicknameBox.Text = shell.Me?.NickName ?? "";
        SignatureBox.Text = shell.Me?.Signature ?? "";
        WxIdBox.Text = shell.Me?.WxId ?? "";
        PhoneBox.Text = shell.Me?.Phone ?? "";
        _currentAvatarUrl = shell.Me?.Avatar;
        InitAvatarPreview(shell.Me);
        Loaded += (_, _) =>
        {
            NicknameBox.Focus();
            NicknameBox.SelectAll();
        };
    }

    private void InitAvatarPreview(WxChatApp.Models.UserDto? me)
    {
        AvatarInitial.Text = me != null ? AvatarHelper.Initial(me.NickName) : "?";
        AvatarBorder.Background = AvatarHelper.BrushFor(me?.Id ?? "me");
        if (!string.IsNullOrWhiteSpace(me?.Avatar))
        {
            var img = AvatarHelper.FromUrl(me.Avatar);
            if (img != null)
            {
                AvatarImage.Source = img;
                AvatarImage.Visibility = Visibility.Visible;
            }
        }
    }

    private async void ChangeAvatar_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = Loc.T("Profile.AvatarFilter"),
            Title = Loc.T("Profile.ChooseAvatar"),
            Multiselect = false
        };
        if (dlg.ShowDialog(this) != true) return;

        var file = dlg.FileName;
        var fi = new FileInfo(file);
        if (fi.Length > 2 * 1024 * 1024)
        {
            ErrorText.Text = Loc.T("Profile.AvatarTooLarge");
            return;
        }
        ErrorText.Text = "";
        ChangeAvatarButton.IsEnabled = false;
        AvatarHint.Text = Loc.T("Profile.AvatarUploading");
        try
        {
            var url = await AppServices.Api.UploadImageAsync(file);
            _pendingAvatarUrl = url;
            // 立即更新本地预览
            var preview = new BitmapImage();
            preview.BeginInit();
            preview.CacheOption = BitmapCacheOption.OnLoad;
            preview.CreateOptions = BitmapCreateOptions.None;
            preview.UriSource = new Uri(file, UriKind.Absolute);
            preview.EndInit();
            preview.Freeze();
            AvatarImage.Source = preview;
            AvatarImage.Visibility = Visibility.Visible;
            AvatarHint.Text = Loc.T("Profile.AvatarChanged");
        }
        catch (Exception ex)
        {
            ErrorText.Text = Loc.T("Profile.AvatarUploadFail") + ex.Message;
            AvatarHint.Text = Loc.T("Profile.AvatarHint");
        }
        finally
        {
            ChangeAvatarButton.IsEnabled = true;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        var nickname = NicknameBox.Text.Trim();
        if (nickname.Length == 0)
        {
            ErrorText.Text = Loc.T("Profile.NicknameEmpty");
            NicknameBox.Focus();
            return;
        }
        if (nickname.Length > 20)
        {
            ErrorText.Text = Loc.T("Profile.NicknameTooLong");
            NicknameBox.Focus();
            return;
        }
        var signature = SignatureBox.Text.Trim();
        if (signature.Length > 60)
        {
            ErrorText.Text = Loc.T("Profile.SignatureTooLong");
            SignatureBox.Focus();
            return;
        }
        var wxid = WxIdBox.Text.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(wxid, "^[A-Za-z][-A-Za-z0-9_]{5,19}$"))
        {
            ErrorText.Text = Loc.T("Profile.WxIdInvalid");
            WxIdBox.Focus();
            return;
        }
        var phone = PhoneBox.Text.Trim();
        if (phone.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(phone, "^1[3-9]\\d{9}$"))
        {
            ErrorText.Text = Loc.T("Profile.PhoneInvalid");
            PhoneBox.Focus();
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            // 只有用户改了头像才传 avatarUrl
            var avatarArg = _pendingAvatarUrl;
            var ok = await _shell.UpdateProfileAsync(nickname, signature, wxid, phone, avatarArg);
            if (ok)
            {
                CloseDialog(true);
            }
            else
            {
                ErrorText.Text = string.IsNullOrWhiteSpace(_shell.LastProfileError)
                    ? Loc.T("Profile.SaveFailNetwork")
                    : _shell.LastProfileError;
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = Loc.T("Profile.SaveFail") + ex.Message;
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => CloseDialog(false);

    private void Close_Click(object sender, RoutedEventArgs e) => CloseDialog(false);

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseDialog(false);
        }
    }

    /// <summary>ShowDialog 时回填 DialogResult；非模态时直接关闭，避免 InvalidOperationException。</summary>
    private void CloseDialog(bool? result)
    {
        if (result.HasValue)
        {
            try
            {
                DialogResult = result.Value;
                return;
            }
            catch (InvalidOperationException)
            {
                // 非 ShowDialog 模式，退化为 Close
            }
        }
        Close();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
