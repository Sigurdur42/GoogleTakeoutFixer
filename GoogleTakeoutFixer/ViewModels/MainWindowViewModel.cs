using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Config.Net;
using GoogleTakeoutFixer.Controller;
using GoogleTakeoutFixer.Models;
using ReactiveUI;

namespace GoogleTakeoutFixer.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ILocalSettings _settings;
    private readonly FixGoogleTakeout _fixGoogleTakeout = new();

    private bool _isProcessing;
    private readonly Stopwatch _busyTimer = new();
    private string _itemsProgress = "0/0 Files";

    public ProgressViewModel FileCopyProgress { get; } = new();
    public ProgressViewModel UpdateExifProgress { get; } = new();

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

    public bool OverWriteExistingInCopy
    {
        get => _settings.OverWriteExistingInCopy;
        set
        {
            if (_settings?.OverWriteExistingInCopy == value) return;
            _settings!.OverWriteExistingInCopy = value;
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


    public List<string> ProgressMessages { get; } = [];
    public ObservableCollection<string> ProgressErrors { get; } = [];

    public MainWindowViewModel()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        Title = $"Papsi's Google Takeout Fixer V{version}";

        var special = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = Path.Combine(special, "GoogleTakeoutFixer", "GoogleTakeoutFixerSettings.json");
        _settings = new ConfigurationBuilder<ILocalSettings>()
            .UseJsonFile(path)
            .Build();

        if (_settings.NumberOfLinesShown < 10000)
        {
            _settings.NumberOfLinesShown = 20000;
        }

        _fixGoogleTakeout.ReportFileCopied += (_, args) => { FileCopyProgress.CurrentValue += 1; };

        _fixGoogleTakeout.ReportExifUpdated += (_, args) => { UpdateExifProgress.CurrentValue += 1; };

        _fixGoogleTakeout.ReportFileCount += (_, args) => { FileCopyProgress.MaxValue = args; };

        _fixGoogleTakeout.ReportExifCount += (_, args) => UpdateExifProgress.MaxValue = args;

        _fixGoogleTakeout.ItemError += (_, args) => { ProgressErrors.Add(args); };

        _fixGoogleTakeout.ItemProgress += (_, message) => { ProgressMessages.Add(message); };

        _fixGoogleTakeout.ReportAllDOne += (_, args) =>
        {
            IsProcessing = false;
            _busyTimer.Stop();
        };
    }

    public void StartScan()
    {
        ProgressMessages.Clear();
        ProgressErrors.Clear();

        FileCopyProgress.Reset();
        UpdateExifProgress.Reset();

        FileCopyProgress.StartTimer();
        UpdateExifProgress.StartTimer();

        IsProcessing = true;
        _busyTimer.Restart();

        Task.Run(async () => { await _fixGoogleTakeout.Scan(_settings); });
    }

    public void CancelProcessing()
    {
        _fixGoogleTakeout.CancelRunning();

        FileCopyProgress.StopTimer();
        UpdateExifProgress.StopTimer();
    }

    public void Dispose()
    {
        FileCopyProgress.Dispose();
        UpdateExifProgress.Dispose();
    }
}