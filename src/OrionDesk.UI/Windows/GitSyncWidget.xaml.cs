using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Media = System.Windows.Media;
using OrionDesk.BLL.Models;
using OrionDesk.BLL.Services;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// Git 同步监控组件
    /// 自动扫描指定目录下的 git 仓库，显示与远程的同步状态
    /// </summary>
    public partial class GitSyncWidget : BaseWidgetWindow
    {
        private readonly GitSyncService _gitService;
        private readonly DispatcherTimer _refreshTimer;
        private GitSyncSettings _settings;
        private List<GitRepoStatus> _repoStatuses = new();
        private List<DiscoveredDir> _nonGitDirs = new();
        private bool _isChecking = false;

        public GitSyncWidget(WidgetConfig config, WidgetManager widgetManager)
            : base(config, widgetManager)
        {
            AcceptFileDrop = true; // 启用 Win32 文件拖放（绕过 WorkerW z-order 限制）
            InitializeComponent();

            _gitService = new GitSyncService();
            _settings = LoadSettings();

            // 初始化定时器
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(_settings.RefreshMinutes)
            };
            _refreshTimer.Tick += (s, e) => RefreshAll();

            // 初始化
            LoadLockState();
            UpdateLockButton();

            // 首次检查
            RefreshAll();
            _refreshTimer.Start();
        }

        #region 设置

        private GitSyncSettings LoadSettings()
        {
            var settings = new GitSyncSettings();

            if (_config.Settings.TryGetValue("scanPath", out var sp))
                settings.ScanPath = sp.ToString() ?? "";

            // 刷新频率从全局设置读取
            settings.RefreshMinutes = _widgetManager.Settings.GitSyncRefreshMinutes;

            // 加载额外仓库
            if (_config.Settings.TryGetValue("extraRepos", out var extra) &&
                extra is System.Text.Json.JsonElement je &&
                je.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in je.EnumerateArray())
                {
                    var path = item.GetString();
                    if (!string.IsNullOrWhiteSpace(path))
                        settings.ExtraRepos.Add(path);
                }
            }

            return settings;
        }

        private void SaveSettings()
        {
            _config.Settings["scanPath"] = _settings.ScanPath;
            _config.Settings["extraRepos"] = _settings.ExtraRepos.ToArray();

            if (!_widgetManager.IsRestoring)
            {
                try { _widgetManager.Save(); }
                catch (Exception ex) { Debug.WriteLine($"[GitSync] 保存设置失败: {ex.Message}"); }
            }
        }

        #endregion

        #region 核心逻辑

        /// <summary>
        /// 刷新所有仓库状态
        /// </summary>
        private async void RefreshAll()
        {
            if (_isChecking) return;
            _isChecking = true;

            try
            {
                // 收集所有仓库路径
                var allRepos = new List<string>();

                // 自动扫描（发现所有子目录，含非 git 目录）
                var discoveredDirs = new List<DiscoveredDir>();
                if (!string.IsNullOrWhiteSpace(_settings.ScanPath))
                {
                    discoveredDirs = _gitService.DiscoverDirs(_settings.ScanPath);
                }

                // 收集 VCS 仓库（扫描到的 + 额外添加的）
                var vcsDirs = discoveredDirs.Where(d => d.VcsType != VcsType.None).ToList();
                var vcsPaths = vcsDirs.Select(d => d.Path).ToList();
                foreach (var extra in _settings.ExtraRepos)
                {
                    if (!vcsPaths.Contains(extra, StringComparer.OrdinalIgnoreCase))
                    {
                        // 额外仓库也识别 VCS 类型
                        var isSvn = Directory.Exists(Path.Combine(extra, ".svn"));
                        vcsDirs.Add(new DiscoveredDir { Path = extra, Name = Path.GetFileName(extra), VcsType = isSvn ? VcsType.Svn : VcsType.Git });
                    }
                }

                // 非 VCS 目录
                var nonVcsDirs = discoveredDirs.Where(d => d.VcsType == VcsType.None).ToList();

                // 更新标题
                if (string.IsNullOrWhiteSpace(_settings.ScanPath))
                {
                    HeaderText.Text = "拖入文件夹路径开始扫描";
                }
                else
                {
                    var scanName = Path.GetFileName(_settings.ScanPath.TrimEnd('\\', '/'));
                    var gitCount = vcsDirs.Count(d => d.VcsType == VcsType.Git);
                    var svnCount = vcsDirs.Count(d => d.VcsType == VcsType.Svn);
                    var countText = svnCount > 0
                        ? $"{gitCount}Git + {svnCount}SVN"
                        : $"{vcsPaths.Count}个仓库";
                    HeaderText.Text = $"📂 {scanName} ({countText})";
                    HeaderText.ToolTip = _settings.ScanPath;
                }

                if (vcsPaths.Count == 0 && nonVcsDirs.Count == 0)
                {
                    RepoPanel.Children.Clear();
                    AddMessageRow("未发现目录", "请拖入包含项目的文件夹");
                    return;
                }

                // 批量检查仓库状态（自动识别 Git/SVN）
                var statuses = await Task.Run(() => _gitService.CheckAllAsync(vcsDirs));
                _repoStatuses = statuses;
                _nonGitDirs = nonVcsDirs;

                // 更新 UI
                RefreshView();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GitSync] 刷新失败: {ex.Message}");
            }
            finally
            {
                _isChecking = false;
            }
        }

        /// <summary>
        /// 根据状态列表重建 UI
        /// </summary>
        private void RefreshView()
        {
            RepoPanel.Children.Clear();

            // 按状态排序：异常 > 分歧 > 待拉 > 待推 > 已同步 > 无远程
            var sorted = _repoStatuses.OrderBy(r => r.Status switch
            {
                GitSyncStatus.Error => 0,
                GitSyncStatus.Diverged => 1,
                GitSyncStatus.Behind => 2,
                GitSyncStatus.Ahead => 3,
                GitSyncStatus.NoRemote => 4,
                GitSyncStatus.Synced => 5,
                _ => 6
            }).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                var repo = sorted[i];

                // 分隔线（第一个之前不加）
                if (i > 0)
                {
                    RepoPanel.Children.Add(new Border
                    {
                        Background = new Media.SolidColorBrush(Media.Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)),
                        Height = 1,
                        Margin = new Thickness(0, 4, 0, 4)
                    });
                }

                AddRepoRow(repo);
            }

            // 非 git 目录（灰色显示，表示不是仓库）
            if (_nonGitDirs.Count > 0)
            {
                // 分隔标题
                if (sorted.Count > 0)
                {
                    RepoPanel.Children.Add(new Border
                    {
                        Background = new Media.SolidColorBrush(Media.Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)),
                        Height = 1,
                        Margin = new Thickness(0, 6, 0, 6)
                    });
                }

                var header = new TextBlock
                {
                    Text = $"非 VCS 目录 ({_nonGitDirs.Count})",
                    Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0x66, 0x66, 0x66)),
                    FontSize = 10,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                RepoPanel.Children.Add(header);

                foreach (var dir in _nonGitDirs)
                {
                    AddNonGitRow(dir);
                }
            }
        }

        /// <summary>
        /// 添加单个仓库的状态行
        /// </summary>
        private void AddRepoRow(GitRepoStatus repo)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };

            // 第一行：仓库名 + 状态
            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var vcsTag = repo.VcsType == VcsType.Svn ? "[SVN] " : "[Git] ";
            var nameText = new TextBlock
            {
                Text = $"📂 {vcsTag}{repo.RepoName}",
                Style = (Style)FindResource("RepoNameStyle"),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"点击打开 {repo.RepoPath}"
            };
            nameText.MouseLeftButtonDown += (s, e) => OpenFolder(repo.RepoPath);
            Grid.SetColumn(nameText, 0);
            headerRow.Children.Add(nameText);

            // 状态颜色
            var statusColor = repo.Status switch
            {
                GitSyncStatus.Synced => Media.Color.FromRgb(0x30, 0xBB, 0x43),
                GitSyncStatus.Ahead => Media.Color.FromRgb(0xFF, 0xB9, 0x00),
                GitSyncStatus.Behind => Media.Color.FromRgb(0x00, 0x99, 0xFF),
                GitSyncStatus.Diverged => Media.Color.FromRgb(0xFF, 0x6B, 0x6B),
                GitSyncStatus.NoRemote => Media.Color.FromRgb(0x88, 0x88, 0x88),
                GitSyncStatus.Error => Media.Color.FromRgb(0xFF, 0x6B, 0x6B),
                _ => Media.Colors.White
            };

            var statusText = new TextBlock
            {
                Text = repo.StatusText,
                Foreground = new Media.SolidColorBrush(statusColor),
                FontSize = 11,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            Grid.SetColumn(statusText, 1);
            headerRow.Children.Add(statusText);

            panel.Children.Add(headerRow);

            // 第二行：分支名（Git）或仓库路径（SVN）
            if (!string.IsNullOrEmpty(repo.Branch))
            {
                var branchIcon = repo.VcsType == VcsType.Svn ? "🔗" : "🌿";
                var branchText = new TextBlock
                {
                    Text = $"{branchIcon} {repo.Branch}",
                    Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0xAA, 0xCC, 0xFF)),
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                panel.Children.Add(branchText);
            }

            // 第三行：最新提交信息
            if (!string.IsNullOrEmpty(repo.LastCommitMessage))
            {
                var commitRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };

                var hashText = new TextBlock
                {
                    Text = repo.LastCommitHash,
                    Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0x88, 0x88, 0x88)),
                    FontSize = 10,
                    FontFamily = new Media.FontFamily("Consolas")
                };
                commitRow.Children.Add(hashText);

                var msgText = new TextBlock
                {
                    Text = $" {repo.LastCommitMessage}",
                    Style = (Style)FindResource("CommitStyle"),
                    MaxWidth = 180
                };
                commitRow.Children.Add(msgText);

                panel.Children.Add(commitRow);

                // 提交时间和作者
                if (!string.IsNullOrEmpty(repo.LastCommitTime))
                {
                    var timeText = new TextBlock
                    {
                        Text = $"{repo.LastCommitTime}  {repo.LastCommitAuthor}",
                        Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0x66, 0x66, 0x66)),
                        FontSize = 10,
                        Margin = new Thickness(0, 1, 0, 0)
                    };
                    panel.Children.Add(timeText);
                }
            }

            RepoPanel.Children.Add(panel);
        }

        /// <summary>
        /// 添加非 VCS 目录行（灰色标识）
        /// </summary>
        private void AddNonGitRow(DiscoveredDir dir)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };

            var nameText = new TextBlock
            {
                Text = $"📁 {dir.Name}",
                Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0x66, 0x66, 0x66)),
                FontSize = 12,
                FontFamily = new Media.FontFamily("Segoe UI"),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"点击打开 {dir.Path}"
            };
            nameText.MouseLeftButtonDown += (s, e) => OpenFolder(dir.Path);
            panel.Children.Add(nameText);

            var tagText = new TextBlock
            {
                Text = "无仓库",
                Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0x55, 0x55, 0x55)),
                FontSize = 10,
                Margin = new Thickness(0, 1, 0, 0)
            };
            panel.Children.Add(tagText);

            RepoPanel.Children.Add(panel);
        }

        /// <summary>
        /// 添加提示消息行
        /// </summary>
        private void AddMessageRow(string title, string subtitle = "")
        {
            var panel = new StackPanel
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            panel.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0x88, 0x88, 0x88)),
                FontSize = 12,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            });
            if (!string.IsNullOrEmpty(subtitle))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = subtitle,
                    Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0x66, 0x66, 0x66)),
                    FontSize = 11,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }
            RepoPanel.Children.Add(panel);
        }

        #endregion

        #region 拖拽

        /// <summary>
        /// 用资源管理器打开文件夹
        /// </summary>
        private static void OpenFolder(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = path,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GitSync] 打开文件夹失败: {ex.Message}");
            }
        }

        /// <summary>
        /// Win32 文件拖放下事件（绕过 WorkerW z-order 限制）
        /// </summary>
        protected override void OnFileDrop(string[] files)
        {
            if (files.Length > 0)
            {
                var path = files[0];

                if (Directory.Exists(path))
                {
                    // 检查是否是 VCS 仓库（.git 或 .svn）
                    var isGit = Directory.Exists(Path.Combine(path, ".git"));
                    var isSvn = Directory.Exists(Path.Combine(path, ".svn"));

                    if (isGit || isSvn)
                    {
                        // 本身是仓库，添加到额外列表
                        if (!_settings.ExtraRepos.Contains(path, StringComparer.OrdinalIgnoreCase))
                        {
                            _settings.ExtraRepos.Add(path);
                            SaveSettings();
                            RefreshAll();
                        }
                    }
                    else
                    {
                        // 是目录但不是仓库，设为扫描路径
                        _settings.ScanPath = path;
                        SaveSettings();
                        RefreshAll();
                    }
                }
            }
        }

        #endregion

        #region 右键菜单

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshAll();
        }

        private void SetScanPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择 Git 项目扫描目录",
                ShowNewFolderButton = false
            };

            if (!string.IsNullOrEmpty(_settings.ScanPath))
                dialog.InitialDirectory = _settings.ScanPath;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _settings.ScanPath = dialog.SelectedPath;
                _settings.ExtraRepos.Clear(); // 切换扫描路径时清空额外仓库
                SaveSettings();
                RefreshAll();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => RequestClose();

        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            ToggleLock();
            UpdateLockButton();
        }

        private void UpdateLockButton()
        {
            LockButton.Content = IsLocked ? "🔒" : "🔓";
            LockButton.ToolTip = IsLocked ? "解锁" : "锁定";
            LockMenuItem.IsChecked = IsLocked;
            LockMenuItem.Header = IsLocked ? "解锁" : "锁定";
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
