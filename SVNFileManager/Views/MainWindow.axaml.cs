using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SVNFileManager.Models;
using SVNFileManager.ViewModels;

namespace SVNFileManager.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private DateTime _lastClickTime = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        Closing += (_, _) => _viewModel.Dispose();
    }

    private async void OnFileClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedIndex >= 0)
        {
            var items = listBox.ItemsSource as System.Collections.IList;
            if (items != null && listBox.SelectedIndex < items.Count)
            {
                var item = items[listBox.SelectedIndex] as FileItem;
                if (item != null && _viewModel != null)
                {
                    var now = DateTime.Now;
                    if (now - _lastClickTime < TimeSpan.FromMilliseconds(500))
                    {
                        // Double click
                        await _viewModel.OpenItemCommand.ExecuteAsync(item);
                    }
                    _lastClickTime = now;
                }
            }
        }
    }
}
