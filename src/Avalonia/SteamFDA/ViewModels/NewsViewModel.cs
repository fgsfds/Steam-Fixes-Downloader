﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamFDCommon.Models;
using SteamFDCommon.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SteamFDA.ViewModels
{
    public partial class NewsViewModel : ObservableObject
    {
        private readonly NewsModel _newsModel;

        public List<NewsEntity> News => _newsModel.News;

        public string NewsTabHeader { get; private set; } = "News";

        public NewsViewModel(NewsModel newsModel)
        {
            _newsModel = newsModel;
        }


        #region Relay Commands

        [RelayCommand]
        async Task InitializeAsync() => await UpdateAsync();

        [RelayCommand(CanExecute=(nameof(MarkAllAsReadCanExecute)))]
        private void MarkAllAsRead()
        {
            _newsModel.MarkAllAsRead();
            UpdateHeader();
            OnPropertyChanged(nameof(News));
            OnPropertyChanged(nameof(NewsTabHeader));
        }
        private bool MarkAllAsReadCanExecute() => _newsModel.HasUnreadNews;

        #endregion Relay Commands


        private async Task UpdateAsync()
        {
            try
            {
                await _newsModel.UpdateNewsListAsync();
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                new PopupMessageViewModel(
                    "Error",
                    "File not found: " + ex.Message,
                    PopupMessageType.OkOnly
                    ).Show();

                return;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                new PopupMessageViewModel(
                    "Error",
                    "Can't connect to GitHub repository",
                    PopupMessageType.OkOnly
                    ).Show();

                return;
            }

            UpdateHeader();
        }

        private void UpdateHeader()
        {
            NewsTabHeader = "News" + (_newsModel.HasUnreadNews ? $" ({_newsModel.UnreadNewsCount} unread)" : string.Empty);

            OnPropertyChanged(nameof(NewsTabHeader));

            MarkAllAsReadCommand.NotifyCanExecuteChanged();
        }
    }
}
