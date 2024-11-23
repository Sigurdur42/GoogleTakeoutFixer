using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GoogleTakeoutFixer.ViewModels;
using GoogleTakeoutFixer.Views;

namespace GoogleTakeoutFixer;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
                ViewModel = viewModel,
                ShowInTaskbar = true,
                ShowActivated = true,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}