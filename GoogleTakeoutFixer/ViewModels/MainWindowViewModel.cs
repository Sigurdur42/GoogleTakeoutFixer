using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

    private int _progressMax = 0;
    private int _progressValue = 0;
    private bool _showProgressBar;
    private Stopwatch _busyTimer = new();

    public string TimeElapsed => _busyTimer.Elapsed.ToString(@"hh\:mm\:ss");
    public string TimeRemaining { get; set; }

    public bool ShowProgressBar
    {
        get => _showProgressBar;
        set
        {
            if (_showProgressBar == value) return;
            _showProgressBar = value;
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
                
                if (args.FilesTotal > 0)
                {
                    var elapsed = _busyTimer.Elapsed;

                    var remaining = elapsed * args.FilesTotal / args.FilesDone;
                    TimeRemaining = remaining.ToString(@"hh\:mm\:ss");
                    this.RaisePropertyChanged(nameof(TimeElapsed));
                    this.RaisePropertyChanged(nameof(TimeRemaining));
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
            try
            {
                _busyTimer.Restart();
                ShowProgressBar = true;
                _fixGoogleTakeout.Scan(_settings);
            }
            finally
            {
                _busyTimer.Stop();
            }
        });
    }
}