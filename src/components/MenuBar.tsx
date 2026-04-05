import { useSettingsStore } from '../stores/settingsStore';

export function MenuBar() {
  const { repositories, activeRepositoryId, setActiveRepository } = useSettingsStore();

  const activeRepo = repositories.find((r) => r.id === activeRepositoryId);

  return (
    <div className="h-8 bg-primary flex items-center px-2 text-sm border-b border-border">
      <div className="flex items-center gap-4">
        <span className="font-semibold">SVN File Manager</span>

        {/* Repository selector */}
        <div className="flex items-center gap-2">
          <span className="text-muted-foreground">仓库:</span>
          <select
            className="bg-background border border-border rounded px-2 py-1 text-xs"
            value={activeRepositoryId || ''}
            onChange={(e) => setActiveRepository(e.target.value)}
          >
            {repositories.length === 0 && (
              <option value="">未配置仓库</option>
            )}
            {repositories.map((repo) => (
              <option key={repo.id} value={repo.id}>
                {repo.name}
              </option>
            ))}
          </select>
        </div>

        {activeRepo && (
          <span className="text-xs text-muted-foreground">
            {activeRepo.url}
          </span>
        )}
      </div>
    </div>
  );
}
