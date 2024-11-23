using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GoogleTakeoutFixer.Models;

namespace GoogleTakeoutFixer.Controller;

public class ImageData
{
    public required string Image { get; set; }
    public string? JsonData { get; set; }
    public bool IsPhoto { get; set; }
}

public class FixGoogleTakeout
{
    private readonly ProgressEventArgs _progress = new()
    {
        CurrentAction = "",
    };

    private readonly List<ImageData> _data = [];

    public event EventHandler<ProgressEventArgs>? ProgressChanged;

    public void Scan(ILocalSettings settings)
    {
        _data.Clear();
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

        LookForFolder(inputFolder);

        var total = _data.Count;
        var missingJson = _data.Where(x => string.IsNullOrWhiteSpace(x.JsonData)).ToArray();
        var photos = _data.Count(x => x.IsPhoto);

        _progress.CurrentAction =
            $"{total} files ({missingJson.Length} json files missing, {photos} photos, {total - photos} videos).";
        InvokeProgress();

        _progress.CurrentAction = "Done.";
        InvokeProgress();
    }

    private void LookForFolder(string inputFolder)
    {
        // Look for files in this folder
        _progress.CurrentAction = $"Scanning <{inputFolder}>...";
        InvokeProgress();

        var files = Directory.GetFiles(inputFolder);
        var analysed = AssignData(files);
        _data.AddRange(analysed);

        _progress.CurrentAction = $"Found {analysed.Count} in <{inputFolder}>...";
        InvokeProgress();

        var folders = Directory.GetDirectories(inputFolder);
        foreach (var folder in folders)
        {
            LookForFolder(folder);
        }
    }

    private List<ImageData> AssignData(string[] files)
    {
        var edited = new[]
        {
            "-bearbeitet.",
            "-edited.",
            "_bearbeitet.",
            "_edited.",
        };

        var extension = new[]
        {
            ".json",
            ".JSON",
        };

        var result = new List<ImageData>();
        var indexed = files.ToDictionary(s => s);
        foreach (var file in files.Select(f => new FileInfo(f)))
        {
            var isPhoto = true;
            switch (file.Extension.ToLowerInvariant())
            {
                case ".jpg":
                case ".png":
                    isPhoto = true;
                    break;

                case ".mov":
                case ".mp4":
                case ".avi":
                    isPhoto = false;
                    break;

                default:
                    continue;
            }

            string? foundDataFile = null;
            foreach (var ext in extension)
            {
                foreach (var edit in edited)
                {
                    var normalizedFile = file.FullName.Replace(edit, ".");
                    var noExtension = Path.GetFileNameWithoutExtension(normalizedFile);
                    if (noExtension.EndsWith(")"))
                    {
                        var normalizedInfo = new FileInfo(normalizedFile);
                        // 20220602_105500(1).jpg
                        // 20220602_105500.jpg(1).json
                        var opening = normalizedFile.LastIndexOf("(", StringComparison.InvariantCultureIgnoreCase);
                        var closing = normalizedFile.LastIndexOf(")", StringComparison.InvariantCultureIgnoreCase);
                        var bracket = normalizedFile.Substring(opening, (closing - opening) + 1);
                        normalizedFile = normalizedFile.Substring(0, opening)
                                         + normalizedInfo.Extension
                                         + bracket;
                    }

                    var dataFile = normalizedFile + ext;

                    indexed.TryGetValue(dataFile, out foundDataFile);
                    if (foundDataFile != null)
                    {
                        break;
                    }
                }

                if (foundDataFile != null)
                {
                    break;
                }
            }


            result.Add(new ImageData()
            {
                Image = file.FullName,
                JsonData = foundDataFile,
                IsPhoto = isPhoto
            });
        }

        return result;
    }

    private void InvokeProgress()
    {
        var progress = new ProgressEventArgs()
        {
            CurrentAction = _progress.CurrentAction,
            IsError = _progress.IsError,
            FilesDone = _progress.FilesDone,
            FilesTotal = _progress.FilesTotal,
        };
        ProgressChanged?.Invoke(this, progress);
    }
}