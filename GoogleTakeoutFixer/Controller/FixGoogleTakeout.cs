using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GoogleTakeoutFixer.Models;
using Microsoft.VisualBasic.FileIO;
using SharpExifTool;

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

    private bool _cancelRunning = false;

    public void CancelRunning()
    {
        _cancelRunning = true;
        _progress.CurrentAction = "Canceling...";
        InvokeProgress();
    }


    public event EventHandler<ProgressEventArgs>? ProgressChanged;

    public void Scan(ILocalSettings settings)
    {
        try
        {
            _cancelRunning = false;
            _data.Clear();
            _progress.FilesDone = 0;
            ScanInputFolder(settings.InputFolder);

            if (settings.ScanOnly)
            {
                return;
            }

            CopyAndProcess(settings);
        }
        finally
        {
            _progress.CurrentAction = "Done.";
            InvokeProgress();
        }
    }

    private void CopyAndProcess(ILocalSettings settings)
    {
        using var exiftool = new SharpExifTool.ExifTool();

        var outputFolder = settings.OutputFolder;
        if (!Directory.Exists(outputFolder))
        {
            _progress.CurrentAction = $"Creating output folder: {outputFolder}.";
            InvokeProgress();
            Directory.Exists(outputFolder);
        }

        _progress.FilesTotal = _data.Count;

        Parallel.ForEach(_data, file =>
        {
            if (_cancelRunning)
            {
                return;
            }

            CopyAndUpdateSingleFile(settings, file);
        });
    }

    private void CopyAndUpdateSingleFile(ILocalSettings settings, ImageData data)
    {
        using var exiftool = new SharpExifTool.ExifTool();

        var targetImagePart = data.Image.Substring(
            settings.InputFolder.Length,
            data.Image.Length - settings.InputFolder.Length);
        var targetFile = new FileInfo(Path.Combine(settings.OutputFolder, targetImagePart));

        try
        {
            if (!targetFile.Directory!.Exists)
            {
                _progress.CurrentAction = $"Creating output folder: {targetFile.Directory}.";
                InvokeProgress();
                targetFile.Directory.Create();
            }

            var stopwatch = Stopwatch.StartNew();
            File.Copy(data.Image, targetFile.FullName, true);
            var copyElapsed = stopwatch.Elapsed;
            if (!string.IsNullOrWhiteSpace(data.JsonData) && File.Exists(data.JsonData))
            {
                var result = exiftool.Execute(
                    "-dateFormat",
                    "%s",
                    " -tagsfromfile",
                    data.JsonData,
                    "-DateTimeOriginal<PhotoTakenTimeTimestamp",
                    // "-FileCreateDate<PhotoTakenTimeTimestamp",
                    "-FileModifyDate<PhotoTakenTimeTimestamp",
                    "-overwrite_original",

                    // Handle potentially incorrect maker notes
                    // See https://exiftool.org/faq.html#Q15
                    "-F",

                    // Last argument has to be the file
                    targetFile.FullName
                );

                var exifElapsed = stopwatch.Elapsed;
                stopwatch.Stop();
                _progress.CurrentAction =
                    $"({result}[{copyElapsed}|{exifElapsed - copyElapsed}]) Copied and updated EXIF of {targetFile.FullName}.";
            }
            else
            {
                _progress.CurrentAction = $"([{copyElapsed}) Copied {targetFile.FullName}.";
            }
        }
        catch (Exception e)
        {
            _progress.CurrentAction = $"Failed to copy image: {data.Image} to {targetFile} ({e.Message})";
            InvokeError();
        }
        finally
        {
            InvokeFileDone();
        }
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
                case ".jpeg":
                case ".png":
                case ".dng":
                case ".cr2":
                    isPhoto = true;
                    break;

                case ".mov":
                case ".mp4":
                case ".mpg":
                case ".avi":
                    isPhoto = false;
                    break;
                
                case ".json":
                    // ignore json here
                    break;

                default:
                    _progress.CurrentAction = $"Found invalid extension <{file.Extension}> in file <{file.FullName}>.";
                    InvokeProgress();
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
            IsError = false,
            FilesDone = _progress.FilesDone,
            FilesTotal = _progress.FilesTotal,
        };
        ProgressChanged?.Invoke(this, progress);
    }

    private void InvokeFileDone()
    {
        _progress.FilesDone += 1;
        var progress = new ProgressEventArgs()
        {
            CurrentAction = _progress.CurrentAction,
            IsError = false,
            FilesDone = _progress.FilesDone,
            FilesTotal = _progress.FilesTotal,
        };
        ProgressChanged?.Invoke(this, progress);
    }

    private void InvokeError()
    {
        var progress = new ProgressEventArgs()
        {
            CurrentAction = _progress.CurrentAction,
            IsError = true,
            FilesDone = _progress.FilesDone,
            FilesTotal = _progress.FilesTotal,
        };
        ProgressChanged?.Invoke(this, progress);
    }
}