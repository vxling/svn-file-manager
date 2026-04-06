import { create } from 'zustand';
import { invoke } from '@tauri-apps/api/core';
import { useSettingsStore } from './settingsStore';

// Types matching Rust structs
export interface FileEntry {
  name: string;
  path: string;
  is_directory: boolean;
  size: number;
  modified_at: number | null;
}

export interface SvnStatusEntry {
  path: string;
  status: string;
  props_status: string;
  revision: number | null;
}

export interface SvnStatusResult {
  entries: SvnStatusEntry[];
  wc_root: string | null;
}

export interface SvnInfo {
  url: string;
  revision: number;
  kind: string;
  repository_root: string;
  wc_path: string;
}

interface RepoState {
  // Current state
  currentPath: string;
  entries: FileEntry[];
  selectedEntry: FileEntry | null;
  selectedEntries: FileEntry[];
  svnStatus: Map<string, SvnStatusEntry>;
  svnInfo: SvnInfo | null;
  isWatching: boolean;
  isLoading: boolean;
  error: string | null;

  // Actions
  setCurrentPath: (path: string) => void;
  loadDirectory: (path: string) => Promise<void>;
  refreshStatus: () => Promise<void>;
  refreshInfo: () => Promise<void>;
  loadActiveRepository: () => Promise<void>;
  setSelectedEntry: (entry: FileEntry | null) => void;
  setSelectedEntries: (entries: FileEntry[]) => void;
  toggleWatch: () => Promise<void>;
  svnUpdate: () => Promise<void>;
  svnCommit: (message: string) => Promise<void>;
  svnAdd: (path: string) => Promise<void>;
  svnRevert: (path: string) => Promise<void>;
  svnDelete: (path: string) => Promise<void>;
  copyPathToClipboard: (path: string) => Promise<void>;
  clearError: () => void;
}

export const useRepoStore = create<RepoState>((set, get) => ({
  currentPath: '',
  entries: [],
  selectedEntry: null,
  selectedEntries: [],
  svnStatus: new Map(),
  svnInfo: null,
  isWatching: false,
  isLoading: false,
  error: null,

  setCurrentPath: (path: string) => {
    set({ currentPath: path });
  },

  loadDirectory: async (path: string) => {
    set({ isLoading: true, error: null });
    try {
      const entries = await invoke<FileEntry[]>('read_directory', { path });
      set({ entries, currentPath: path, isLoading: false });
    } catch (error) {
      set({ error: String(error), isLoading: false });
    }
  },

  refreshStatus: async () => {
    const { currentPath } = get();
    if (!currentPath) return;

    try {
      const result = await invoke<SvnStatusResult>('svn_status', { path: currentPath });
      const statusMap = new Map<string, SvnStatusEntry>();
      result.entries.forEach((entry) => {
        statusMap.set(entry.path, entry);
      });
      set({ svnStatus: statusMap });
    } catch (error) {
      console.error('Failed to refresh SVN status:', error);
    }
  },

  refreshInfo: async () => {
    const { currentPath } = get();
    if (!currentPath) return;

    try {
      const info = await invoke<SvnInfo>('svn_info', { path: currentPath });
      set({ svnInfo: info });
    } catch (error) {
      console.error('Failed to refresh SVN info:', error);
    }
  },

  loadActiveRepository: async () => {
    try {
      const { activeRepositoryId, repositories } = useSettingsStore.getState();

      if (!activeRepositoryId) {
        // Load default or first repository
        const repos = useSettingsStore.getState().repositories;
        if (repos.length > 0) {
          const defaultRepo = repos.find((r) => r.isDefault) || repos[0];
          useSettingsStore.getState().setActiveRepository(defaultRepo.id);
        }
        return;
      }

      const repo = repositories.find((r) => r.id === activeRepositoryId);
      if (repo) {
        // Get workdir path
        const workdir = await invoke<string>('get_workdir_path', { name: repo.name });
        await get().loadDirectory(workdir);
        await get().refreshStatus();
        await get().refreshInfo();
      }
    } catch (error) {
      console.error('Failed to load active repository:', error);
    }
  },

  setSelectedEntry: (entry: FileEntry | null) => {
    set({ selectedEntry: entry });
  },

  setSelectedEntries: (entries: FileEntry[]) => {
    set({ selectedEntries: entries });
  },

  toggleWatch: async () => {
    const { isWatching, currentPath } = get();
    try {
      if (isWatching) {
        await invoke('stop_watching');
        set({ isWatching: false });
      } else {
        await invoke('start_watching', {
          path: currentPath,
          config: { debounceMs: 300, watchDepth: 'recursive' },
        });
        set({ isWatching: true });
      }
    } catch (error) {
      set({ error: String(error) });
    }
  },

  svnUpdate: async () => {
    const { currentPath } = get();
    if (!currentPath) return;

    set({ isLoading: true });
    try {
      await invoke('svn_update', { path: currentPath });
      await get().refreshStatus();
      await get().refreshInfo();
      set({ isLoading: false });
    } catch (error) {
      set({ error: String(error), isLoading: false });
    }
  },

  svnCommit: async (message: string) => {
    const { currentPath } = get();
    if (!currentPath) return;

    set({ isLoading: true });
    try {
      await invoke('svn_commit', { path: currentPath, message });
      await get().refreshStatus();
      set({ isLoading: false });
    } catch (error) {
      set({ error: String(error), isLoading: false });
    }
  },

  svnAdd: async (path: string) => {
    try {
      await invoke('svn_add', { path });
      await get().refreshStatus();
    } catch (error) {
      set({ error: String(error) });
    }
  },

  svnRevert: async (path: string) => {
    try {
      await invoke('svn_revert', { path });
      await get().refreshStatus();
    } catch (error) {
      set({ error: String(error) });
    }
  },

  svnDelete: async (path: string) => {
    try {
      await invoke('svn_delete', { path });
      await get().refreshStatus();
    } catch (error) {
      set({ error: String(error) });
    }
  },

  copyPathToClipboard: async (path: string) => {
    try {
      await invoke('copy_path_to_clipboard', { path });
    } catch (error) {
      set({ error: String(error) });
    }
  },

  clearError: () => {
    set({ error: null });
  },
}));
