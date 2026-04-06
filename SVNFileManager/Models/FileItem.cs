using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SVNFileManager.Models;

public partial class FileItem : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _fullPath = "";

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private SvnStatus _svnStatus = SvnStatus.Normal;

    [ObservableProperty]
    private long _fileSize;

    [ObservableProperty]
    private DateTime _lastModified;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    public FileItem() { }

    public FileItem(string path, FileInfo fileInfo, SvnStatus status = SvnStatus.Normal)
    {
        Name = fileInfo.Name;
        FullPath = path;
        IsDirectory = fileInfo.Attributes.HasFlag(FileAttributes.Directory);
        FileSize = fileInfo.Length;
        LastModified = fileInfo.LastWriteTime;
        SvnStatus = status;
    }

    public FileItem(string path, bool isDirectory, SvnStatus status = SvnStatus.Normal)
    {
        Name = Path.GetFileName(path);
        FullPath = path;
        IsDirectory = isDirectory;
        SvnStatus = status;
    }
}
