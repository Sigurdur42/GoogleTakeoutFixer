using System;

namespace GoogleTakeoutFixer.Controller;

public class ProgressEventArgs : EventArgs
{
    public required string CurrentAction { get; set; }
    public int FilesDone { get; set; }
    public int FilesTotal { get; set; }
    public bool IsError { get; set; }
}