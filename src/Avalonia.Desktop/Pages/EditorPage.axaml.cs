using Avalonia.Controls;
using Avalonia.Desktop.ViewModels.Editor;
using Common.Client.DI;
using Microsoft.Extensions.DependencyInjection;

namespace Avalonia.Desktop.Pages;

public sealed partial class EditorPage : UserControl
{
    public EditorPage()
    {
        var vm = BindingsManager.Provider.GetRequiredService<EditorViewModel>();

        DataContext = vm;

        InitializeComponent();
    }
}

