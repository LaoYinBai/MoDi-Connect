using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MoDi.Presentation.Theming;

namespace MoDi.Presentation.Shell;

public partial class AppShellView : UserControl
{
    private AppShellViewModel? _viewModel;

    public AppShellView()
    {
        InitializeComponent();
        TopBar.MinimizeRequested += (_, _) => MinimizeRequested?.Invoke(this, EventArgs.Empty);
        TopBar.CloseRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        TopBar.DragRequested += (_, args) => DragRequested?.Invoke(this, args);
        DataContextChanged += OnDataContextChanged;
        BindViewModel();
    }

    public event EventHandler? MinimizeRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler<PointerPressedEventArgs>? DragRequested;

    private void OnDataContextChanged(object? sender, EventArgs eventArgs) => BindViewModel();

    private void BindViewModel()
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as AppShellViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyAppearance(_viewModel.Appearance);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(AppShellViewModel.Appearance) && _viewModel is not null)
            ApplyAppearance(_viewModel.Appearance);
    }

    private static void ApplyAppearance(MoDi.App.Contracts.AppearanceSnapshot appearance)
    {
        if (Application.Current is not null)
            AppearanceResourceApplicator.Apply(Application.Current, appearance);
    }
}
