using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GoogleTakeoutFixer.ViewModels;

namespace GoogleTakeoutFixer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    internal MainWindowViewModel ViewModel { get; init; } = null!;

    private async Task<string?> OpenDirectoryPicker(string pickerTitle, string initialDirectory)
    {
        var storageProvider = StorageProvider;

        // Create an OpenFilePickerOptions instance
        var options = new FolderPickerOpenOptions()
        {
            AllowMultiple = false,
            Title = pickerTitle,
            
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(initialDirectory),
        };

        var result = await storageProvider.OpenFolderPickerAsync(options);
        if (!result.Any())
        {
            return null;
        }

        var selectedFile = result[0];
        return Uri.UnescapeDataString(selectedFile.Path.AbsolutePath);
    }

    private async void OnBrowseInputFolder(object? sender, RoutedEventArgs e)
    {
        var folder = await OpenDirectoryPicker("Select Google Takeout Folder", ViewModel.SourceFolder);
        if (folder != null)
        {
            Dispatcher.UIThread.Post(() => ViewModel.SourceFolder = folder);
        }
    }

    private async void OnBrowseOutputFolder(object? sender, RoutedEventArgs e)
    {
        var folder = await OpenDirectoryPicker("Select Target Folder", ViewModel.TargetFolder);
        if (folder != null)
        {
            Dispatcher.UIThread.Post(() => ViewModel.TargetFolder = folder);
        }
    }

    private void OnStartScan(object? sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.StartScan();
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            
        }
    }
}