﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Superheater.Avalonia.Core.ViewModels
{
    internal sealed partial class PopupEditorViewModel : ObservableObject
    {
        private string? _result;
        private SemaphoreSlim? _semaphore;


        #region Binding Properties

        [ObservableProperty]
        private bool _isPopupEditorVisible;

        [ObservableProperty]
        private string _titleText = string.Empty;

        [ObservableProperty]
        private string _text = string.Empty;

        #endregion Binding Properties


        #region Relay Commands

        [RelayCommand]
        private void Cancel()
        {
            _result = null;

            Reset();
        }

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                _result = null;
            }
            else
            {
                _result = Text;
            }

            Reset();
        }

        #endregion Relay Commands


        /// <summary>
        /// Show popup window and return result
        /// </summary>
        /// <returns>true if Ok or Yes pressed, false if Cancel pressed</returns>
        public async Task<string?> ShowAndGetResultAsync(string title, IEnumerable<string>? text)
        {
            var textString = string.Empty;

            if (text is not null)
            {
                textString = string.Join(Environment.NewLine, text);
            }

            TitleText = title;
            Text = textString;
            IsPopupEditorVisible = true;

            _semaphore = new(0);
            await _semaphore.WaitAsync();

            return _result;
        }


        /// <summary>
        /// Show popup window and return result
        /// </summary>
        /// <returns>true if Ok or Yes pressed, false if Cancel pressed</returns>
        public async Task<string?> ShowAndGetResultAsync(string title, string? text)
        {
            TitleText = title;
            Text = text ?? string.Empty;
            IsPopupEditorVisible = true;

            _semaphore = new(0);
            await _semaphore.WaitAsync();

            return _result;
        }

        /// <summary>
        /// Reset popup to its initial state
        /// </summary>
        private void Reset()
        {
            IsPopupEditorVisible = false;

            _semaphore?.Release();
            _semaphore = null;
        }
    }
}
