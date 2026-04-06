using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SVNFileManager.Models;
using SVNFileManager.ViewModels;

namespace SVNFileManager.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private ListBox? _fileListBox;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        Loaded += async (_, _) =>
        {
            _fileListBox = this.FindControl<ListBox>("FileListBox");
            await _viewModel!.InitializeAsync();
        };
        Closing += (_, _) => _viewModel?.Dispose();
    }

    private void OnListBoxDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (_fileListBox == null) return;

        var item = _fileListBox.SelectedItem as FileItem;
        if (item != null && _viewModel != null)
        {
            _viewModel.OpenItem(item);
        }
    }
}
