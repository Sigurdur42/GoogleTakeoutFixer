using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly ExifToolWrapper _exiftool = new();

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
    public event EventHandler? ProgressDone;

    public async Task Scan(ILocalSettings settings)
    {
        try
        {
            await _exiftool.DetectExifTool();

            _cancelRunning = false;
            _data.Clear();
            _progress.FilesDone = 0;
            await ScanInputFolder(settings.InputFolder);

            if (settings.ScanOnly)
            {
                return;
            }

            CopyAndProcess(settings);

            InvokeProgress("Done.");
            ProgressDone?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception error)
        {
            InvokeError(error.Message + Environment.NewLine + "Aborting now.");
        }
    }

    private void CopyAndProcess(ILocalSettings settings)
    {
        var outputFolder = settings.OutputFolder;
        if (!Directory.Exists(outputFolder))
        {
            _progress.CurrentAction = $"Creating output folder: {outputFolder}.";
            InvokeProgress();
            Directory.Exists(outputFolder);
        }

        _progress.FilesTotal = _data.Count;

        var maxDegreeOfParallelism = Environment.ProcessorCount / 2;
        Parallel.ForEachAsync(_data,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism  },
            async (file, token) =>
            {
                if (_cancelRunning)
                {
                    return;
                }

                await CopyAndUpdateSingleFile(settings, file);
            }).Wait();
    }

    private async Task CopyAndUpdateSingleFile(ILocalSettings settings, ImageData data)
    {
        var targetImagePart = data.Image.Substring(
            settings.InputFolder.Length,
            data.Image.Length - settings.InputFolder.Length);
        var targetFile = new FileInfo(Path.Combine(settings.OutputFolder, targetImagePart));

        try
        {
            if (!targetFile.Directory!.Exists)
            {
                InvokeProgress($"Creating output folder: {targetFile.Directory}.");
                targetFile.Directory.Create();
            }

            var stopwatch = Stopwatch.StartNew();
            File.Copy(data.Image, targetFile.FullName, true);
            var copyElapsed = stopwatch.Elapsed;
            if (!string.IsNullOrWhiteSpace(data.JsonData) && File.Exists(data.JsonData))
            {
                var arguments = new List<string>();
                arguments.Add("-dateFormat");
                arguments.Add("%s");
                arguments.Add("-tagsfromfile");
                arguments.Add(data.JsonData);
                arguments.Add("-DateTimeOriginal<PhotoTakenTimeTimestamp");
                // arguments.Add("-FileCreateDate<PhotoTakenTimeTimestamp");
                arguments.Add("-FileModifyDate<PhotoTakenTimeTimestamp");
                arguments.Add("-overwrite_original");

                // Handle potentially incorrect maker notes
                // See https://exiftool.org/faq.html#Q15
                arguments.Add("-F");

                // Last argument has to be the file
                arguments.Add(targetFile.FullName);

                var result = await _exiftool.RunExifToolAsync(arguments.ToArray());

                var exifElapsed = stopwatch.Elapsed;
                stopwatch.Stop();

                var parts = new List<string>
                {
                    $"({copyElapsed}) Copied and updated EXIF of {targetFile.FullName}.",
                    $"Exit Code: {result.ExitCode}, Elapsed Time: {result.ExecutionTime}"
                };

                if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    parts.Add($"StdOut: {result.StandardOutput}");
                }

                if (!string.IsNullOrWhiteSpace(result.StandardError))
                {
                    parts.Add($"StdOut: {result.StandardError}");
                }

                InvokeFileDone(string.Join(Environment.NewLine, parts));
            }
            else
            {
                InvokeFileDone($"([{copyElapsed}) Copied {targetFile.FullName}.");
            }
        }
        catch (Exception e)
        {
            InvokeError($"Failed to copy image: {data.Image} to {targetFile} ({e.Message})");
        }
    }

    private async Task ScanInputFolder(string inputFolder)
    {
        InvokeProgress("Scanning input folder...");

        if (!Directory.Exists(inputFolder))
        {
            InvokeError("Input folder does not exist.");
            return;
        }

        await LookForFolder(inputFolder);

        var total = _data.Count;
        var missingJson = _data.Where(x => string.IsNullOrWhiteSpace(x.JsonData)).ToArray();
        var photos = _data.Count(x => x.IsPhoto);

        InvokeProgress($"{total} files ({missingJson.Length} json files missing, {photos} photos, {total - photos} videos).");
    }

    private async Task LookForFolder(string inputFolder)
    {
        // Look for files in this folder
        InvokeProgress($"Scanning <{inputFolder}>...");

        var files = Directory.GetFiles(inputFolder);
        var analysed = AssignData(files);
        _data.AddRange(analysed);

        InvokeProgress($"Found {analysed.Count} in <{inputFolder}>...");

        var folders = Directory.GetDirectories(inputFolder);
        foreach (var folder in folders)
        {
            await LookForFolder(folder);
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
            var ignoreFile = false;
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
                case ".3gp":
                case ".gif":
                case ".avi":
                    isPhoto = false;
                    break;

                case ".json":
                    ignoreFile = true;
                    // ignore json here
                    break;

                default:
                    InvokeProgress($"Found invalid extension <{file.Extension}> in file <{file.FullName}>.");
                    continue;
            }

            if (ignoreFile)
            {
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

    private void InvokeProgress(string? message = null)
    {
        var progress = new ProgressEventArgs()
        {
            CurrentAction = message ?? _progress.CurrentAction,
            IsError = false,
            FilesDone = _progress.FilesDone,
            FilesTotal = _progress.FilesTotal,
        };
        ProgressChanged?.Invoke(this, progress);
    }

    private void InvokeFileDone(string message)
    {
        _progress.FilesDone += 1;
        var progress = new ProgressEventArgs()
        {
            CurrentAction = message,
            IsError = false,
            FilesDone = _progress.FilesDone,
            FilesTotal = _progress.FilesTotal,
        };
        ProgressChanged?.Invoke(this, progress);
    }

    private void InvokeError(string message)
    {
        var progress = new ProgressEventArgs()
        {
            CurrentAction = message,
            IsError = true,
            FilesDone = _progress.FilesDone,
            FilesTotal = _progress.FilesTotal,
        };
        ProgressChanged?.Invoke(this, progress);
    }
}