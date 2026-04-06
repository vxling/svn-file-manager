import {
  RefreshCw,
  ArrowUpDown,
  Eye,
  EyeOff,
  Settings,
} from 'lucide-react';
import { useRepoStore } from '../stores/repoStore';
import { useSettingsStore } from '../stores/settingsStore';

export function Toolbar() {
  const { refreshStatus, svnUpdate, isLoading, svnInfo } = useRepoStore();
  const { showHiddenFiles, updateSettings } = useSettingsStore();

  return (
    <div className="h-10 bg-secondary flex items-center px-4 gap-2 border-b border-border">
      {/* Refresh */}
      <ToolbarButton
        icon={<RefreshCw className="w-4 h-4" />}
        label="刷新"
        onClick={() => refreshStatus()}
        disabled={isLoading}
      />

      {/* SVN Update */}
      <ToolbarButton
        icon={<ArrowUpDown className="w-4 h-4" />}
        label="更新"
        onClick={() => svnUpdate()}
        disabled={isLoading}
      />

      <div className="w-px h-6 bg-border mx-2" />

      {/* Show hidden files toggle */}
      <ToolbarButton
        icon={showHiddenFiles ? <Eye className="w-4 h-4" /> : <EyeOff className="w-4 h-4" />}
        label={showHiddenFiles ? '隐藏文件' : '显示文件'}
        onClick={() => updateSettings({ showHiddenFiles: !showHiddenFiles })}
        active={showHiddenFiles}
      />

      <div className="flex-1" />

      {/* SVN Info */}
      {svnInfo && (
        <span className="text-xs text-muted-foreground mr-4">
          r{svnInfo.revision}
        </span>
      )}

      {/* Settings */}
      <ToolbarButton
        icon={<Settings className="w-4 h-4" />}
        label="设置"
        onClick={() => {
          // Open settings dialog
          console.log('Open settings');
        }}
      />
    </div>
  );
}

interface ToolbarButtonProps {
  icon: React.ReactNode;
  label: string;
  onClick: () => void;
  disabled?: boolean;
  active?: boolean;
}

function ToolbarButton({ icon, label, onClick, disabled, active }: ToolbarButtonProps) {
  return (
    <button
      className={`
        flex items-center gap-1.5 px-3 py-1.5 rounded text-sm
        transition-colors
        ${disabled ? 'opacity-50 cursor-not-allowed' : 'hover:bg-accent'}
        ${active ? 'bg-accent text-accent-foreground' : ''}
      `}
      onClick={onClick}
      disabled={disabled}
      title={label}
    >
      {icon}
      <span>{label}</span>
    </button>
  );
}
