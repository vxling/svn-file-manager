import { useState, useCallback } from 'react';
import { FileItem } from './FileItem';
import { ContextMenu } from './ContextMenu';
import { useRepoStore, FileEntry } from '../stores/repoStore';
import { useSettingsStore } from '../stores/settingsStore';

export function FileList() {
  const { entries, selectedEntry, setSelectedEntry, loadDirectory, isLoading } =
    useRepoStore();
  const { showHiddenFiles } = useSettingsStore();
  const [contextMenu, setContextMenu] = useState<{
    x: number;
    y: number;
    entry: FileEntry;
  } | null>(null);

  // Filter entries based on settings
  const visibleEntries = entries.filter((entry) => {
    if (!showHiddenFiles && entry.name.startsWith('.')) {
      return false;
    }
    return true;
  });

  const handleContextMenu = useCallback((e: React.MouseEvent, entry: FileEntry) => {
    e.preventDefault();
    setContextMenu({
      x: e.clientX,
      y: e.clientY,
      entry,
    });
  }, []);

  const handleDoubleClick = useCallback(
    (entry: FileEntry) => {
      if (entry.is_directory) {
        loadDirectory(entry.path);
      }
    },
    [loadDirectory]
  );

  const handleClick = useCallback(
    (entry: FileEntry) => {
      setSelectedEntry(entry);
    },
    [setSelectedEntry]
  );

  const closeContextMenu = useCallback(() => {
    setContextMenu(null);
  }, []);

  if (isLoading) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-muted-foreground">加载中...</div>
      </div>
    );
  }

  return (
    <div className="flex-1 overflow-auto p-2" onClick={closeContextMenu}>
      <div className="space-y-0.5">
        {visibleEntries.map((entry) => (
          <FileItem
            key={entry.path}
            entry={entry}
            isSelected={selectedEntry?.path === entry.path}
            onClick={() => handleClick(entry)}
            onDoubleClick={() => handleDoubleClick(entry)}
            onContextMenu={(e) => handleContextMenu(e, entry)}
          />
        ))}
      </div>

      {visibleEntries.length === 0 && (
        <div className="flex items-center justify-center h-full text-muted-foreground">
          目录为空
        </div>
      )}

      {contextMenu && (
        <ContextMenu
          x={contextMenu.x}
          y={contextMenu.y}
          entry={contextMenu.entry}
          onClose={closeContextMenu}
        />
      )}
    </div>
  );
}
