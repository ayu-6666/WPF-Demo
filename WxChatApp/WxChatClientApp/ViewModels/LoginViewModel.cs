using CommunityToolkit.Mvvm.ComponentModel;
using WxChatApp.Models;
using WxChatApp.Services;

namespace WxChatApp.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty] private string _account = "";
    [ObservableProperty] private string _nickname = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isRegisterMode;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _rememberAccount = true;

    public event Action<AuthResult>? LoginSucceeded;

    public string SubmitText => IsRegisterMode ? Loc.T("Login.Register") : Loc.T("Login.Submit");
    public string SwitchText => IsRegisterMode ? Loc.T("Login.SwitchToLogin") : Loc.T("Login.SwitchToRegister");
    public string SubtitleText => IsRegisterMode ? Loc.T("Login.SubtitleRegister") : Loc.T("Login.Subtitle");
    public string ServerHint => Loc.T("Login.ServerHint", AppConfig.ServerBaseUrl);

    public LoginViewModel()
    {
        RememberAccount = LocalStore.Settings.RememberAccount;
        Account = LocalStore.Settings.LastAccount;
    }

    partial void OnIsRegisterModeChanged(bool value)
    {
        ErrorMessage = "";
        OnPropertyChanged(nameof(SubmitText));
        OnPropertyChanged(nameof(SwitchText));
        OnPropertyChanged(nameof(SubtitleText));
    }

    public void ToggleMode()
    {
        IsRegisterMode = !IsRegisterMode;
    }

    public async Task SubmitAsync(string password)
    {
        if (IsBusy)
        {
            return;
        }
        ErrorMessage = "";
        var account = Account.Trim();
        if (account.Length == 0)
        {
            ErrorMessage = Loc.T("Login.ErrorAccount");
            return;
        }
        if (password.Length == 0)
        {
            ErrorMessage = Loc.T("Login.ErrorPassword");
            return;
        }
        if (IsRegisterMode && Nickname.Trim().Length == 0)
        {
            ErrorMessage = Loc.T("Login.ErrorNickname");
            return;
        }

        IsBusy = true;
        try
        {
            AuthResult result;
            if (IsRegisterMode)
            {
                result = await AppServices.Api.RegisterAsync(account, password, Nickname.Trim());
            }
            else
            {
                result = await AppServices.Api.LoginAsync(account, password);
            }

            LocalStore.Settings.LastAccount = account;
            LocalStore.Settings.RememberAccount = RememberAccount;
            LocalStore.Save();

            LoginSucceeded?.Invoke(result);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            ErrorMessage = Loc.T("Login.ErrorServer");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
