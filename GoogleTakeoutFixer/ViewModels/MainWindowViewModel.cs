using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using Config.Net;
using GoogleTakeoutFixer.Controller;
using GoogleTakeoutFixer.Models;
using ReactiveUI;

namespace GoogleTakeoutFixer.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ILocalSettings _settings;
    private readonly FixGoogleTakeout _fixGoogleTakeout = new();

    public string SourceFolder
    {
        get => _settings.InputFolder;
        set
        {
            if (_settings?.InputFolder == value) return;
            _settings!.InputFolder = value;
            this.RaisePropertyChanged();
        }
    }

    public string TargetFolder
    {
        get => _settings.OutputFolder;
        set
        {
            if (_settings?.OutputFolder == value) return;
            _settings!.OutputFolder = value;
            this.RaisePropertyChanged();
        }
    }

    public bool ScanOnly
    {
        get => _settings.ScanOnly;
        set
        {
            if (_settings?.ScanOnly == value) return;
            _settings!.ScanOnly = value;
            this.RaisePropertyChanged();
        }
    }

    public int NumberOfLinesShown
    {
        get => _settings.NumberOfLinesShown;
        set
        {
            if (_settings?.NumberOfLinesShown == value) return;
            _settings!.NumberOfLinesShown = value;
            this.RaisePropertyChanged();
        }
    }

    public ObservableCollection<ProgressViewModel> ProgressViewModels { get; } = [];

    public MainWindowViewModel()
    {
        var special = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = Path.Combine(special, "GoogleTakeoutFixer", "GoogleTakeoutFixerSettings.json");
        _settings = new ConfigurationBuilder<ILocalSettings>()
            .UseJsonFile(path)
            .Build();

        if (_settings.NumberOfLinesShown < 10)
        {
            _settings.NumberOfLinesShown = 20;
        }

        _fixGoogleTakeout.ProgressChanged += (sender, args) =>
        {
            var item = new ProgressViewModel(args);
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProgressViewModels.Add(item);
                while (ProgressViewModels.Count >= _settings.NumberOfLinesShown)
                {
                    ProgressViewModels.RemoveAt(0);
                }
            });
        };
    }

    public void StartScan()
    {
        ProgressViewModels.Clear();
        Task.Run(() =>
        {
            // TODO: Lock UI while running
            _fixGoogleTakeout.Scan(_settings);
        });
    }
}