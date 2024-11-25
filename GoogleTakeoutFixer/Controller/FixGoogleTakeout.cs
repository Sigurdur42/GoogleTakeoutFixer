using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GoogleTakeoutFixer.Models;
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


    public event EventHandler<ProgressEventArgs>? ProgressChanged;

    public void Scan(ILocalSettings settings)
    {
        try
        {
            _data.Clear();
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

        var index = 0;
        _progress.FilesTotal = _data.Count;
        foreach (var file in _data)
        {
            ++index;
            _progress.FilesDone = index;
            _progress.CurrentAction = $"Updating file {file.Image}...";
            InvokeProgress();

            CopyAndUpdateSingleFile(settings, file);
        }
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

            File.Copy(data.Image, targetFile.FullName, true);

            if (!string.IsNullOrWhiteSpace(data.JsonData) && File.Exists(data.JsonData))
            {
                var result = exiftool.Execute(
                    "-dateFormat",
                    "%s",
                    " -tagsfromfile",
                    data.JsonData,
                    "-DateTimeOriginal<PhotoTakenTimeTimestamp",
                    // "-FileCreateDate<PhotoTakenTimeTimestamp",
                    "-FileModifyDate<PhotoTakenTimeTimestamp" ,
                    "-overwrite_original",
                    
                    // Handle potentially incorrect maker notes
                    // See https://exiftool.org/faq.html#Q15
                    "-F",
                    
                    // Last argument has to be the file
                    targetFile.FullName
                );
                _progress.CurrentAction = $"({result}) Updated EXIF of {targetFile.FullName}.";
                InvokeProgress();
            }
        }
        catch (Exception e)
        {
            _progress.CurrentAction = $"Failed to copy image: {data.Image} to {targetFile} ({e.Message})";
            InvokeError();
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