using Avalonia.Desktop.ViewModels.Popups;
using Common;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.Desktop.ViewModels;

internal sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IConfigProvider _config;


    #region Binding Properties

    public string RepositoryMessage =>
        _config.UseLocalApiAndRepo
            ? "Local API"
            : "Online API";

    [ObservableProperty]
    private PopupMessageViewModel? _popupDataContext;

    #endregion Binding Properties


    public MainWindowViewModel(IConfigProvider configProvider)
    {
        _config = configProvider;

        _config.ParameterChangedEvent += OnParameterChangedEvent;

        OnPropertyChanged(nameof(RepositoryMessage));
    }

    private void OnParameterChangedEvent(string parameterName)
    {
        if (parameterName.Equals(nameof(_config.UseLocalApiAndRepo)) ||
            parameterName.Equals(nameof(_config.LocalRepoPath)))
        {
            OnPropertyChanged(nameof(RepositoryMessage));
        }
    }
}

