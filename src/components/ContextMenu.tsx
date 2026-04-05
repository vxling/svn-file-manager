import { useEffect, useRef } from 'react';
import {
  FileText,
  FolderOpen,
  Copy,
  Pencil,
  Trash2,
  Upload,
  ArrowUpDown,
  Plus,
  Minus,
  Undo,
  GitCompare,
  History,
} from 'lucide-react';
import { useRepoStore, FileEntry } from '../stores/repoStore';
import { invoke } from '@tauri-apps/api/core';

export function ContextMenu({
  x,
  y,
  entry,
  onClose,
}: {
  x: number;
  y: number;
  entry: FileEntry;
  onClose: () => void;
}) {
  const menuRef = useRef<HTMLDivElement>(null);
  const {
    svnAdd,
    svnRevert,
    svnDelete,
    svnStatus,
    copyPathToClipboard,
    loadDirectory,
  } = useRepoStore();

  const status = svnStatus.get(entry.path);

  // Close on click outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        onClose();
      }
    };

    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    document.addEventListener('keydown', handleEscape);

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleEscape);
    };
  }, [onClose]);

  // Adjust position to keep menu in viewport
  useEffect(() => {
    if (menuRef.current) {
      const rect = menuRef.current.getBoundingClientRect();
      const viewportWidth = window.innerWidth;
      const viewportHeight = window.innerHeight;

      if (rect.right > viewportWidth) {
        menuRef.current.style.left = `${x - rect.width}px`;
      }
      if (rect.bottom > viewportHeight) {
        menuRef.current.style.top = `${y - rect.height}px`;
      }
    }
  }, [x, y]);

  const handleOpen = () => {
    if (entry.is_directory) {
      loadDirectory(entry.path);
    }
    onClose();
  };

  const handleRevealInBrowser = async () => {
    try {
      await invoke('reveal_in_file_browser', { path: entry.path });
    } catch (error) {
      console.error('Failed to reveal in file browser:', error);
    }
    onClose();
  };

  const handleCopyPath = () => {
    copyPathToClipboard(entry.path);
    onClose();
  };

  const handleSvnAdd = () => {
    svnAdd(entry.path);
    onClose();
  };

  const handleSvnRevert = () => {
    svnRevert(entry.path);
    onClose();
  };

  const handleSvnDelete = () => {
    svnDelete(entry.path);
    onClose();
  };

  const handleSvnDiff = async () => {
    if (!entry.is_directory) {
      try {
        const diff = await invoke<string>('svn_diff', { path: entry.path });
        console.log('Diff:', diff);
        // TODO: Show diff viewer
      } catch (error) {
        console.error('Failed to get diff:', error);
      }
    }
    onClose();
  };

  return (
    <div
      ref={menuRef}
      className="fixed z-50 min-w-[180px] bg-popover border border-border rounded-lg shadow-lg py-1"
      style={{ left: x, top: y }}
    >
      {/* Basic operations */}
      <MenuItem icon={<FolderOpen className="w-4 h-4" />} label="打开" onClick={handleOpen} />
      <MenuItem
        icon={<FileText className="w-4 h-4" />}
        label="在系统文件浏览器中显示"
        onClick={handleRevealInBrowser}
      />
      <MenuItem icon={<Copy className="w-4 h-4" />} label="复制路径" onClick={handleCopyPath} />

      <MenuDivider />

      {/* Rename/Delete */}
      <MenuItem icon={<Pencil className="w-4 h-4" />} label="重命名" disabled />
      <MenuItem icon={<Trash2 className="w-4 h-4" />} label="删除" disabled />

      <MenuDivider />

      {/* SVN operations */}
      <MenuItem icon={<Upload className="w-4 h-4" />} label="提交" disabled />
      <MenuItem icon={<ArrowUpDown className="w-4 h-4" />} label="更新" disabled />

      <MenuDivider />

      {/* Add to version control */}
      {(!status || status.status === 'unversioned') && (
        <MenuItem icon={<Plus className="w-4 h-4" />} label="添加到版本控制" onClick={handleSvnAdd} />
      )}

      {/* Revert */}
      {status && status.status === 'modified' && (
        <MenuItem icon={<Undo className="w-4 h-4" />} label="撤销更改" onClick={handleSvnRevert} />
      )}

      {/* Delete from version control */}
      {status && status.status === 'added' && (
        <MenuItem icon={<Minus className="w-4 h-4" />} label="从版本控制删除" onClick={handleSvnDelete} />
      )}

      {/* Diff */}
      {status && status.status === 'modified' && !entry.is_directory && (
        <MenuItem icon={<GitCompare className="w-4 h-4" />} label="查看变更" onClick={handleSvnDiff} />
      )}

      <MenuDivider />

      {/* SVN Info */}
      <MenuItem icon={<History className="w-4 h-4" />} label="查看历史" disabled />
    </div>
  );
}

interface MenuItemProps {
  icon: React.ReactNode;
  label: string;
  onClick: () => void;
  disabled?: boolean;
}

function MenuItem({ icon, label, onClick, disabled }: MenuItemProps) {
  return (
    <button
      className={`
        w-full flex items-center gap-2 px-3 py-1.5 text-sm
        ${disabled ? 'opacity-50 cursor-not-allowed' : 'hover:bg-accent'}
      `}
      onClick={disabled ? undefined : onClick}
      disabled={disabled}
    >
      {icon}
      <span>{label}</span>
    </button>
  );
}

function MenuDivider() {
  return <div className="my-1 border-t border-border" />;
}
