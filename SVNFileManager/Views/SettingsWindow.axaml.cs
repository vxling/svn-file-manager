using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SVNFileManager.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        this.FindControl<Button>("CancelButton")!.Click += (_, _) => Close();

        this.FindControl<Button>("SaveButton")!.Click += (_, _) =>
        {
            if (DataContext is SettingsWindowViewModel vm)
            {
                _ = vm.SaveAsync();
            }
            Close();
        };

        this.FindControl<Button>("ManageCredsButton")!.Click += (_, _) =>
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var msgBox = new Window
                {
                    Title = "凭证管理",
                    Width = 400,
                    Height = 200,
                    CanResize = false,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = new StackPanel
                    {
                        Margin = new Thickness(24),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "凭证管理功能开发中...",
                                FontSize = 14,
                                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#888888")),
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = "密码保存在系统密钥链中",
                                FontSize = 12,
                                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#AAAAAA")),
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                Margin = new Thickness(0, 8, 0, 0)
                            }
                        }
                    }
                };
                msgBox.ShowDialog(desktop.MainWindow!);
            }
        };
    }
}
