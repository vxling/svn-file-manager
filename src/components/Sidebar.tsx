import { useState, useEffect } from 'react';
import { ChevronRight, ChevronDown, Folder } from 'lucide-react';
import { useRepoStore, FileEntry } from '../stores/repoStore';

export function Sidebar() {
  const { currentPath, loadDirectory, entries, isLoading } = useRepoStore();
  const [expandedDirs, setExpandedDirs] = useState<Set<string>>(new Set());

  // Directory tree structure
  const dirTree = buildDirTree(entries);

  // Build directory tree from flat entries
  function buildDirTree(entries: FileEntry[]): Map<string, FileEntry[]> {
    const dirs = new Map<string, FileEntry[]>();

    entries
      .filter((e) => e.is_directory)
      .forEach((dir) => {
        const parent = getParentPath(dir.path);
        if (!dirs.has(parent)) {
          dirs.set(parent, []);
        }
        dirs.get(parent)!.push(dir);
      });

    return dirs;
  }

  function getParentPath(path: string): string {
    const parts = path.replace(/\\/g, '/').split('/');
    parts.pop();
    return parts.join('/') || '/';
  }

  function toggleDir(path: string) {
    const newExpanded = new Set(expandedDirs);
    if (newExpanded.has(path)) {
      newExpanded.delete(path);
    } else {
      newExpanded.add(path);
      // Load subdirectory if not loaded
      loadDirectory(path);
    }
    setExpandedDirs(newExpanded);
  }

  function getDirName(path: string): string {
    const parts = path.replace(/\\/g, '/').split('/');
    return parts.pop() || path;
  }

  return (
    <div className="w-48 bg-secondary border-r border-border overflow-y-auto">
      <div className="p-2">
        <div className="text-xs font-semibold text-muted-foreground mb-2 px-2">
          目录树
        </div>

        {/* Root / current directory */}
        <DirItem
          path={currentPath}
          name={getDirName(currentPath) || '/'}
          isExpanded={expandedDirs.has(currentPath)}
          isActive={true}
          onToggle={() => toggleDir(currentPath)}
          onSelect={() => loadDirectory(currentPath)}
        />

        {/* Subdirectories */}
        {dirTree.get(currentPath)?.map((dir) => (
          <DirItem
            key={dir.path}
            path={dir.path}
            name={dir.name}
            isExpanded={expandedDirs.has(dir.path)}
            onToggle={() => toggleDir(dir.path)}
            onSelect={() => loadDirectory(dir.path)}
          />
        ))}

        {isLoading && (
          <div className="text-xs text-muted-foreground px-2 py-1">
            加载中...
          </div>
        )}
      </div>
    </div>
  );
}

interface DirItemProps {
  path: string;
  name: string;
  isExpanded: boolean;
  isActive?: boolean;
  onToggle: () => void;
  onSelect: () => void;
}

function DirItem({ path, name, isExpanded, isActive, onToggle, onSelect }: DirItemProps) {
  return (
    <div
      className={`
        flex items-center gap-1 px-2 py-1 rounded cursor-pointer
        text-sm select-none
        ${isActive ? 'bg-accent text-accent-foreground' : 'hover:bg-accent/50'}
      `}
      style={{ paddingLeft: '0.5rem' }}
      onClick={onSelect}
    >
      <button
        className="p-0.5 hover:bg-accent rounded"
        onClick={(e) => {
          e.stopPropagation();
          onToggle();
        }}
      >
        {isExpanded ? (
          <ChevronDown className="w-3 h-3" />
        ) : (
          <ChevronRight className="w-3 h-3" />
        )}
      </button>
      <Folder className="w-4 h-4 text-yellow-500" />
      <span className="truncate">{name}</span>
    </div>
  );
}
