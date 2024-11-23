using System;
using System.IO;
using GoogleTakeoutFixer.Models;

namespace GoogleTakeoutFixer.Controller;

public class FixGoogleTakeout
{
    private readonly ProgressEventArgs _progress = new()
    {
        CurrentAction = "",
    };

    public event EventHandler<ProgressEventArgs>? ProgressChanged;

    public void Scan(ILocalSettings settings)
    {
        ScanInputFolder(settings.InputFolder);
    }

    private void ScanInputFolder(string inputFolder)
    {
        _progress.CurrentAction = "Scanning input folder...";
        InvokeProgress();

        if (!Directory.Exists(inputFolder))
        {
            _progress.IsError = true;
            _progress.CurrentAction = "Input folder does not exist.";
            InvokeProgress();
            return;
        }
        
        // TODO: Continue
    }

    private void InvokeProgress()
        => ProgressChanged?.Invoke(this, _progress);
}