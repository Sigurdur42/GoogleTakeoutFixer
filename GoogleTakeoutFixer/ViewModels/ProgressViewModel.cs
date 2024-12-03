using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;

namespace GoogleTakeoutFixer.ViewModels;

public class ProgressViewModel : ReactiveObject, IDisposable
{
    private int _currentValue;
    private bool _hasChanged;
    private bool _isCancelled;
    private int _maxValue;

    private string _message = "";
    private Stopwatch _timer = new();

    public ProgressViewModel()
    {
        Task.Run(() =>
        {
            while (!_isCancelled)
            {
                Thread.Sleep(500);
                if (!_hasChanged) continue;
                _hasChanged = false;
                this.RaisePropertyChanged(nameof(MaxValue));
                this.RaisePropertyChanged(nameof(CurrentValue));
                this.RaisePropertyChanged(nameof(Message));
                this.RaisePropertyChanged(nameof(Elapsed));
                this.RaisePropertyChanged(nameof(Remaining));
            }
        });
    }

    public void StartTimer() => _timer = Stopwatch.StartNew();
    public void StopTimer() => _timer.Stop();

    public int MaxValue
    {
        get => _maxValue;
        set
        {
            if (_maxValue == value) return;
            _maxValue = value;
            _hasChanged = true;
        }
    }

    public string Elapsed { get; private set; } = "";
    public string Remaining { get; private set; } = "";

    public int CurrentValue
    {
        get => _currentValue;
        set
        {
            if (_currentValue == value) return;
            _currentValue = value;
            Message = $"{CurrentValue} / {MaxValue}";
            var elapsed = _timer.Elapsed;
            if (CurrentValue == MaxValue && MaxValue > 0)
            {
                StopTimer();
                Remaining = "";
            }
            else
            {
                
                var timePerItem = elapsed / (_currentValue > 0 ? _currentValue : 1);
                var totalTime = timePerItem * _maxValue;
                var remaining = totalTime - elapsed;
                Remaining = remaining.ToString(@"hh\:mm\:ss");
            }

            Elapsed = elapsed.ToString(@"hh\:mm\:ss");
            
           
            _hasChanged = true;
        }
    }

    public string Message
    {
        get => _message;
        set
        {
            if (_message == value) return;
            _message = value;
            _hasChanged = true;
        }
    }

    public void Dispose()
    {
        StopUpdateTask();
    }

    private void StopUpdateTask()
    {
        _isCancelled = true;
    }

    public void Reset()
    {
        CurrentValue = 0;
        MaxValue = 0;

        _hasChanged = true;
    }
}