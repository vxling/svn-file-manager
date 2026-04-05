import { useEffect } from 'react';
import { listen } from '@tauri-apps/api/event';
import { useRepoStore } from './stores/repoStore';
import { useSettingsStore } from './stores/settingsStore';
import { MenuBar } from './components/MenuBar';
import { Toolbar } from './components/Toolbar';
import { Sidebar } from './components/Sidebar';
import { FileList } from './components/FileList';
import { StatusBar } from './components/StatusBar';
import { DropZone } from './components/DropZone';
import { ErrorBoundary } from './components/ErrorBoundary';

function AppContent() {
  const { refreshStatus, loadActiveRepository } = useRepoStore();
  const { loadSettings } = useSettingsStore();

  useEffect(() => {
    // Load settings and active repository on startup
    loadSettings();
    loadActiveRepository();

    // Listen for file change events from Rust backend
    const unlisten = listen<{ path: string; kind: string }>('file-changed', (event) => {
      console.log('File changed:', event.payload);
      // Refresh SVN status when file changes detected
      refreshStatus();
    });

    return () => {
      unlisten.then((fn) => fn());
    };
  }, []);

  return (
    <div className="h-full flex flex-col bg-background text-foreground">
      <MenuBar />
      <Toolbar />
      <div className="flex-1 flex overflow-hidden">
        <Sidebar />
        <DropZone>
          <FileList />
        </DropZone>
      </div>
      <StatusBar />
    </div>
  );
}

export default function App() {
  return (
    <ErrorBoundary>
      <AppContent />
    </ErrorBoundary>
  );
}
