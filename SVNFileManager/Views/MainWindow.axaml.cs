using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SVNFileManager.Models;
using SVNFileManager.ViewModels;

namespace SVNFileManager.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        Closing += (_, _) => _viewModel.Dispose();
    }

    private async void OnListBoxDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Find the ListBox from sender (could be the ListBox itself or a child control)
        ListBox? listBox = null;
        
        if (sender is ListBox lb)
        {
            listBox = lb;
        }
        else if (sender is Control c)
        {
            // Walk up the visual tree to find the ListBox
            while (c != null)
            {
                if (c is ListBox parentLb)
                {
                    listBox = parentLb;
                    break;
                }
                c = c.Parent as Control;
            }
        }

        if (listBox == null) return;

        var item = listBox.SelectedItem as FileItem;
        if (item != null && _viewModel != null)
        {
            await _viewModel.OpenItemCommand.ExecuteAsync(item);
        }
    }
}
