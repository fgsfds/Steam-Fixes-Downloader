﻿using Superheater.DI;
using Common.Config;
using Common.DI;
using System;
using System.Windows;

namespace Superheater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly ConfigEntity _config;

        public MainWindow()
        {
            Dispatcher.UnhandledException += OnDispatcherUnhandledException;

            var container = BindingsManager.Instance;
            container.Options.EnableAutoVerification = false;

            ModelsBindings.Load(container);
            ViewModelsBindings.Load(container);
            CommonBindings.Load(container);
            ProvidersBindings.Load(container);

            _config = container.GetInstance<ConfigProvider>().Config;
            _config.NotifyParameterChanged += OnUseLocalRepoChanged;

            UpdateHeader();

            InitializeComponent();

            void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                string errorMessage = $"An unhandled exception occurred:{Environment.NewLine}{e.Exception.Message}";

                if (e.Exception.InnerException is not null)
                {
                    errorMessage += Environment.NewLine;
                    errorMessage += e.Exception.InnerException.Message;
                }

                MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);

                e.Handled = true;

                Environment.Exit(-1);
            }
        }

        private void OnUseLocalRepoChanged(string parameterName)
        {
            if (parameterName.Equals(nameof(ConfigEntity.UseLocalRepo)))
            {
                UpdateHeader();
            }
        }

        private void UpdateHeader()
        {
            if (_config.UseLocalRepo)
            {
                Title = "Steam Superheater (local repository)";
            }
            else
            {
                Title = "Steam Superheater";
            }
        }
    }
}
