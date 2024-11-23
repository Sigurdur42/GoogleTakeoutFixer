namespace GoogleTakeoutFixer.Models;

public interface ILocalSettings
{
    string InputFolder { get; set; }
    string OutputFolder { get; set; }
    bool ScanOnly { get; set; }
    int NumberOfLinesShown { get; set; }
}