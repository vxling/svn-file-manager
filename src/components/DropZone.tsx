import { useState, useCallback, ReactNode } from 'react';
import { invoke } from '@tauri-apps/api/core';
import { useRepoStore } from '../stores/repoStore';

export function DropZone({ children }: { children: ReactNode }) {
  const [isDragging, setIsDragging] = useState(false);
  const { loadDirectory, currentPath, refreshStatus } = useRepoStore();

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();

    // Check if dragging files
    if (e.dataTransfer.types.includes('Files')) {
      setIsDragging(true);
    }
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();

    // Only set to false if leaving the drop zone entirely
    const rect = e.currentTarget.getBoundingClientRect();
    const { clientX, clientY } = e;
    if (
      clientX < rect.left ||
      clientX > rect.right ||
      clientY < rect.top ||
      clientY > rect.bottom
    ) {
      setIsDragging(false);
    }
  }, []);

  const handleDrop = useCallback(
    async (e: React.DragEvent) => {
      e.preventDefault();
      e.stopPropagation();
      setIsDragging(false);

      if (!currentPath) {
        console.error('No current path set');
        return;
      }

      // Get dropped files
      const files = Array.from(e.dataTransfer.files);

      if (files.length === 0) {
        // Try to get file paths from other sources
        const text = e.dataTransfer.getData('text/plain');
        if (text) {
          try {
            // Parse as JSON array of paths or single path
            let paths = JSON.parse(text);
            if (!Array.isArray(paths)) {
              paths = [paths];
            }

            // Copy files to workdir
            const result = await invoke<{ copied_count: number; failed_paths: string[] }>(
              'copy_files_to_workdir',
              {
                sourcePaths: paths,
                targetDir: currentPath,
              }
            );

            console.log(`Copied ${result.copied_count} files`);

            if (result.failed_paths.length > 0) {
              console.error('Failed to copy:', result.failed_paths);
            }

            // Refresh directory and SVN status
            await loadDirectory(currentPath);
            await refreshStatus();
          } catch (error) {
            console.error('Failed to process dropped files:', error);
          }
        }
        return;
      }

      // Get file paths from File objects
      const paths = files.map((file) => (file as any).path || file.name);

      try {
        // Copy files to workdir
        const result = await invoke<{ copied_count: number; failed_paths: string[] }>(
          'copy_files_to_workdir',
          {
            sourcePaths: paths,
            targetDir: currentPath,
          }
        );

        console.log(`Copied ${result.copied_count} files`);

        if (result.failed_paths.length > 0) {
          console.error('Failed to copy:', result.failed_paths);
        }

        // Refresh directory and SVN status
        await loadDirectory(currentPath);
        await refreshStatus();
      } catch (error) {
        console.error('Failed to copy dropped files:', error);
      }
    },
    [currentPath, loadDirectory, refreshStatus]
  );

  return (
    <div
      className={`flex-1 relative ${isDragging ? 'drop-zone-active' : ''}`}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      {children}

      {/* Drop overlay */}
      {isDragging && (
        <div className="absolute inset-0 bg-blue-500/20 flex items-center justify-center pointer-events-none z-10">
          <div className="bg-popover border-2 border-dashed border-blue-500 rounded-xl px-8 py-6 shadow-xl">
            <div className="text-4xl mb-2">📥</div>
            <div className="text-lg font-semibold text-blue-500">释放以添加到 SVN</div>
            <div className="text-sm text-muted-foreground mt-1">
              文件将被复制到工作目录并自动添加
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
