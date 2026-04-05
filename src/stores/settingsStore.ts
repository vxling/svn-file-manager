import { create } from 'zustand';
import { invoke } from '@tauri-apps/api/core';
import { persist } from 'zustand/middleware';

export interface Repository {
  id: string;
  name: string;
  url: string;
  username?: string;
  password?: string;
  isDefault: boolean;
  lastSynced: string | null;
  enabled: boolean;
  rememberPassword: boolean;
  autoCommitEnabled?: boolean;
  autoCommitExcludePatterns?: string[];
}

interface SettingsState {
  // Repositories
  repositories: Repository[];
  activeRepositoryId: string | null;

  // SVN settings
  svnPath: string;
  autoRefresh: boolean;
  refreshInterval: number;
  showHiddenFiles: boolean;
  showUnversioned: boolean;

  // Auto-sync settings
  autoSync: boolean;
  autoSyncInterval: number;

  // Auto-commit settings
  autoCommit: boolean;
  autoCommitDelay: number;
  autoCommitInterval: number;
  autoCommitMessage: string;
  autoCommitOnNoConflict: boolean;
  autoCommitNotifyOnConflict: boolean;
  autoCommitAddNewFiles: boolean;
  autoCommitExcludePatterns: string[];

  // UI state
  isLoading: boolean;

  // Actions
  loadSettings: () => Promise<void>;
  addRepository: (repo: Omit<Repository, 'id'>) => Promise<void>;
  removeRepository: (id: string) => void;
  updateRepository: (id: string, repo: Partial<Repository>) => void;
  setActiveRepository: (id: string) => void;
  updateSettings: (settings: Partial<SettingsState>) => void;
}

// Generate UUID
function generateId(): string {
  return crypto.randomUUID();
}

export const useSettingsStore = create<SettingsState>()(
  persist(
    (set, get) => ({
      repositories: [],
      activeRepositoryId: null,
      svnPath: 'svn',
      autoRefresh: true,
      refreshInterval: 3000,
      showHiddenFiles: false,
      showUnversioned: true,
      autoSync: false,
      autoSyncInterval: 600000, // 10 minutes
      autoCommit: false,
      autoCommitDelay: 5000,
      autoCommitInterval: 30000,
      autoCommitMessage: 'Auto-commit: {date} {time}',
      autoCommitOnNoConflict: true,
      autoCommitNotifyOnConflict: true,
      autoCommitAddNewFiles: false,
      autoCommitExcludePatterns: ['*.log', '*.tmp', 'node_modules/'],
      isLoading: false,

      loadSettings: async () => {
        // Settings are persisted via zustand/middleware
        // This can be extended to load from file if needed
        console.log('Settings loaded');
      },

      addRepository: async (repo) => {
        const id = generateId();
        const newRepo: Repository = { ...repo, id };

        set((state) => ({
          repositories: [...state.repositories, newRepo],
        }));

        // If this is the first or default repo, activate it
        if (repo.isDefault || get().repositories.length === 0) {
          set({ activeRepositoryId: id });
        }
      },

      removeRepository: (id: string) => {
        set((state) => ({
          repositories: state.repositories.filter((r) => r.id !== id),
          activeRepositoryId:
            state.activeRepositoryId === id ? null : state.activeRepositoryId,
        }));
      },

      updateRepository: (id: string, repo: Partial<Repository>) => {
        set((state) => ({
          repositories: state.repositories.map((r) =>
            r.id === id ? { ...r, ...repo } : r
          ),
        }));
      },

      setActiveRepository: (id: string) => {
        set({ activeRepositoryId: id, isLoading: true });

        // Switch to new repository
        const repo = get().repositories.find((r) => r.id === id);
        if (repo) {
          // Stop current watcher if any
          invoke('stop_watching').catch(console.error);

          // Load new repository
          invoke<{ path: string }>('get_workdir_path', { name: repo.name })
            .then(({ path }) => {
              // Load directory and start watching
              return invoke('start_watching', {
                path,
                config: { debounceMs: 300, watchDepth: 'recursive' },
              });
            })
            .then(() => {
              set({ isLoading: false });
            })
            .catch((error) => {
              console.error('Failed to switch repository:', error);
              set({ isLoading: false });
            });
        }
      },

      updateSettings: (settings) => {
        set((state) => ({ ...state, ...settings }));
      },
    }),
    {
      name: 'svn-file-manager-settings',
    }
  )
);
