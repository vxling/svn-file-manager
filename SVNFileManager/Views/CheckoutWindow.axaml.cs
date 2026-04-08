using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SVNFileManager.Services;

namespace SVNFileManager.Views;

public partial class CheckoutWindow : Window
{
    public CheckoutWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Wire up buttons in code
        this.FindControl<Button>("CancelButton")!.Click += (_, _) => Close();
        this.FindControl<Button>("CheckoutButton")!.Command = (DataContext as CheckoutWindowViewModel)!.CheckoutCommand;
    }
}

public partial class CheckoutWindowViewModel : ObservableObject
{
    private readonly SvnService _svnService;
    private readonly Action<RepositoryResult> _onCompleted;

    [ObservableProperty]
    private string _repoName = "";

    [ObservableProperty]
    private string _repoUrl = "";

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _statusText = "Please fill in the repository information.";

    [ObservableProperty]
    private bool _isCheckingOut;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isSuccess;

    public bool CanCheckout => !IsCheckingOut && !IsSuccess;

    partial void OnIsCheckingOutChanged(bool value) => OnPropertyChanged(nameof(CanCheckout));
    partial void OnIsSuccessChanged(bool value) => OnPropertyChanged(nameof(CanCheckout));

    public CheckoutWindowViewModel(Action<RepositoryResult> onCompleted)
    {
        _svnService = new SvnService();
        _onCompleted = onCompleted;
    }

    private string WorkCopiesBaseDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SVNFileManager",
            "workcopies");

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        HasError = false;
        IsSuccess = false;

        if (string.IsNullOrWhiteSpace(RepoName))
        {
            StatusText = "Repository name is required.";
            HasError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(RepoUrl))
        {
            StatusText = "Repository URL is required.";
            HasError = true;
            return;
        }

        var localPath = Path.Combine(WorkCopiesBaseDir, RepoName.Trim());

        if (Directory.Exists(localPath))
        {
            StatusText = $"Local path already exists: {localPath}\nPlease choose a different repository name.";
            HasError = true;
            return;
        }

        IsCheckingOut = true;
        StatusText = $"Checking out to {localPath}...";

        try
        {
            var result = await _svnService.CheckoutAsync(
                RepoUrl.Trim(),
                localPath,
                string.IsNullOrWhiteSpace(Username) ? null : Username.Trim(),
                string.IsNullOrWhiteSpace(Password) ? null : Password.Trim());

            if (result.exitCode == 0)
            {
                IsSuccess = true;
                StatusText = $"Checkout successful!\nSaved to: {localPath}";
                _onCompleted?.Invoke(new RepositoryResult
                {
                    Success = true,
                    LocalPath = localPath,
                    Url = RepoUrl.Trim(),
                    Username = Username.Trim(),
                    RepoName = RepoName.Trim()
                });
            }
            else
            {
                HasError = true;
                StatusText = $"Checkout failed (exit {result.exitCode}):\n{result.output}";

                // Clean up partial checkout
                if (Directory.Exists(localPath))
                {
                    try { Directory.Delete(localPath, true); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusText = $"Checkout error: {ex.Message}";
        }
        finally
        {
            IsCheckingOut = false;
        }
    }
}

public class RepositoryResult
{
    public bool Success { get; set; }
    public string LocalPath { get; set; } = "";
    public string Url { get; set; } = "";
    public string Username { get; set; } = "";
    public string RepoName { get; set; } = "";
    public string? EncryptedPassword { get; set; }
}
