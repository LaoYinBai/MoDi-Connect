using System.Windows.Input;

namespace MoDi.Presentation.Infrastructure;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(
        Func<CancellationToken, Task> execute,
        Func<bool>? canExecute = null) =>
        (_execute, _canExecute) = (execute, canExecute);

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter) => await ExecuteAsync();

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!CanExecute(null))
            return;

        _isRunning = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await _execute(cancellationToken);
        }
        finally
        {
            _isRunning = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, CancellationToken, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(
        Func<T?, CancellationToken, Task> execute,
        Func<T?, bool>? canExecute = null) =>
        (_execute, _canExecute) = (execute, canExecute);

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        var value = Convert(parameter);
        return !_isRunning && (_canExecute?.Invoke(value) ?? true);
    }

    public async void Execute(object? parameter) => await ExecuteAsync(Convert(parameter));

    public async Task ExecuteAsync(T? parameter, CancellationToken cancellationToken = default)
    {
        if (_isRunning || !(_canExecute?.Invoke(parameter) ?? true))
            return;

        _isRunning = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await _execute(parameter, cancellationToken);
        }
        finally
        {
            _isRunning = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? Convert(object? parameter) => parameter is T typed ? typed : default;
}
