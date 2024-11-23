using GoogleTakeoutFixer.Controller;

namespace GoogleTakeoutFixer.ViewModels;

public class ProgressViewModel
{
    public ProgressViewModel(ProgressEventArgs args)
    {
        Message = args.CurrentAction;
        IsError = args.IsError;
    }

    public bool IsError { get; set; }

    public string Message { get; set; }
}