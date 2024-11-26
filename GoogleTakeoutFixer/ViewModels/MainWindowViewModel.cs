using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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

    private int _progressMax = 0;
    private int _progressValue = 0;
    private bool _isProcessing;
    private readonly Stopwatch _busyTimer = new();
    private string _itemsProgress = "0/0 Files";

    public string TimeElapsed => _busyTimer.Elapsed.ToString(@"hh\:mm\:ss");
    public string TimeRemaining { get; set; } = "Coming soon :)";
    public string Title { get; init; }

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (_isProcessing == value) return;
            _isProcessing = value;
            this.RaisePropertyChanged();
        }
    }

    public string ItemsProgress
    {
        get => _itemsProgress;
        set
        {
            if (_itemsProgress == value) return;
            _itemsProgress = value;
            this.RaisePropertyChanged();
        }
    }

    public int ProgressMax
    {
        get => _progressMax;
        set
        {
            if (_progressMax == value) return;
            _progressMax = value;
            this.RaisePropertyChanged();
        }
    }

    public int ProgressValue
    {
        get => _progressValue;
        set
        {
            if (_progressValue == value) return;
            _progressValue = value;
            this.RaisePropertyChanged();
        }
    }

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
            if (_settings.ScanOnly == value) return;
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
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        Title = $"Papsi's Google Takeout Fixer V{version}";

        var special = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = Path.Combine(special, "GoogleTakeoutFixer", "GoogleTakeoutFixerSettings.json");
        _settings = new ConfigurationBuilder<ILocalSettings>()
            .UseJsonFile(path)
            .Build();

        if (_settings.NumberOfLinesShown < 1000)
        {
            _settings.NumberOfLinesShown = 2000;
        }

        _fixGoogleTakeout.ProgressChanged += (sender, args) =>
        {
            var item = new ProgressViewModel(args);
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                while (ProgressViewModels.Count >= _settings.NumberOfLinesShown)
                {
                    ProgressViewModels.Clear();
                }

                ProgressViewModels.Insert(0, item);
                ProgressMax = args.FilesTotal;
                ProgressValue = args.FilesDone;

                if (args is not { FilesTotal: > 0, FilesDone: > 0 })
                {
                    return;
                }

                var elapsed = _busyTimer.Elapsed;

                var remaining = elapsed * args.FilesTotal / args.FilesDone;
                TimeRemaining = remaining.ToString(@"hh\:mm\:ss");
                this.RaisePropertyChanged(nameof(TimeElapsed));
                this.RaisePropertyChanged(nameof(TimeRemaining));

                ItemsProgress = $"{args.FilesDone}/{args.FilesTotal} Files";
            });
        };
    }

    public void StartScan()
    {
        ProgressViewModels.Clear();
        Task.Run(() =>
        {
            // TODO: Lock UI while running
            try
            {
                _busyTimer.Restart();
                IsProcessing = true;
                _fixGoogleTakeout.Scan(_settings);
            }
            finally
            {
                IsProcessing = false;
                _busyTimer.Stop();
            }
        });
    }

    public void CancelProcessing()
    {
        _fixGoogleTakeout.CancelRunning();
    }
}