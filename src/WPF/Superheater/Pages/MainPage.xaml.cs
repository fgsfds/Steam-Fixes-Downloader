﻿using Superheater.ViewModels;
using Common.DI;
using System.Windows.Controls;

namespace Superheater.Pages
{
    /// <summary>
    /// Interaction logic for MainForm.xaml
    /// </summary>
    public sealed partial class MainPage : UserControl
    {
        private readonly MainViewModel _mvm;

        public MainPage()
        {
            _mvm = BindingsManager.Instance.GetInstance<MainViewModel>();

            DataContext = _mvm;

            InitializeComponent();

            _mvm.InitializeCommand.Execute(null);
        }
    }
}
