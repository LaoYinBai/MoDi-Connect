using System.Windows.Input;

namespace MoDi.Presentation.Infrastructure;

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null) =>
        (_execute, _canExecute) = (execute, canExecute);

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null) =>
        (_execute, _canExecute) = (execute, canExecute);

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(Convert(parameter)) ?? true;
    public void Execute(object? parameter) => _execute(Convert(parameter));
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? Convert(object? parameter) => parameter is T typed ? typed : default;
}
