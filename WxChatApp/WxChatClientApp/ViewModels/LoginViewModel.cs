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
    [ObservableProperty] private string _serverAddress = AppConfig.ServerBaseUrl;

    public event Action<AuthResult>? LoginSucceeded;

    public string SubmitText => IsRegisterMode ? Loc.T("Login.Register") : Loc.T("Login.Submit");
    public string SwitchText => IsRegisterMode ? Loc.T("Login.SwitchToLogin") : Loc.T("Login.SwitchToRegister");
    public string SubtitleText => IsRegisterMode ? Loc.T("Login.SubtitleRegister") : Loc.T("Login.Subtitle");
    public bool CanEditServer => !AppConfig.IsServerUrlLocked;

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
        if (!AppConfig.TryNormalizeServerUrl(ServerAddress, out var serverUrl, out var serverError))
        {
            ErrorMessage = serverError;
            return;
        }
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

        // 登录前先按输入框切换服务端，保证跨机器部署时也能连上
        AppConfig.SaveServerBaseUrl(serverUrl);
        ServerAddress = serverUrl;

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
        catch (Exception ex)
        {
            ErrorMessage = Loc.T("Login.ErrorConnect", AppConfig.ServerBaseUrl, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
