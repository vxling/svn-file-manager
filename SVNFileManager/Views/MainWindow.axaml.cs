using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SVNFileManager.Models;
using SVNFileManager.ViewModels;
using System.Diagnostics;

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
        Debug.WriteLine("[DEBUG] ======= DoubleTap Fired =======");
        Debug.WriteLine($"[DEBUG] sender: {sender?.GetType().Name ?? "null"}");
        if (_fileListBox == null)
        {
            Debug.WriteLine("[DEBUG] _fileListBox is null - returning");
            return;
        }

        var item = _fileListBox.SelectedItem as FileItem;
        Debug.WriteLine($"[DEBUG] SelectedItem: {(item != null ? $"{item.Name} (IsDir={item.IsDirectory})" : "null")}");
        if (item != null && _viewModel != null)
        {
            Debug.WriteLine($"[DEBUG] Calling OpenItem for: {item.Name}");
            _viewModel.OpenItem(item);
            Debug.WriteLine($"[DEBUG] OpenItem returned for: {item.Name}");
        }
        else if (_viewModel == null)
        {
            Debug.WriteLine("[DEBUG] _viewModel is null - returning");
        }
    }
}
