using CommunityToolkit.Mvvm.ComponentModel;

namespace GenaralDBTools.ViewModels;

public abstract partial class WorkspaceTab : ObservableObject
{
    [ObservableProperty] private string title = "";
    [ObservableProperty] private bool isBusy;
}
