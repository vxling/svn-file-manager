namespace SVNFileManager.Models;

public enum SvnStatus
{
    Normal,
    Modified,
    Added,
    Deleted,
    Conflicted,
    Unversioned,
    Missing,
    Replaced,
    Obstructed,
    External,
    Incomplete,
    Unknown
}

public static class SvnStatusExtensions
{
    public static string ToDisplayString(this SvnStatus status) => status switch
    {
        SvnStatus.Normal => "",
        SvnStatus.Modified => "M",
        SvnStatus.Added => "A",
        SvnStatus.Deleted => "D",
        SvnStatus.Conflicted => "C",
        SvnStatus.Unversioned => "?",
        SvnStatus.Missing => "!",
        SvnStatus.Replaced => "R",
        SvnStatus.Obstructed => "~",
        SvnStatus.External => "X",
        SvnStatus.Incomplete => "?",
        _ => "?"
    };

    public static string ToColor(this SvnStatus status) => status switch
    {
        SvnStatus.Normal => "#888888",
        SvnStatus.Modified => "#FFB900",
        SvnStatus.Added => "#00D26A",
        SvnStatus.Deleted => "#FF3860",
        SvnStatus.Conflicted => "#FF3860",
        SvnStatus.Unversioned => "#888888",
        SvnStatus.Missing => "#FF3860",
        SvnStatus.Replaced => "#00D26A",
        _ => "#888888"
    };
}
