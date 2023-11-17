using Avalonia.Input.Platform;
using Common;
using Common.Config;
using Common.Entities.CombinedEntities;
using Common.Entities.Fixes;
using Common.Entities.Fixes.FileFix;
using Common.Helpers;
using Common.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Superheater.Avalonia.Core.Helpers;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Superheater.Avalonia.Core.ViewModels
{
    internal sealed partial class MainViewModel : ObservableObject
    {
        public MainViewModel(
            MainModel mainModel,
            ConfigProvider config
            )
        {
            _mainModel = mainModel ?? ThrowHelper.ArgumentNullException<MainModel>(nameof(mainModel));
            _config = config?.Config ?? ThrowHelper.ArgumentNullException<ConfigEntity>(nameof(config));

            MainTabHeader = "Main";
            LaunchGameButtonText = "Launch game...";
            _searchBarText = string.Empty;

            SelectedTagFilter = TagsComboboxList.First();

            _config.NotifyParameterChanged += NotifyParameterChanged;
        }

        private readonly MainModel _mainModel;
        private readonly ConfigEntity _config;
        private readonly SemaphoreSlim _locker = new(1, 1);
        private bool _lockButtons;


        #region Binding Properties

        public ImmutableList<FixFirstCombinedEntity> FilteredGamesList => _mainModel.GetFilteredGamesList(SearchBarText, SelectedTagFilter);

        public ImmutableList<BaseFixEntity>? SelectedGameFixesList => SelectedGame is null ? [] : SelectedGame.FixesList.Fixes.Where(x => !x.IsHidden).ToImmutableList();

        public ImmutableList<string>? SelectedFixTags => SelectedFix?.Tags?.Where(x => !_config.HiddenTags.Contains(x)).ToImmutableList();

        public HashSet<string> TagsComboboxList => _mainModel.GetListOfTags();

        public ImmutableList<string>? SelectedFixVariants
        {
            get
            {
                if (SelectedFix is not FileFixEntity fileFix) { return null; }

                return fileFix.Variants?.ToImmutableList();
            }
        }


        public static bool IsSteamGameMode => CommonProperties.IsInSteamDeckGameMode;

        public static bool IsTagsComboboxVisible => true;

        public bool DoesSelectedFixHaveVariants => SelectedFixVariants is not null && !SelectedFixVariants.IsEmpty;

        public bool DoesSelectedFixHaveUpdates => SelectedFix?.HasNewerVersion ?? false;


        public string MainTabHeader { get; private set; }

        public string LaunchGameButtonText { get; private set; }

        private string SelectedFixUrl => _mainModel.GetSelectedFixUrl(SelectedFix);

        public string SelectedFixRequirements => GetRequirementsString();


        public bool DoesSelectedGameRequireAdmin => SelectedGame?.Game is not null && SelectedGame.Game.DoesRequireAdmin();

        public bool SelectedFixHasTags => SelectedFixTags is not null && !SelectedFixTags.IsEmpty;


        public float ProgressBarValue { get; private set; }


        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedGameFixesList))]
        [NotifyPropertyChangedFor(nameof(DoesSelectedGameRequireAdmin))]
        [NotifyCanExecuteChangedFor(nameof(LaunchGameCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenGameFolderCommand))]
        [NotifyCanExecuteChangedFor(nameof(ApplyAdminCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenPCGamingWikiCommand))]
        private FixFirstCombinedEntity? _selectedGame;
        partial void OnSelectedGameChanged(FixFirstCombinedEntity? value)
        {
            if (value?.Game is not null &&
                value.Game.DoesRequireAdmin())
            {
                RequireAdmin();
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedFixRequirements))]
        [NotifyPropertyChangedFor(nameof(DoesSelectedFixHaveUpdates))]
        [NotifyPropertyChangedFor(nameof(SelectedFixVariants))]
        [NotifyPropertyChangedFor(nameof(DoesSelectedFixHaveVariants))]
        [NotifyPropertyChangedFor(nameof(SelectedFixUrl))]
        [NotifyPropertyChangedFor(nameof(SelectedFixTags))]
        [NotifyPropertyChangedFor(nameof(SelectedFixHasTags))]
        [NotifyCanExecuteChangedFor(nameof(InstallFixCommand))]
        [NotifyCanExecuteChangedFor(nameof(UninstallFixCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenConfigCommand))]
        [NotifyCanExecuteChangedFor(nameof(UpdateFixCommand))]
        private BaseFixEntity? _selectedFix;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(InstallFixCommand))]
        private string? _selectedFixVariant;

        [ObservableProperty]
        private string _selectedTagFilter;
        partial void OnSelectedTagFilterChanged(string value) => FillGamesList();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ClearSearchCommand))]
        private string _searchBarText;
        partial void OnSearchBarTextChanged(string value) => FillGamesList();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UpdateGamesCommand))]
        private bool _isInProgress;
        partial void OnIsInProgressChanged(bool value) => _lockButtons = value;

        #endregion Binding Properties


        #region Relay Commands

        /// <summary>
        /// VM initialization
        /// </summary>
        [RelayCommand]
        private async Task InitializeAsync() => await UpdateAsync(true);


        /// <summary>
        /// Update games list
        /// </summary>
        [RelayCommand(CanExecute = (nameof(UpdateGamesCanExecute)))]
        private async Task UpdateGames()
        {
            await UpdateAsync(false);

            InstallFixCommand.NotifyCanExecuteChanged();
            UninstallFixCommand.NotifyCanExecuteChanged();
            UpdateFixCommand.NotifyCanExecuteChanged();
        }
        private bool UpdateGamesCanExecute() => !_lockButtons;


        /// <summary>
        /// Install selected fix
        /// </summary>
        [RelayCommand(CanExecute = (nameof(InstallFixCanExecute)))]
        private async Task InstallFix() => await InstallUpdateFixAsync(false);
        private bool InstallFixCanExecute()
        {
            if (SelectedGame is null ||
                SelectedFix is null ||
                SelectedFix.IsInstalled ||
                !SelectedGame.IsGameInstalled ||
                (DoesSelectedFixHaveVariants && SelectedFixVariant is null) ||
                _lockButtons)
            {
                return false;
            }

            var result = !_mainModel.DoesFixHaveNotInstalledDependencies(SelectedGame, SelectedFix);

            return result;
        }


        /// <summary>
        /// Update selected fix
        /// </summary>
        [RelayCommand(CanExecute = (nameof(UpdateFixCanExecute)))]
        private async Task UpdateFix() => await InstallUpdateFixAsync(true);
        public bool UpdateFixCanExecute() => (SelectedGame is not null && SelectedGame.IsGameInstalled) || !_lockButtons;


        /// <summary>
        /// Uninstall selected fix
        /// </summary>
        [RelayCommand(CanExecute = (nameof(UninstallFixCanExecute)))]
        private void UninstallFix()
        {
            if (SelectedFix is null) ThrowHelper.NullReferenceException(nameof(SelectedFix));
            if (SelectedGame?.Game is null) ThrowHelper.NullReferenceException(nameof(SelectedGame.Game));

            IsInProgress = true;

            UpdateGamesCommand.NotifyCanExecuteChanged();

            var fixUninstallResult = _mainModel.UninstallFix(SelectedGame.Game, SelectedFix);

            FillGamesList();

            IsInProgress = false;

            InstallFixCommand.NotifyCanExecuteChanged();
            UninstallFixCommand.NotifyCanExecuteChanged();
            OpenConfigCommand.NotifyCanExecuteChanged();
            UpdateGamesCommand.NotifyCanExecuteChanged();

            new PopupMessageViewModel(
                fixUninstallResult.IsSuccess ? "Success" : "Error",
                fixUninstallResult.Message,
                PopupMessageType.OkOnly)
                .Show();
        }
        private bool UninstallFixCanExecute()
        {
            if (SelectedFix is null ||
                !SelectedFix.IsInstalled ||
                SelectedGameFixesList is null ||
                (SelectedGame is not null && !SelectedGame.IsGameInstalled) ||
                _lockButtons)
            {
                return false;
            }

            var result = !MainModel.DoesFixHaveInstalledDependentFixes(SelectedGameFixesList, SelectedFix.Guid);

            return result;
        }


        /// <summary>
        /// Open selected game install folder
        /// </summary>
        [RelayCommand(CanExecute = (nameof(OpenGameFolderCanExecute)))]
        private void OpenGameFolder()
        {
            if (SelectedGame?.Game is null) ThrowHelper.NullReferenceException(nameof(SelectedGame.Game));

            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedGame.Game.InstallDir,
                UseShellExecute = true
            });
        }
        private bool OpenGameFolderCanExecute() => SelectedGame is not null && SelectedGame.IsGameInstalled;


        /// <summary>
        /// Clear search bar
        /// </summary>
        [RelayCommand(CanExecute = (nameof(ClearSearchCanExecute)))]
        private void ClearSearch() => SearchBarText = string.Empty;
        private bool ClearSearchCanExecute() => !string.IsNullOrEmpty(SearchBarText);


        /// <summary>
        /// Open config file for selected fix
        /// </summary>
        [RelayCommand(CanExecute = (nameof(OpenConfigCanExecute)))]
        private void OpenConfig() => OpenConfigXml();
        private bool OpenConfigCanExecute() => SelectedFix is FileFixEntity fileFix && fileFix.ConfigFile is not null && fileFix.IsInstalled && (SelectedGame is not null && SelectedGame.IsGameInstalled);


        /// <summary>
        /// Apply admin rights for selected game
        /// </summary>
        [RelayCommand(CanExecute = (nameof(ApplyAdminCanExecute)))]
        private void ApplyAdmin()
        {
            if (SelectedGame?.Game is null) ThrowHelper.NullReferenceException(nameof(SelectedGame.Game));
            SelectedGame.Game.SetRunAsAdmin();
            OnPropertyChanged(nameof(DoesSelectedGameRequireAdmin));
        }
        private bool ApplyAdminCanExecute() => DoesSelectedGameRequireAdmin;


        /// <summary>
        /// Open PCGW page for selected game
        /// </summary>
        [RelayCommand(CanExecute = (nameof(OpenPCGamingWikiCanExecute)))]
        private void OpenPCGamingWiki()
        {
            if (SelectedGame is null) ThrowHelper.NullReferenceException(nameof(SelectedGame));

            Process.Start(new ProcessStartInfo
            {
                FileName = Consts.PCGamingWikiUrl + SelectedGame.GameId,
                UseShellExecute = true
            });
        }
        private bool OpenPCGamingWikiCanExecute() => SelectedGame is not null;


        /// <summary>
        /// Copy file URL to clipboard
        /// </summary>
        [RelayCommand]
        private async Task UrlCopyToClipboardAsync()
        {
            var clipboard = Properties.TopLevel.Clipboard ?? ThrowHelper.ArgumentNullException<IClipboard>("Error while getting clipboard implementation");
            await clipboard.SetTextAsync(SelectedFixUrl);
        }


        /// <summary>
        /// Launch/install game
        /// </summary>
        [RelayCommand(CanExecute = (nameof(LaunchGameCanExecute)))]
        private void LaunchGame()
        {
            if (SelectedGame is null) ThrowHelper.NullReferenceException(nameof(SelectedGame));

            if (SelectedGame.IsGameInstalled)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"steam://rungameid/{SelectedGame.GameId}",
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"steam://install/{SelectedGame.GameId}",
                    UseShellExecute = true
                });
            }
        }
        private bool LaunchGameCanExecute()
        {
            if (SelectedGame is null)
            {
                return false;
            }

            if (SelectedGame.IsGameInstalled)
            {
                LaunchGameButtonText = "Launch game...";
            }
            else
            {
                LaunchGameButtonText = "Install game...";
            }

            OnPropertyChanged(nameof(LaunchGameButtonText));
            return true;
        }


        /// <summary>
        /// Close app
        /// </summary>
        [RelayCommand]
        private static void CloseApp() => Properties.MainWindow.Close();


        /// <summary>
        /// Hide selected tag
        /// </summary>
        [RelayCommand]
        private void HideTag(string value)
        {
            var tags = _config.HiddenTags.ToList();
            tags.Add(value);
            tags = tags.OrderBy(x => x).ToList();

            _config.HiddenTags = tags;
        }

        #endregion Relay Commands


        /// <summary>
        /// Install or update fix
        /// </summary>
        /// <param name="isUpdate">Update fix</param>
        private async Task InstallUpdateFixAsync(bool isUpdate)
        {
            if (SelectedGame?.Game is null) ThrowHelper.NullReferenceException(nameof(SelectedGame.Game));
            if (SelectedFix is null) ThrowHelper.NullReferenceException(nameof(SelectedFix));

            _lockButtons = true;

            UpdateGamesCommand.NotifyCanExecuteChanged();
            InstallFixCommand.NotifyCanExecuteChanged();
            UpdateFixCommand.NotifyCanExecuteChanged();
            UninstallFixCommand.NotifyCanExecuteChanged();
            OpenConfigCommand.NotifyCanExecuteChanged();

            FileTools.Progress.ProgressChanged += Progress_ProgressChanged;

            Result result;

            if (isUpdate)
            {
                result = await _mainModel.UpdateFixAsync(SelectedGame.Game, SelectedFix, SelectedFixVariant, false);
            }
            else
            {
                result = await _mainModel.InstallFixAsync(SelectedGame.Game, SelectedFix, SelectedFixVariant, false);
            }

            if (result.ResultEnum is ResultEnum.MD5Error)
            {
                var popupResult = await new PopupMessageViewModel(
                    "Warning",
                    @"MD5 of the file doesn't match the database. This file wasn't verified by the maintainer.

Do you still want to install the fix?",
                    PopupMessageType.YesNo)
                    .ShowAndGetResultAsync();

                if (popupResult)
                {
                    result = await _mainModel.InstallFixAsync(SelectedGame.Game, SelectedFix, SelectedFixVariant, true);
                }
            }

            FillGamesList();

            _lockButtons = false;

            UninstallFixCommand.NotifyCanExecuteChanged();
            OpenConfigCommand.NotifyCanExecuteChanged();
            UpdateGamesCommand.NotifyCanExecuteChanged();

            FileTools.Progress.ProgressChanged -= Progress_ProgressChanged;
            ProgressBarValue = 0;
            OnPropertyChanged(nameof(ProgressBarValue));

            if (!result.IsSuccess)
            {
                new PopupMessageViewModel(
                    "Error",
                    result.Message,
                    PopupMessageType.OkOnly)
                .Show();

                return;
            }

            if (SelectedFix is FileFixEntity fileFix &&
                fileFix.ConfigFile is not null &&
                _config.OpenConfigAfterInstall)
            {
                new PopupMessageViewModel(
                    "Success",
                    result.Message + Environment.NewLine + Environment.NewLine + "Open config file?",
                    PopupMessageType.YesNo,
                    OpenConfigXml)
                .Show();
            }
            else
            {
                new PopupMessageViewModel(
                    "Success",
                    result.Message,
                    PopupMessageType.OkOnly)
                .Show();
            }
        }

        /// <summary>
        /// Update games list
        /// </summary>
        /// <param name="useCache">Use cached list</param>
        private async Task UpdateAsync(bool useCache)
        {
            await _locker.WaitAsync();
            IsInProgress = true;

            var result = await _mainModel.UpdateGamesListAsync(useCache);

            FillGamesList();

            if (!result.IsSuccess)
            {
                new PopupMessageViewModel(
                    "Error",
                    result.Message,
                    PopupMessageType.OkOnly
                    ).Show();
            }

            IsInProgress = false;

            _locker.Release();
        }

        /// <summary>
        /// Update tab header
        /// </summary>
        private void UpdateHeader()
        {
            MainTabHeader = "Main" + (_mainModel.HasUpdateableGames
                ? $" ({_mainModel.UpdateableGamesCount} {(_mainModel.UpdateableGamesCount < 2
                    ? "update"
                    : "updates")})"
                : string.Empty);

            OnPropertyChanged(nameof(MainTabHeader));
        }

        /// <summary>
        /// Fill games and available games lists based on a search bar
        /// </summary>
        private void FillGamesList()
        {
            var selectedGameId = SelectedGame?.GameId;
            var selectedFixGuid = SelectedFix?.Guid;

            OnPropertyChanged(nameof(FilteredGamesList));
            OnPropertyChanged(nameof(TagsComboboxList));

            UpdateHeader();

            if (selectedGameId is not null && FilteredGamesList.Any(x => x.GameId == selectedGameId))
            {
                SelectedGame = FilteredGamesList.First(x => x.GameId == selectedGameId);

                if (selectedFixGuid is not null &&
                    SelectedGameFixesList is not null &&
                    SelectedGameFixesList.Any(x => x.Guid == selectedFixGuid))
                {
                    SelectedFix = SelectedGameFixesList.First(x => x.Guid == selectedFixGuid);
                }
            }
        }

        /// <summary>
        /// Show popup with admin right requirement
        /// </summary>
        /// <exception cref="NullReferenceException"></exception>
        private void RequireAdmin()
        {
            if (SelectedGame?.Game is null) ThrowHelper.NullReferenceException(nameof(SelectedGame.Game));

            new PopupMessageViewModel(
                "Admin privileges required",
                @"This game requires to be run as admin in order to work.

Do you want to set it to always run as admin?",
                PopupMessageType.YesNo,
                SelectedGame.Game.SetRunAsAdmin)
                .Show();

            OnPropertyChanged(nameof(DoesSelectedGameRequireAdmin));
        }

        /// <summary>
        /// Open config file for selected fix
        /// </summary>
        private void OpenConfigXml()
        {
            if (SelectedFix is not FileFixEntity fileFix) { return; }
            if (SelectedGame?.Game is null) ThrowHelper.NullReferenceException(nameof(SelectedGame.Game));
            if (fileFix.ConfigFile is null) ThrowHelper.NullReferenceException(nameof(fileFix.ConfigFile));

            var pathToConfig = Path.Combine(SelectedGame.Game.InstallDir, fileFix.ConfigFile);

            var workingDir = fileFix.ConfigFile.EndsWith(".exe") ? Path.GetDirectoryName(pathToConfig) : Directory.GetCurrentDirectory();

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(pathToConfig),
                UseShellExecute = true,
                WorkingDirectory = workingDir
            });
        }

        /// <summary>
        /// Get requirements for selected fix
        /// </summary>
        private string GetRequirementsString()
        {
            if (SelectedGameFixesList is null ||
                SelectedFix is null ||
                SelectedGame is null)
            {
                return string.Empty;
            }

            var dependsOn = _mainModel.GetDependenciesForAFix(SelectedGame, SelectedFix);

            string? requires = null;

            if (dependsOn.Count != 0)
            {
                requires = "REQUIRES: ";

                requires += string.Join(", ", dependsOn.Select(x => x.Name));
            }

            string? required = null;

            if (SelectedFix?.Dependencies is not null)
            {
                var dependsBy = MainModel.GetDependentFixes(SelectedGameFixesList, SelectedFix.Guid);

                if (dependsBy.Count != 0)
                {
                    required = "REQUIRED BY: ";

                    required += string.Join(", ", dependsBy.Select(x => x.Name));
                }
            }

            if (requires is not null && required is not null)
            {
                return requires + Environment.NewLine + required;
            }
            else if (requires is not null)
            {
                return requires;
            }
            else if (required is not null)
            {
                return required;
            }

            return string.Empty;
        }

        private void Progress_ProgressChanged(object? sender, float e)
        {
            ProgressBarValue = e;
            OnPropertyChanged(nameof(ProgressBarValue));
        }

        private async void NotifyParameterChanged(string parameterName)
        {
            if (parameterName.Equals(nameof(_config.ShowUninstalledGames)))
            {
                await UpdateAsync(true);
            }

            if (parameterName.Equals(nameof(_config.ShowUnsupportedFixes)) ||
                parameterName.Equals(nameof(_config.HiddenTags)))
            {
                await UpdateAsync(true);
                OnPropertyChanged(nameof(SelectedGameFixesList));
                OnPropertyChanged(nameof(SelectedFixTags));
            }

            if (parameterName.Equals(nameof(_config.UseTestRepoBranch)) ||
                parameterName.Equals(nameof(_config.UseLocalRepo)) ||
                parameterName.Equals(nameof(_config.LocalRepoPath)))
            {
                await UpdateAsync(false);
            }
        }
    }
}