# SVN File Manager - 技术方案

## 1. 项目概述

**项目名称:** SVN File Manager (代号: SVNExplorer)

**项目类型:** 桌面应用程序

**核心功能:** 一个内置 SVN 版本控制功能的文件管理器，能够在文件修改或更新时自动刷新仓库状态，并支持自动提交到 SVN 服务器。

**目标用户:** 软件开发人员、版本控制用户

---

## 2. 技术架构

### 2.1 技术选型

| 层级 | 技术选型 | 说明 |
|------|----------|------|
| **桌面框架** | Tauri v2 | 轻量、跨平台、Rust 后端 |
| **前端框架** | React + TypeScript | 生态成熟、开发效率高 |
| **UI 组件库** | shadcn/ui + Tailwind CSS | 现代简洁的 UI |
| **文件监控** | notify (Rust) | 高性能文件系统事件监听 |
| **SVN 交互** | svn CLI | 通过命令行调用 SVN |
| **状态管理** | Zustand | 轻量级 React 状态管理 |
| **打包工具** | Tauri Bundler | 生成 Windows/macOS/Linux 安装包 |

### 2.2 架构图

```
┌─────────────────────────────────────────────────────────────┐
│                        UI 层 (React)                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │  文件列表视图  │  │  SVN状态面板  │  │    Diff查看器       │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│                    Tauri IPC Bridge                         │
├───────────────────┬─────────────────────────────────────────┤
│   文件监控模块     │            SVN 交互模块                 │
│   (Rust notify)   │    (svn status/info/diff/add/commit)    │
├───────────────────┴─────────────────────────────────────────┤
│                    系统层 (Rust Backend)                     │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │  文件系统    │  │  SVN CLI    │  │   事件通知系统       │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. 功能模块

### 3.1 核心功能

#### F1: 文件浏览
- 目录树导航
- 文件列表（网格/列表视图切换）
- 文件类型图标
- 文件大小、修改时间显示
- 右键菜单（复制、粘贴、删除、重命名）

#### F2: SVN 状态显示
- 文件/目录的 SVN 状态图标（M=修改、A=新增、D=删除、?=未追踪、C=冲突）
- 工作副本根目录识别
- 当前分支/版本信息显示
- 状态自动刷新（文件变化时）

#### F3: 文件监控与自动刷新
- 监听工作副本内的文件变化
- 文件修改时自动触发 `svn status` 刷新
- 可配置监控深度（整个仓库/仅当前目录）
- 手动刷新按钮

#### F4: 自动提交（Auto-Commit）
- 文件变化被检测后，自动执行 `svn update` 刷新最新版本
- 检查是否有冲突（`C` 状态）
- 如无冲突，自动执行 `svn commit` 提交更改
- 提交信息格式：`Auto-commit: {timestamp}` 或自定义模板
- 支持配置提交间隔（避免频繁提交）
- 冲突时自动停止，等待用户手动处理
- **每次仅能监控和操作一个活动仓库**

#### F5: 仓库切换与启动同步
- 程序启动时自动加载上次活动的仓库（如存在）
- 切换活动仓库时：
  1. 停止当前仓库的文件监控
  2. 切换到新仓库
  3. **自动执行 `svn update`** 与服务器同步
  4. 刷新 SVN 状态显示
  5. 启动新仓库的文件监控
- 状态栏显示同步进度
- 同步完成后显示最新版本号

#### F6: 定时自动同步
- 活动仓库定时执行 `svn update` 与服务器保持同步
- 默认间隔：**10 分钟**（可配置）
- 可配置范围：1 分钟 ~ 60 分钟
- 同步时状态栏显示「正在同步...」
- 同步完成后更新版本信息
- 可在设置中启用/禁用
- 下次切换仓库时自动应用新间隔
- `svn update` - 更新到最新版本
- `svn commit` - 提交更改（需输入提交信息）
- `svn add` - 添加文件到版本控制
- `svn delete` - 从版本控制删除
- `svn revert` - 撤销更改
- `svn diff` - 查看文件变更
- `svn log` - 查看提交历史

#### F7: Diff 查看器
- 侧边栏展示文件变更
- 行级差异高亮（增加/删除/修改）
- 支持图片文件 diff（暂不支持）

#### F8: 与操作系统文件浏览器单向集成（仅接收）

**处理逻辑（统一）**

拖放和粘贴的文件处理流程相同：

```
用户拖入/粘贴文件
       ↓
将文件 Copy 到本地工作目录
~/.svnfilemanager/workcopies/{当前仓库名}/
       ↓
文件监控检测到变化（新增/修改）
       ↓
自动刷新 SVN 状态（已有 F3 文件监控流程）
       ↓
【如启用自动提交】→ 自动提交到 SVN 服务器（已有 F4 自动提交流程）
```

**F8.1 文件拖入 (Drop)**
- 从 OS 文件浏览器拖入文件/文件夹到 SVN File Manager
- 支持单个或批量拖入
- 支持拖入目录（递归复制所有文件）
- 拖入时显示复制进度
- 复制完成后文件监控自动触发后续流程

**F8.2 粘贴 (Paste)**
- 支持 Ctrl+V 粘贴来自 OS 文件浏览器的文件
- 自动识别剪贴板中的文件路径
- 复制到本地工作目录
- 后续流程同上

**F8.3 外部文件变化响应**
- 在 OS 文件浏览器中修改文件，文件监控仍能捕获
- 捕获后通过已有流程自动处理
- 侧边栏展示文件变更
- 行级差异高亮（增加/删除/修改）
- 支持图片文件 diff（暂不支持）

### 3.2 功能优先级

| 优先级 | 功能 | 说明 |
|--------|------|------|
| P0 | 文件浏览 + SVN 状态显示 | 核心功能 |
| P0 | 文件监控 + 自动刷新 | 核心差异化功能 |
| P0 | **OS 文件浏览器集成** | 拖入、粘贴、外部变化响应 |
| P1 | 基本 SVN 操作 (commit/update/add/delete) | 常用操作 |
| P1 | Diff 查看器 | 开发者强需求 |
| P1 | **自动提交 (Auto-Commit)** | 文件变化后自动提交 |
| P2 | 提交历史查看 | 增强功能 |
| P2 | 多仓库标签页 | 增强功能 |

---

## 4. 数据流设计

### 4.1 文件变化 → SVN 刷新流程

```
1. 用户修改文件 / 外部工具修改文件
       ↓
2. Rust notify 监听器捕获 IN_MODIFY 事件
       ↓
3. 防抖处理（300ms 内合并多次事件）
       ↓
4. 调用 svn status --xml --no-ignore
       ↓
5. 解析 XML 输出，生成文件状态列表
       ↓
6. 通过 Tauri IPC 发送到前端
       ↓
7. Zustand store 更新状态
       ↓
8. React 组件重新渲染
```

### 4.2 自动提交流程（Auto-Commit）

```
1. 文件变化检测触发
       ↓
2. 【可选】等待提交延迟（防抖，避免频繁触发）
       ↓
3. 检查距离上次提交的时间是否超过最小间隔
       ↓
   ├─ 否 → 跳过，等待下一次触发
   └─ 是 → 继续
       ↓
4. 调用 svn update（刷新最新版本，避免冲突）
       ↓
5. 检查是否有冲突 (C 状态)
       ↓
   ├─ 有冲突 →
   │    ├─ 配置为「仅无冲突时提交」→ 停止，通知用户
   │    └─ 配置为「冲突时通知我」→ 停止，弹出冲突处理对话框
   └─ 无冲突 → 继续
       ↓
6. 调用 svn status 获取变更文件列表
       ↓
7. 根据配置决定是否 svn add 新文件
       ↓
8. 调用 svn commit -m "{提交信息模板}"
       ↓
9. 通知用户提交成功
       ↓
10. 重置提交计时器
```

### 4.3 防抖与节流策略

| 场景 | 策略 | 说明 |
|------|------|------|
| 文件变化 → SVN 刷新 | 防抖 300ms | 避免短时间内多次刷新 UI |
| 自动提交触发 | 防抖 5s（可配置） | 等待文件操作完成 |
| 自动提交间隔 | 节流 30s（可配置） | 避免提交过于频繁 |
| SVN 命令执行 | 队列+重试 | 防止并发命令冲突 |

### 4.2 SVN 命令封装

```rust
// src/svn.rs

pub enum SvnCommand {
    Status,
    Info,
    Update,
    Commit { message: String },
    Add { path: String },
    Delete { path: String },
    Revert { path: String },
    Diff { path: String },
    Log { path: String, limit: u32 },
}

pub struct SvnResult {
    pub success: bool,
    pub output: String,
    pub error: String,
    pub exit_code: i32,
}
```

### 4.3 文件监控配置

```rust
// src/watcher.rs

pub struct WatcherConfig {
    pub enabled: bool,           // 是否启用监控
    pub debounce_ms: u64,        // 防抖延迟（毫秒）
    pub watch_depth: WatchDepth, // 监控深度
}

pub enum WatchDepth {
    Root,       // 仅监控仓库根目录
    OneLevel,   // 监控当前目录一层
    Recursive,  // 递归监控所有子目录
}
```

---

## 5. 前端设计

### 5.1 页面布局

```
┌──────────────────────────────────────────────────────────┐
│  [菜单栏]  文件  |  仓库  |  SVN  |  视图  |  帮助       │
├──────────────────────────────────────────────────────────┤
│  [工具栏]  ← 后退 | 前进 | 刷新 | 提交 | 更新 | 设置      │
├─────────────┬────────────────────────────────────────────┤
│             │                                            │
│  [目录树]   │         [文件列表]                          │
│             │                                            │
│  ├─ src/    │   📄 main.ts      M  (修改)               │
│  │  ├─ ...  │   📄 utils.ts     A  (新增)               │
│  └─ tests/  │   📄 app.tsx      ?  (未追踪)             │
│             │                                            │
├─────────────┴────────────────────────────────────────────┤
│  [状态栏]  工作副本: /path/to/repo  |  版本: r1234       │
└──────────────────────────────────────────────────────────┘
```

### 5.2 组件列表

| 组件 | 说明 |
|------|------|
| `AppShell` | 主布局容器 |
| `MenuBar` | 顶部菜单栏 |
| `Toolbar` | 工具栏（操作按钮） |
| `Sidebar` | 左侧目录树 |
| `FileList` | 文件列表（支持网格/列表切换） |
| `FileItem` | 单个文件项（含状态图标） |
| `ContextMenu` | 右键菜单 |
| `StatusBar` | 底部状态栏 |
| `SvnStatusBadge` | SVN 状态徽章（M/A/D/C/?） |
| `DiffViewer` | 变更查看器 |
| `CommitDialog` | 提交对话框 |
| `AddRepoDialog` | 添加仓库对话框 |
| `SettingsDialog` | 设置对话框（仓库管理/SVN配置） |
| `DropZone` | 拖放区域组件（接收外部文件拖入） |
| `ClipboardHandler` | 剪贴板监听与处理组件 |

### 5.4 右键菜单设计

#### 5.4.1 文件项右键菜单

所有 SVN 操作集成到右键菜单中：

```
文件项右键菜单
├── 基础操作
│   ├── 打开
│   ├── 复制路径
│   ├── 重命名
│   └── 删除
│
├── SVN 操作
│   ├── 提交 (Commit)          [M]  提交此文件的更改
│   ├── 更新 (Update)           [D]  更新此文件（仅目录）
│   ├── 添加到版本控制 (Add)    [?]  将文件加入 SVN 追踪
│   ├── 从版本控制删除 (Delete) [A]  从 SVN 删除（保留本地文件）
│   ├── 撤销更改 (Revert)       [M]  撤销此文件的本地更改
│   └── 查看变更 (Diff)         [M]  查看此文件的详细差异
│
└── SVN 信息
    ├── 查看状态
    └── 查看历史
```

#### 5.4.2 目录项 vs 文件项的菜单差异

| 操作 | 文件 | 目录 |
|------|:----:|:----:|
| 打开 | ✅ | ✅  进入目录 |
| 复制路径 | ✅ | ✅ |
| 重命名 | ✅ | ✅ |
| 删除 | ✅ | ✅ |
| Commit | ✅ | ✅ |
| Update | ❌ | ✅ |
| Add | ✅ | ✅ |
| Delete | ✅ | ✅ |
| Revert | ✅ | ✅ |
| Diff | ✅ | ❌ |
| 查看历史 | ✅ | ✅ |

#### 5.4.3 多选时的菜单

当选中多个文件/目录时：

```
多选右键菜单
├── 提交选中项 (Commit)      [M]  提交所有选中的已追踪文件
├── 添加选中项 (Add)          [?]  将所有选中的未追踪文件加入 SVN
├── 撤销选中项 (Revert)       [M]  撤销所有选中的已修改文件
├── 删除选中项 (Delete)       [A]  从 SVN 删除选中的文件
└── 复制选中项路径            复制所有选中项的路径
```

#### 5.4.4 右键菜单组件定义

```typescript
// components/ContextMenu.tsx

interface ContextMenuProps {
  x: number;                    // 菜单位置（屏幕坐标）
  y: number;
  target: FileEntry | FileEntry[];  // 选中目标
  onAction: (action: MenuAction) => void;
}

type MenuAction = 
  | 'open'
  | 'copyPath'
  | 'rename'
  | 'delete'
  | 'svnCommit'
  | 'svnUpdate'
  | 'svnAdd'
  | 'svnDelete'
  | 'svnRevert'
  | 'svnDiff'
  | 'svnStatus'
  | 'svnLog';

// 菜单项数据结构
interface MenuItem {
  id: MenuAction;
  label: string;
  icon?: ReactNode;
  shortcut?: string;           // 快捷键提示
  enabled: boolean;           // 是否可用
  danger?: boolean;           // 危险操作（红色高亮）
  separator?: boolean;        // 分隔线
}
```

#### 5.4.5 快捷键支持

| 操作 | 快捷键 |
|------|--------|
| 提交 | `Ctrl + Shift + C` |
| 更新 | `Ctrl + U` |
| 添加 | `Ctrl + A` |
| 撤销 | `Ctrl + Shift + Z` |
| 查看 Diff | `Ctrl + D` |
| 重命名 | `F2` |
| 删除 | `Delete` |

### 5.6 数据存储

#### 5.6.1 程序数据目录

```
~/.svnfilemanager/
├── config.json              # 全局配置（窗口大小、最后打开的仓库等）
├── repositories.json        # 仓库列表配置
├── settings.json            # 用户偏好设置
├── auto-commit-templates.json  # 提交信息模板历史
├── logs/
│   └── svnfilemanager-{date}.log  # 运行日志
├── cache/
│   └── file-status-cache.json    # 文件状态缓存（加速启动）
└── workcopies/              # 所有工作副本统一存放目录
    ├── my-project/          # 仓库1的工作副本
    │   ├── .svn/
    │   └── (项目文件...)
    ├── another-repo/        # 仓库2的工作副本
    │   ├── .svn/
    │   └── (项目文件...)
    └── ...
```

| 文件 | 说明 |
|------|------|
| `config.json` | 程序全局配置 |
| `repositories.json` | 已配置仓库列表及元数据 |
| `settings.json` | 用户偏好（视图、SVN路径、自动提交配置等） |
| `auto-commit-templates.json` | 提交模板历史记录 |
| `logs/` | 日志文件 |
| `cache/` | 缓存文件 |

#### 5.6.2 配置文件结构

```json
// repositories.json
{
  "repositories": [
    {
      "id": "uuid-1",
      "name": "my-project",
      "url": "svn+ssh://example.com/repos/my-project",
      "username": "devuser",
      "password": "encrypted:xxxxx",
      "isDefault": true,
      "lastSynced": "2026-04-05T13:50:00Z",
      "enabled": true,
      "rememberPassword": true
      // 工作副本路径: ~/.svnfilemanager/workcopies/my-project/
    }
  ]
}
```

**安全说明：**
- 密码采用系统密钥链（Keychain/Credential Manager）存储，不以明文保存
- `file://` 协议本地仓库无需认证
- `svn+ssh://` 协议可使用 SSH 密钥认证

```json
// config.json
{
  "lastActiveRepositoryId": "uuid-1",   // 上次活动的仓库 ID
  "windowBounds": {
    "x": 100,
    "y": 100,
    "width": 1200,
    "height": 800
  },
  "firstRun": false
}
```

```json
// settings.json
{
  "svnPath": "svn",
  "autoRefresh": true,
  "refreshInterval": 3000,
  "showHiddenFiles": false,
  "showUnversioned": true,
  "autoSync": true,
  "autoSyncInterval": 600000,      // 10分钟（毫秒）
  "autoCommit": false,
  "autoCommitDelay": 5000,
  "autoCommitInterval": 30000,
  "autoCommitMessage": "Auto-commit: {date} {time}",
  "autoCommitOnNoConflict": true,
  "autoCommitNotifyOnConflict": true,
  "autoCommitAddNewFiles": false,
  "autoCommitExcludePatterns": ["*.log", "*.tmp", "node_modules/"]
}
```

### 5.7 主窗口菜单栏

#### 5.7.1 菜单结构

```
文件 (File)
├── 新建标签页              Ctrl + T
├── 关闭标签页              Ctrl + W
├── ─────────────────────
├── 打开文件夹              Ctrl + O
├── 打开 SVN 仓库          Ctrl + Shift + O
├── ─────────────────────
├── 偏好设置                Ctrl + ,
└── 退出                    Alt + F4

仓库 (Repository)
├── 添加仓库...             Ctrl + Shift + A
├── 移除仓库                Delete
├── ─────────────────────
├── **切换仓库**             Ctrl + Tab
│   └─ 子菜单显示所有已配置的仓库，当前活动仓库标记 ★
├── ─────────────────────
├── 刷新状态                F5
├── 清理工作副本...          svn cleanup
├── ─────────────────────
├── 查看历史                Ctrl + L
└── 仓库属性...

SVN 操作
├── 提交                    Ctrl + Shift + C
├── 更新                    Ctrl + U
├── 更新到版本...           Ctrl + Shift + T
├── ─────────────────────
├── 添加到版本控制          Ctrl + A
├── 从版本控制删除          Ctrl + Shift + D
├── ─────────────────────
├── 撤销更改                Ctrl + Shift + Z
├── 解决冲突...
├── ─────────────────────
├── **定时自动同步**         Ctrl + Shift + Y     ← 新增
│   └─ 勾选后每10分钟自动 update
├── 自动同步设置...
├── ─────────────────────
├── **启用自动提交**         Ctrl + Shift + G
├── 自动提交设置...
├── ─────────────────────
├── 导出...                 svn export
└── 创建分支/标签...        svn copy

视图 (View)
├── 切换目录树              Ctrl + B
├── 切换状态栏              Ctrl + S
├── ─────────────────────
├── 网格视图                Ctrl + 1
├── 列表视图                Ctrl + 2
├── ─────────────────────
├── 显示隐藏文件             Ctrl + H
├── 显示 SVN 状态图标
└── 显示文件大小

帮助 (Help)
├── 文档                    F1
├── 快捷键列表
├── ─────────────────────
├── 检查更新...
└── 关于
```

#### 5.7.2 仓库管理面板

**添加仓库对话框：**

```
┌─────────────────────────────────────────────────────────┐
│                   添加 SVN 仓库                           │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  仓库名称:  [____________________]  例: my-project       │
│                                                         │
│  仓库地址:  [____________________]                       │
│              支持: file://, svn://, svn+ssh://,         │
│                    http://, https://                    │
│                                                         │
│  ─────────────────────────────────────────────────────  │
│  认证信息 (可选):                                        │
│                                                         │
│  用户名:  [____________________]                        │
│                                                         │
│  密码:    [____________________]  ☐ 显示密码            │
│                                                         │
│  ─────────────────────────────────────────────────────  │
│  工作副本将自动创建于:                                    │
│  ~/.svnfilemanager/workcopies/{仓库名称}/                │
│  (首次切换到该仓库时自动 checkout)                       │
│                                                         │
│  ┌─────────────────────────────────────────────────┐   │
│  │  ☑ 启用文件监控                                  │   │
│  │  ☑ 启动时自动刷新状态                            │   │
│  │  ☐ 设置为默认仓库                               │   │
│  │  ☐ 记住密码                                     │   │
│  └─────────────────────────────────────────────────┘   │
│                                                         │
│                    [取消]         [添加]                │
└─────────────────────────────────────────────────────────┘
```

**仓库列表（设置面板）：**

```
┌─────────────────────────────────────────────────────┐
│                    仓库管理                          │
├─────────────────────────────────────────────────────┤
│                                                     │
│  已配置的仓库:                          [当前活动]    │
│                                                     │
│  ★ my-project (活动)       /home/user/my-project   │
│    最后同步: 2026-04-05 13:50         [编辑] [移除]  │
│    状态: 监控中 | 自动提交: 开启                    │
│                                                     │
│    another-repo             /home/user/another       │
│    最后同步: 2026-04-04 09:20         [切换] [编辑] [移除]  │
│                                                     │
│  [+ 添加仓库]                                       │
│                                                     │
│  ─────────────────────────────────────────────────  │
│  提示: 同时只能有一个活动仓库用于监控和操作           │
│                                                     │
├─────────────────────────────────────────────────────┤
│                    SVN 配置                          │
├─────────────────────────────────────────────────────┤
│                                                     │
│  SVN 客户端路径:  [/usr/bin/svn]  [自动检测]        │
│                                                     │
│  ┌─────────────────────────────────┐               │
│  │  ☑ 启用自动刷新                  │               │
│  │  刷新间隔:  [3000] ms            │               │
│  │  ☑ 显示未追踪文件                │               │
│  │  ☑ 显示忽略文件                  │               │
│  │  ☐ 启用 SSH 隧道 (需要配置)      │               │
│  └─────────────────────────────────┘               │
│                                                     │
├─────────────────────────────────────────────────────┤
│                定时自动同步 (Auto-Sync)              │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ┌─────────────────────────────────┐               │
│  │  ☑ 启用定时自动同步              │               │
│  │                                     │               │
│  │  同步间隔:  [10] 分钟  (1-60)      │               │
│  │                                     │               │
│  │  状态栏显示同步进度                 │               │
│  └─────────────────────────────────┘               │
│                                                     │
├─────────────────────────────────────────────────────┤
│                自动提交配置 (Auto-Commit)            │
├─────────────────────────────────────────────────────┤
│                                                     │
│  ┌─────────────────────────────────┐               │
│  │  ☐ 启用自动提交                  │               │
│  │                                     │               │
│  │  提交延迟:  [5000] ms  (检测到变化后等待)  │               │
│  │  提交间隔:  [30000] ms (两次提交最小间隔)  │               │
│  │                                     │               │
│  │  提交信息模板:                          │               │
│  │  [Auto-commit: {date} {time}]          │               │
│  │                                     │               │
│  │  ☐ 仅在无冲突时自动提交              │               │
│  │  ☐ 冲突时通知我                      │               │
│  │  ☐ 自动添加新文件                    │               │
│  │                                     │               │
│  │  排除模式 (glob):                       │               │
│  │  [*.log] [*.tmp] [node_modules/]        │               │
│  └─────────────────────────────────┘               │
│                                                     │
│                    [保存]                           │
└─────────────────────────────────────────────────────┘
```

#### 5.7.3 组件定义

```typescript
// components/MenuBar.tsx

interface MenuBarProps {
  onAction: (action: MenuAction) => void;
}

// stores/settingsStore.ts

interface SettingsState {
  repositories: Repository[];       // 配置的仓库列表（可配置多个）
  activeRepositoryId: string | null; // 当前活动的仓库（仅一个）
  svnPath: string;                  // SVN CLI 路径
  autoRefresh: boolean;
  refreshInterval: number;           // ms
  showHiddenFiles: boolean;
  showUnversioned: boolean;
  
  // 自动同步配置
  autoSync: boolean;                // 是否启用定时自动同步
  autoSyncInterval: number;         // 自动同步间隔（毫秒），默认 600000 (10分钟)
  
  // Auto-Commit 配置（应用于活动仓库）
  autoCommit: boolean;               // 是否启用自动提交
  autoCommitDelay: number;           // 触发后延迟 ms
  autoCommitInterval: number;        // 两次提交最小间隔 ms
  autoCommitMessage: string;          // 提交信息模板
  autoCommitOnNoConflict: boolean;   // 仅无冲突时提交
  autoCommitNotifyOnConflict: boolean; // 冲突时通知
  autoCommitAddNewFiles: boolean;   // 自动添加新文件
  autoCommitExcludePatterns: string[]; // 排除的 glob 模式
  
  // Actions
  addRepository: (repo: Repository) => void;
  removeRepository: (id: string) => void;
  updateRepository: (id: string, repo: Partial<Repository>) => void;
  /**
   * 切换活动仓库
   * - 切换时会停止当前仓库的文件监控
   * - 启动新仓库的监控
   * - 如果启用了自动提交，则新仓库继承配置
   * - 自动执行 svn update 与服务器同步
   */
  setActiveRepository: (id: string) => void;
  updateSettings: (settings: Partial<Settings>) => void;
}

interface Repository {
  id: string;
  name: string;                    // 显示名称（也是工作副本目录名）
  url: string;                    // 仓库地址 (svn://, http://, file:// 等)
  username?: string;              // 认证用户名
  password?: string;              // 认证密码（加密存储）
  isDefault: boolean;
  lastSynced: Date | null;
  enabled: boolean;
  rememberPassword: boolean;     // 是否记住密码
  // 工作副本路径 = ~/.svnfilemanager/workcopies/{name}/
  // 仓库级别的自动提交覆盖
  autoCommitEnabled?: boolean;
  autoCommitExcludePatterns?: string[];
}
```

### 5.8 状态管理 (Zustand)

```typescript
// stores/repoStore.ts

interface RepoState {
  currentPath: string;
  entries: FileEntry[];
  selectedEntry: FileEntry | null;
  selectedEntries: FileEntry[];     // 多选支持
  svnStatus: Map<string, SvnStatus>;
  isWatching: boolean;
  
  // Actions
  setCurrentPath: (path: string) => void;
  refreshStatus: () => Promise<void>;
  toggleWatch: () => void;
  setSelectedEntry: (entry: FileEntry | null) => void;
  setSelectedEntries: (entries: FileEntry[]) => void;
}

interface FileEntry {
  name: string;
  path: string;
  isDirectory: boolean;
  size: number;
  modifiedAt: Date;
}

interface SvnStatus {
  status: 'modified' | 'added' | 'deleted' | 'unversioned' | 'conflict' | 'normal';
  propsStatus: string;
  revision?: number;
}
```

---

## 6. 项目结构

```
svn-file-manager/
├── src/                      # React 前端源码
│   ├── components/            # UI 组件
│   │   ├── AppShell.tsx
│   │   ├── Toolbar.tsx
│   │   ├── Sidebar.tsx
│   │   ├── FileList.tsx
│   │   ├── FileItem.tsx
│   │   ├── SvnStatusBadge.tsx
│   │   ├── DiffViewer.tsx
│   │   ├── CommitDialog.tsx
│   │   └── StatusBar.tsx
│   ├── stores/               # Zustand 状态管理
│   │   └── repoStore.ts
│   ├── hooks/                # 自定义 React hooks
│   │   ├── useSvnStatus.ts
│   │   └── useFileWatcher.ts
│   ├── utils/                # 工具函数
│   │   └── format.ts
│   ├── App.tsx
│   ├── main.tsx
│   └── index.css
├── src-tauri/                 # Rust 后端源码
│   ├── src/
│   │   ├── main.rs           # Tauri 入口
│   │   ├── commands/         # Tauri 命令
│   │   │   ├── mod.rs
│   │   │   ├── fs.rs         # 文件系统命令
│   │   │   └── svn.rs        # SVN 命令
│   │   ├── watcher.rs        # 文件监控
│   │   └── svn.rs            # SVN 封装
│   ├── Cargo.toml
│   └── tauri.conf.json
├── package.json
├── tsconfig.json
├── vite.config.ts
├── tailwind.config.js
├── SPEC.md                   # 本文档
└── README.md
```

---

## 7. Rust 后端命令接口

### 7.1 Tauri Commands

```rust
// 文件系统
#[tauri::command]
fn read_directory(path: String) -> Result<Vec<FileEntry>, String>

#[tauri::command]
fn get_file_info(path: String) -> Result<FileEntry, String>

// 剪贴板操作
#[tauri::command]
fn get_clipboard_files() -> Result<Vec<String>, String>  // 获取剪贴板中的文件路径

#[tauri::command]
fn copy_path_to_clipboard(path: String) -> Result<(), String>  // 复制路径到剪贴板

// SVN 操作
#[tauri::command]
fn svn_status(path: String) -> Result<SvnStatusResult, String>

#[tauri::command]
fn svn_info(path: String) -> Result<SvnInfo, String>

#[tauri::command]
fn svn_update(path: String) -> Result<SvnUpdateResult, String>

#[tauri::command]
fn svn_commit(path: String, message: String) -> Result<SvnCommitResult, String>

#[tauri::command]
fn svn_add(path: String) -> Result<(), String>

#[tauri::command]
fn svn_delete(path: String) -> Result<(), String>

#[tauri::command]
fn svn_revert(path: String) -> Result<(), String>

#[tauri::command]
fn svn_diff(path: String) -> Result<String, String>

#[tauri::command]
fn svn_log(path: String, limit: u32) -> Result<Vec<SvnLogEntry>, String>

// 文件监控
#[tauri::command]
fn start_watching(path: String, config: WatcherConfig) -> Result<(), String>

#[tauri::command]
fn stop_watching() -> Result<(), String>

// 拖放操作 - 将文件复制到工作目录
#[tauri::command]
fn copy_files_to_workdir(source_paths: Vec<String>, target_dir: String) -> Result<CopyResult, String>

struct CopyResult {
    pub copied_count: u32,
    pub failed_paths: Vec<String>,  // 复制失败的文件
}
```

### 7.2 事件推送

```rust
// 文件变化时主动推送到前端
#[tauri::command]
fn emit_file_changed(event: FileChangedEvent)

// 前端监听
window.addEventListener('file-changed', (event) => {
    // 刷新 SVN 状态
})
```

---

## 8. 依赖版本

### 前端

| 依赖 | 版本 | 用途 |
|------|------|------|
| react | ^18.2.0 | UI 框架 |
| react-dom | ^18.2.0 | React DOM |
| typescript | ^5.3.0 | 类型系统 |
| vite | ^5.0.0 | 构建工具 |
| @tauri-apps/api | ^2.0.0 | Tauri 前端 API |
| zustand | ^4.4.0 | 状态管理 |
| tailwindcss | ^3.4.0 | CSS 框架 |
| @radix-ui/* | ^1.0.0 | UI 组件库 |

### Rust 后端

| 依赖 | 版本 | 用途 |
|------|------|------|
| tauri | 2.0 | 桌面框架 |
| notify | 6.1 | 文件监控 |
| serde | 1.0 | 序列化 |
| serde_json | 1.0 | JSON 解析 |
| tokio | 1.0 | 异步运行时 |
| quick-xml | 0.31 | XML 解析（SVN status 输出） |

---

## 9. 开发计划

### Phase 1: 基础框架 (1-2 周)
- [x] 项目初始化 (Tauri + React)
- [x] 基础 UI 布局
- [x] 目录浏览功能
- [x] SVN status 调用

### Phase 2: 核心功能 (2-3 周)
- [ ] 文件监控 + 自动刷新
- [ ] SVN 状态图标显示
- [ ] 基本 SVN 操作（commit/update/add/delete）
- [ ] Diff 查看器

### Phase 3: 增强功能 (2 周)
- [ ] 提交历史查看
- [ ] 多标签页支持
- [ ] 设置面板
- [ ] 打包发布

---

## 10. 风险与挑战

| 风险 | 影响 | 应对措施 |
|------|------|----------|
| SVN CLI 不可用 | 高 | 启动时检测，显示安装指引 |
| 大仓库性能问题 | 中 | 分层加载、懒加载、增量更新 |
| 文件锁冲突 | 中 | 监控 svn lock 状态，提示用户 |
| Windows/macOS/Linux SVN 行为差异 | 中 | 使用 `--xml` 输出统一解析 |

---

## 11. 测试策略

| 测试类型 | 覆盖内容 |
|----------|----------|
| 单元测试 | SVN 命令解析、防抖逻辑 |
| 集成测试 | Tauri IPC 调用、文件监控 |
| E2E 测试 | 完整用户操作流程 |
| 手动测试 | 各平台 SVN 命令兼容性 |

---

## 12. 参考资料

- [Tauri 2.0 文档](https://tauri.app/)
- [Rust notify 库](https://docs.rs/notify/latest/notify/)
- [SVN 命令行文档](https://svnbook.red-bean.com/)
- [React 最佳实践](https://react.dev/learn)

---

*文档版本: 2.2.0*  
*创建日期: 2026-04-05*  
*更新日期: 2026-04-05*
