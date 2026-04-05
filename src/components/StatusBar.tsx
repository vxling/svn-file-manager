import { useRepoStore } from '../stores/repoStore';
import { useSettingsStore } from '../stores/settingsStore';
import { Circle } from 'lucide-react';

export function StatusBar() {
  const { currentPath, svnInfo, entries, svnStatus, isWatching, isLoading } =
    useRepoStore();
  const { activeRepositoryId, repositories } = useSettingsStore();

  const activeRepo = repositories.find((r) => r.id === activeRepositoryId);

  // Count files by status
  let modifiedCount = 0;
  let addedCount = 0;
  let deletedCount = 0;
  let unversionedCount = 0;

  svnStatus.forEach((status) => {
    switch (status.status) {
      case 'modified':
        modifiedCount++;
        break;
      case 'added':
        addedCount++;
        break;
      case 'deleted':
        deletedCount++;
        break;
      case 'unversioned':
        unversionedCount++;
        break;
    }
  });

  return (
    <div className="h-6 bg-secondary flex items-center px-3 text-xs text-muted-foreground border-t border-border">
      {/* Watch status */}
      <div className="flex items-center gap-1.5 mr-4">
        <Circle
          className={`w-2 h-2 ${isWatching ? 'fill-green-500 text-green-500' : 'fill-gray-400 text-gray-400'}`}
        />
        <span>{isWatching ? '监控中' : '未监控'}</span>
      </div>

      {/* SVN Status summary */}
      {svnStatus.size > 0 && (
        <div className="flex items-center gap-3 mr-4">
          {modifiedCount > 0 && (
            <span className="text-orange-500">修改: {modifiedCount}</span>
          )}
          {addedCount > 0 && <span className="text-green-500">新增: {addedCount}</span>}
          {deletedCount > 0 && <span className="text-red-500">删除: {deletedCount}</span>}
          {unversionedCount > 0 && (
            <span className="text-gray-400">未追踪: {unversionedCount}</span>
          )}
        </div>
      )}

      <div className="flex-1" />

      {/* Current path */}
      <div className="truncate max-w-md" title={currentPath}>
        {currentPath}
      </div>

      {/* SVN revision */}
      {svnInfo && (
        <div className="ml-4">
          r{svnInfo.revision}
        </div>
      )}

      {/* Loading indicator */}
      {isLoading && (
        <div className="ml-4 text-blue-500">
          加载中...
        </div>
      )}
    </div>
  );
}
