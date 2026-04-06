using Avalonia.Controls;
using Avalonia.Input;
using SVNFileManager.Models;
using SVNFileManager.ViewModels;

namespace SVNFileManager.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        Closing += (_, _) => _viewModel.Dispose();
    }

    private async void OnFileDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is FileItem item)
        {
            await _viewModel.OpenItemCommand.ExecuteAsync(item);
        }
    }
}
