using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Common;
using Common.Helpers;
using System;
using System.Threading.Tasks;

namespace Superheater.Avalonia.Core.ViewModels
{
    internal sealed partial class AboutViewModel : ObservableObject
    {
        private readonly UpdateInstaller _updateInstaller;

        public string AboutTabHeader { get; private set; }

        public bool IsUpdateAvailable { get; set; }

        public bool IsAutoUpdateAvailable => OSEnumHelper.GetCurrentOS() is OSEnum.Windows && !IsUpdateAvailable;

        public bool IsInProgress { get; set; }

        public string CheckForUpdatesText { get; set; }

        public Version CurrentVersion => CommonProperties.CurrentVersion;

        public AboutViewModel(UpdateInstaller updateInstaller)
        {
            _updateInstaller = updateInstaller ?? throw new NullReferenceException(nameof(updateInstaller));
            AboutTabHeader = "About";
            CheckForUpdatesText = string.Empty;
        }


        #region Relay Commands

        /// <summary>
        /// VM initialization
        /// </summary>
        [RelayCommand]
        private async Task InitializeAsync() => await CheckForUpdates();

        /// <summary>
        /// Check for SSH updates
        /// </summary>
        [RelayCommand(CanExecute = (nameof(CheckForUpdatesCanExecute)))]
        private async Task CheckForUpdates()
        {
            if (!IsAutoUpdateAvailable)
            {
                return;
            }

            IsInProgress = true;
            OnPropertyChanged(nameof(IsInProgress));
            CheckForUpdatesCommand.NotifyCanExecuteChanged();

            bool updates = false;

            try
            {
                CheckForUpdatesText = "Checking...";
                OnPropertyChanged(nameof(CheckForUpdatesText));
                updates = await _updateInstaller.CheckForUpdates(CurrentVersion);
            }
            catch (Exception ex)
            {
                new PopupMessageViewModel(
                    "Error",
                    @$"Cannot retrieve latest releases from GitHub:
                    
{ex.Message}",
                    PopupMessageType.OkOnly
                    ).Show();
            }

            if (updates)
            {
                IsUpdateAvailable = true;
                OnPropertyChanged(nameof(IsUpdateAvailable));
                DownloadAndInstallCommand.NotifyCanExecuteChanged();

                UpdateHeader();
            }
            else
            {
                CheckForUpdatesText = "Already up-to-date";
                OnPropertyChanged(nameof(CheckForUpdatesText));
            }

            IsInProgress = false;
            OnPropertyChanged(nameof(IsInProgress));
            CheckForUpdatesCommand.NotifyCanExecuteChanged();
        }
        private bool CheckForUpdatesCanExecute() => IsInProgress is false;

        /// <summary>
        /// Download and install SSH update
        /// </summary>
        [RelayCommand(CanExecute = (nameof(DownloadAndInstallCanExecute)))]
        private async Task DownloadAndInstall()
        {
            IsInProgress = true;
            OnPropertyChanged(nameof(IsInProgress));
            DownloadAndInstallCommand.NotifyCanExecuteChanged();

            await _updateInstaller.DownloadAndUnpackLatestRelease();

            UpdateInstaller.InstallUpdate();
        }
        private bool DownloadAndInstallCanExecute() => IsUpdateAvailable is true;

        #endregion Relay Commands

        /// <summary>
        /// Update tab header
        /// </summary>
        private void UpdateHeader()
        {
            AboutTabHeader = "About" + (IsUpdateAvailable
                ? " (Update available)"
                : string.Empty);

            OnPropertyChanged(nameof(AboutTabHeader));
        }
    }
}