using Avalonia.Controls;
using Common.DI;
using Microsoft.Extensions.DependencyInjection;
using Superheater.Avalonia.Core.ViewModels;
using Superheater.Avalonia.Core.ViewModels.Popups;

namespace Superheater.Avalonia.Core.UserControls
{
    public sealed partial class PopupMessage : UserControl
    {
        public PopupMessage()
        {
            var vm = BindingsManager.Provider.GetRequiredService<PopupMessageViewModel>();

            DataContext = vm;

            InitializeComponent();
        }
    }
}
