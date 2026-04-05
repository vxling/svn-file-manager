import { FileText, Folder, File, FileImage, FileCode } from 'lucide-react';
import { useRepoStore, FileEntry, SvnStatusEntry } from '../stores/repoStore';

// SVN status icons/colors
const STATUS_COLORS: Record<string, string> = {
  modified: 'text-orange-500',
  added: 'text-green-500',
  deleted: 'text-red-500',
  unversioned: 'text-gray-400',
  conflict: 'text-red-600',
  normal: 'text-blue-400',
};

const STATUS_LABELS: Record<string, string> = {
  modified: 'M',
  added: 'A',
  deleted: 'D',
  unversioned: '?',
  conflict: 'C',
  normal: '',
};

export function FileItem({
  entry,
  isSelected,
  onClick,
  onDoubleClick,
  onContextMenu,
}: {
  entry: FileEntry;
  isSelected: boolean;
  onClick: () => void;
  onDoubleClick: () => void;
  onContextMenu: (e: React.MouseEvent) => void;
}) {
  const { svnStatus } = useRepoStore();

  // Get SVN status for this entry
  const status: SvnStatusEntry | undefined = svnStatus.get(entry.path);

  const getFileIcon = () => {
    if (entry.is_directory) {
      return <Folder className="w-5 h-5 text-yellow-500" />;
    }

    const ext = entry.name.split('.').pop()?.toLowerCase();
    switch (ext) {
      case 'png':
      case 'jpg':
      case 'jpeg':
      case 'gif':
      case 'svg':
        return <FileImage className="w-5 h-5 text-purple-500" />;
      case 'ts':
      case 'tsx':
      case 'js':
      case 'jsx':
      case 'py':
      case 'rs':
      case 'go':
        return <FileCode className="w-5 h-5 text-green-500" />;
      case 'md':
      case 'txt':
      case 'doc':
      case 'docx':
        return <FileText className="w-5 h-5 text-blue-400" />;
      default:
        return <File className="w-5 h-5 text-gray-400" />;
    }
  };

  const formatSize = (bytes: number): string => {
    if (bytes === 0) return '-';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  };

  const formatDate = (timestamp: number | null): string => {
    if (!timestamp) return '-';
    const date = new Date(timestamp * 1000);
    return date.toLocaleDateString('zh-CN', {
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  return (
    <div
      className={`
        file-item flex items-center gap-2 px-3 py-2 rounded cursor-pointer
        ${isSelected ? 'bg-accent' : 'hover:bg-accent/50'}
      `}
      onClick={onClick}
      onDoubleClick={onDoubleClick}
      onContextMenu={onContextMenu}
    >
      {/* Icon */}
      {getFileIcon()}

      {/* Name */}
      <div className="flex-1 truncate font-medium">{entry.name}</div>

      {/* SVN Status Badge */}
      {status && status.status !== 'normal' && (
        <span
          className={`text-xs font-bold ${STATUS_COLORS[status.status] || ''}`}
        >
          {STATUS_LABELS[status.status] || status.status}
        </span>
      )}

      {/* Size */}
      <div className="w-20 text-right text-xs text-muted-foreground">
        {entry.is_directory ? '-' : formatSize(entry.size)}
      </div>

      {/* Modified Date */}
      <div className="w-28 text-right text-xs text-muted-foreground">
        {formatDate(entry.modified_at)}
      </div>
    </div>
  );
}
