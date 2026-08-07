using System.Diagnostics;

namespace OrionDesk.BLL.Services
{
    /// <summary>
    /// 版本控制同步监控服务
    /// 通过 git/svn CLI 检查本地仓库与远程仓库的同步状态
    /// </summary>
    public class GitSyncService
    {
        private const int DefaultTimeoutMs = 15000; // 命令超时 15 秒

        /// <summary>
        /// 扫描指定目录，返回所有子目录（标记是否为 git 仓库）
        /// </summary>
        /// <param name="scanPath">扫描根目录</param>
        /// <returns>发现的目录列表（含 git 标记）</returns>
        public List<DiscoveredDir> DiscoverDirs(string scanPath)
        {
            var dirs = new List<DiscoveredDir>();

            if (string.IsNullOrWhiteSpace(scanPath) || !Directory.Exists(scanPath))
                return dirs;

            try
            {
                // 只扫描一层深度：scanPath/项目名
                foreach (var dir in Directory.GetDirectories(scanPath))
                {
                    try
                    {
                        var isGit = Directory.Exists(Path.Combine(dir, ".git"))
                                 || File.Exists(Path.Combine(dir, ".git"));
                        var isSvn = Directory.Exists(Path.Combine(dir, ".svn"));
                        var vcsType = isGit ? VcsType.Git : (isSvn ? VcsType.Svn : VcsType.None);
                        dirs.Add(new DiscoveredDir
                        {
                            Path = dir,
                            Name = Path.GetFileName(dir),
                            VcsType = vcsType
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 无权限，跳过
                    }
                    catch
                    {
                        // 其他错误，跳过
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine($"[GitSync] 无权限访问: {scanPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GitSync] 扫描目录失败: {ex.Message}");
            }

            return dirs;
        }

        /// <summary>
        /// 扫描指定目录，只返回包含 .git 的仓库路径（兼容旧调用）
        /// </summary>
        public List<string> DiscoverRepos(string scanPath)
        {
            return DiscoverDirs(scanPath)
                .Where(d => d.VcsType != VcsType.None)
                .Select(d => d.Path)
                .ToList();
        }

        /// <summary>
        /// 检查单个仓库的同步状态
        /// </summary>
        public async Task<GitRepoStatus> CheckRepoAsync(string repoPath)
        {
            var status = new GitRepoStatus
            {
                RepoPath = repoPath,
                RepoName = Path.GetFileName(repoPath)
            };

            try
            {
                // 检查是否是 git 仓库
                if (!IsGitRepo(repoPath))
                {
                    status.Error = "非Git仓库";
                    return status;
                }

                // 检查 git 是否可用
                var gitVersion = await RunGitAsync(repoPath, "--version");
                if (gitVersion == null)
                {
                    status.Error = "未检测到Git";
                    return status;
                }

                // 获取当前分支
                var branch = await RunGitAsync(repoPath, "branch --show-current");
                if (string.IsNullOrWhiteSpace(branch))
                {
                    // 可能是 detached HEAD 或空仓库
                    var commitCount = await RunGitAsync(repoPath, "rev-list --count HEAD 2>nul");
                    if (commitCount == "0" || string.IsNullOrWhiteSpace(commitCount))
                    {
                        status.Branch = "(空仓库)";
                        status.Error = "空仓库";
                        return status;
                    }
                    status.Branch = "(detached)";
                }
                else
                {
                    status.Branch = branch.Trim();
                }

                // 获取最新提交信息
                var lastCommit = await RunGitAsync(repoPath, "log -1 --format=%H|%s|%ai|%an");
                if (!string.IsNullOrWhiteSpace(lastCommit))
                {
                    var parts = lastCommit.Split('|', 4);
                    if (parts.Length >= 4)
                    {
                        status.LastCommitHash = parts[0][..Math.Min(7, parts[0].Length)];
                        status.LastCommitMessage = parts[1];
                        status.LastCommitAuthor = parts[3];

                        // 解析时间
                        if (DateTime.TryParse(parts[2], out var commitTime))
                        {
                            status.LastCommitTime = FormatRelativeTime(commitTime);
                        }
                        else
                        {
                            status.LastCommitTime = parts[2];
                        }
                    }
                }

                // 检查是否有远程跟踪分支
                var upstream = await RunGitAsync(repoPath, "rev-parse --abbrev-ref @{u} 2>nul");
                if (string.IsNullOrWhiteSpace(upstream))
                {
                    status.HasRemote = false;
                    status.Status = GitSyncStatus.NoRemote;
                    return status;
                }

                status.HasRemote = true;

                // fetch 远程更新
                await RunGitAsync(repoPath, "fetch --all --prune --quiet", timeoutMs: 20000);

                // 获取 ahead/behind 计数
                var countResult = await RunGitAsync(repoPath, "rev-list --left-right --count HEAD...@{u}");
                if (!string.IsNullOrWhiteSpace(countResult))
                {
                    var counts = countResult.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if (counts.Length >= 2 &&
                        int.TryParse(counts[0], out var ahead) &&
                        int.TryParse(counts[1], out var behind))
                    {
                        status.Ahead = ahead;
                        status.Behind = behind;

                        status.Status = (ahead, behind) switch
                        {
                            (0, 0) => GitSyncStatus.Synced,
                            (> 0, 0) => GitSyncStatus.Ahead,
                            (0, > 0) => GitSyncStatus.Behind,
                            _ => GitSyncStatus.Diverged
                        };
                    }
                }
                else
                {
                    // rev-list 失败，尝试比较 hash
                    var localHash = await RunGitAsync(repoPath, "rev-parse HEAD");
                    var remoteHash = await RunGitAsync(repoPath, "rev-parse @{u}");

                    if (!string.IsNullOrWhiteSpace(localHash) && !string.IsNullOrWhiteSpace(remoteHash))
                    {
                        status.Status = localHash.Trim() == remoteHash.Trim()
                            ? GitSyncStatus.Synced
                            : GitSyncStatus.Diverged;
                    }
                    else
                    {
                        status.Status = GitSyncStatus.Error;
                        status.Error = "无法比较";
                    }
                }
            }
            catch (Exception ex)
            {
                status.Status = GitSyncStatus.Error;
                status.Error = ex.Message;
                Debug.WriteLine($"[GitSync] 检查仓库失败 {repoPath}: {ex.Message}");
            }

            return status;
        }

        /// <summary>
        /// 检查单个 SVN 仓库的工作副本状态
        /// </summary>
        public async Task<GitRepoStatus> CheckSvnRepoAsync(string repoPath)
        {
            var status = new GitRepoStatus
            {
                RepoPath = repoPath,
                RepoName = Path.GetFileName(repoPath),
                VcsType = VcsType.Svn
            };

            try
            {
                // 检查 svn 是否可用
                var svnVersion = await RunSvnAsync(repoPath, "--version --quiet");
                if (svnVersion == null)
                {
                    status.Error = "未检测到SVN";
                    status.Status = GitSyncStatus.Error;
                    return status;
                }

                // 获取仓库 URL（SVN 没有分支概念，用 URL 代替）
                var url = await RunSvnAsync(repoPath, "info --show-item url");
                if (!string.IsNullOrWhiteSpace(url))
                {
                    // 显示 URL 的最后两段路径
                    var uri = url.Trim().TrimEnd('/');
                    var segments = uri.Split('/');
                    status.Branch = segments.Length >= 2
                        ? string.Join("/", segments[^2..])
                        : segments[^1];
                }

                // 获取最新提交信息
                var lastCommit = await RunSvnAsync(repoPath, "log -l 1 --quiet");
                if (!string.IsNullOrWhiteSpace(lastCommit))
                {
                    // 输出格式: "r1234 | author | 2026-08-07 10:30:00 +0800 (Thu, 07 Aug 2026)"
                    var lines = lastCommit.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith('r') && line.Contains('|'))
                        {
                            var parts = line.Split('|', 3, StringSplitOptions.TrimEntries);
                            if (parts.Length >= 3)
                            {
                                status.LastCommitHash = parts[0]; // r1234
                                status.LastCommitAuthor = parts[1];
                                if (DateTime.TryParse(parts[2].Split('(')[0].Trim(), out var commitTime))
                                    status.LastCommitTime = FormatRelativeTime(commitTime);
                            }
                            break;
                        }
                    }
                }

                // 获取本地修改数量
                var localStatus = await RunSvnAsync(repoPath, "status");
                var localChanges = 0;
                if (!string.IsNullOrWhiteSpace(localStatus))
                {
                    localChanges = localStatus.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Count(line => line.Length > 0 && "MADC!".Contains(line[0]));
                }

                // 获取远程最新版本（svn status -u 只显示更新信息）
                var remoteInfo = await RunSvnAsync(repoPath, "info --show-item revision");
                var localRev = 0;
                if (!string.IsNullOrWhiteSpace(remoteInfo) && int.TryParse(remoteInfo.Trim().TrimStart('r'), out var rev))
                    localRev = rev;

                // 尝试获取远程最新版本（需要网络）
                var remoteStatus = await RunSvnAsync(repoPath, "status -u -q", timeoutMs: 20000);
                var remoteRev = localRev;
                if (!string.IsNullOrWhiteSpace(remoteStatus))
                {
                    // 输出中 " * 1234" 行表示远程有更新
                    foreach (var line in remoteStatus.Split('\n'))
                    {
                        if (line.Contains('*'))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)");
                            if (match.Success && int.TryParse(match.Value, out var rRev))
                            {
                                remoteRev = Math.Max(remoteRev, rRev);
                            }
                        }
                    }
                }

                // 确定状态
                status.HasRemote = true;
                status.Ahead = localChanges;
                status.Behind = Math.Max(0, remoteRev - localRev);

                if (localChanges > 0 && remoteRev > localRev)
                    status.Status = GitSyncStatus.Diverged;
                else if (localChanges > 0)
                    status.Status = GitSyncStatus.Ahead;
                else if (remoteRev > localRev)
                    status.Status = GitSyncStatus.Behind;
                else
                    status.Status = GitSyncStatus.Synced;
            }
            catch (Exception ex)
            {
                status.Status = GitSyncStatus.Error;
                status.Error = ex.Message;
                Debug.WriteLine($"[GitSync] SVN 检查失败 {repoPath}: {ex.Message}");
            }

            return status;
        }

        /// <summary>
        /// 批量检查多个仓库（自动识别 Git/SVN）
        /// </summary>
        public async Task<List<GitRepoStatus>> CheckAllAsync(List<DiscoveredDir> dirs)
        {
            var results = new List<GitRepoStatus>();

            // 串行检查，避免并发网络请求过多
            foreach (var dir in dirs)
            {
                try
                {
                    GitRepoStatus status;
                    if (dir.VcsType == VcsType.Svn)
                        status = await CheckSvnRepoAsync(dir.Path);
                    else
                        status = await CheckRepoAsync(dir.Path);
                    results.Add(status);
                }
                catch (Exception ex)
                {
                    results.Add(new GitRepoStatus
                    {
                        RepoPath = dir.Path,
                        RepoName = dir.Name,
                        VcsType = dir.VcsType,
                        Status = GitSyncStatus.Error,
                        Error = ex.Message
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// 批量检查多个仓库（兼容旧调用，自动识别 VCS 类型）
        /// </summary>
        public async Task<List<GitRepoStatus>> CheckAllAsync(List<string> repoPaths)
        {
            var dirs = repoPaths.Select(p => new DiscoveredDir
            {
                Path = p,
                Name = System.IO.Path.GetFileName(p),
                VcsType = Directory.Exists(System.IO.Path.Combine(p, ".svn")) ? VcsType.Svn : VcsType.Git
            }).ToList();
            return await CheckAllAsync(dirs);
        }

        #region 辅助方法

        /// <summary>
        /// 检查路径是否是 git 仓库（包含 .git 目录或文件）
        /// </summary>
        private static bool IsGitRepo(string path)
        {
            var gitPath = Path.Combine(path, ".git");
            return Directory.Exists(gitPath) || File.Exists(gitPath);
        }

        /// <summary>
        /// 执行 svn 命令并返回标准输出（强制 UTF-8 编码）
        /// </summary>
        private static async Task<string?> RunSvnAsync(string workingDir, string arguments, int timeoutMs = DefaultTimeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "svn",
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return null;

                using var cts = new CancellationTokenSource(timeoutMs);
                var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);

                var waitTask = process.WaitForExitAsync(cts.Token);
                try
                {
                    await waitTask;
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(true); } catch { }
                    return null;
                }

                return await outputTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GitSync] svn 命令执行失败: {arguments} - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 执行 git 命令并返回标准输出（强制 UTF-8 编码）
        /// </summary>
        private static async Task<string?> RunGitAsync(string workingDir, string arguments, int timeoutMs = DefaultTimeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // 强制 git 输出 UTF-8，避免中文 Windows 下 GBK 乱码
                psi.EnvironmentVariables["LANG"] = "en_US.UTF-8";
                psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

                using var process = Process.Start(psi);
                if (process == null) return null;

                using var cts = new CancellationTokenSource(timeoutMs);
                var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

                // 等待进程完成或超时
                var waitTask = process.WaitForExitAsync(cts.Token);
                try
                {
                    await waitTask;
                }
                catch (OperationCanceledException)
                {
                    // 超时，杀掉进程
                    try { process.Kill(true); } catch { }
                    return null;
                }

                var output = await outputTask;
                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GitSync] git 命令执行失败: {arguments} - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 格式化相对时间
        /// </summary>
        private static string FormatRelativeTime(DateTime time)
        {
            var span = DateTime.Now - time;

            if (span.TotalMinutes < 1) return "刚刚";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}分钟前";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}小时前";
            if (span.TotalDays < 2) return "昨天";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}天前";
            if (span.TotalDays < 30) return $"{(int)(span.TotalDays / 7)}周前";
            return time.ToString("MM/dd");
        }

        #endregion
    }

    /// <summary>
    /// 版本控制系统类型
    /// </summary>
    public enum VcsType
    {
        /// <summary>无版本控制</summary>
        None,
        /// <summary>Git</summary>
        Git,
        /// <summary>SVN (Subversion)</summary>
        Svn
    }

    /// <summary>
    /// 仓库同步状态
    /// </summary>
    public enum GitSyncStatus
    {
        /// <summary>已同步</summary>
        Synced,
        /// <summary>本地领先（有未推送的提交）</summary>
        Ahead,
        /// <summary>远程更新（有可拉取的提交）</summary>
        Behind,
        /// <summary>本地和远程分歧</summary>
        Diverged,
        /// <summary>无远程跟踪分支</summary>
        NoRemote,
        /// <summary>检查出错</summary>
        Error
    }

    /// <summary>
    /// 单个仓库的同步状态信息
    /// </summary>
    public class GitRepoStatus
    {
        /// <summary>仓库目录名</summary>
        public string RepoName { get; set; } = "";
        /// <summary>仓库完整路径</summary>
        public string RepoPath { get; set; } = "";
        /// <summary>VCS 类型</summary>
        public VcsType VcsType { get; set; } = VcsType.Git;
        /// <summary>当前分支名（Git）或仓库路径（SVN）</summary>
        public string Branch { get; set; } = "";
        /// <summary>同步状态</summary>
        public GitSyncStatus Status { get; set; }
        /// <summary>本地领先提交数</summary>
        public int Ahead { get; set; }
        /// <summary>远程领先提交数</summary>
        public int Behind { get; set; }
        /// <summary>是否有远程跟踪分支</summary>
        public bool HasRemote { get; set; }
        /// <summary>最新提交的短 hash</summary>
        public string LastCommitHash { get; set; } = "";
        /// <summary>最新提交信息</summary>
        public string LastCommitMessage { get; set; } = "";
        /// <summary>最新提交作者</summary>
        public string LastCommitAuthor { get; set; } = "";
        /// <summary>最新提交的相对时间</summary>
        public string LastCommitTime { get; set; } = "";
        /// <summary>错误信息</summary>
        public string Error { get; set; } = "";

        /// <summary>
        /// 状态的显示文字
        /// </summary>
        public string StatusText => (Status, VcsType) switch
        {
            (GitSyncStatus.Synced, _) => "✅ 已同步",
            (GitSyncStatus.Ahead, VcsType.Svn) => $"⬆ {Ahead} 本地修改",
            (GitSyncStatus.Ahead, _) => $"⬆ {Ahead} 待推",
            (GitSyncStatus.Behind, _) => $"⬇ {Behind} 待拉",
            (GitSyncStatus.Diverged, VcsType.Svn) => $"⚠ 有修改+有更新",
            (GitSyncStatus.Diverged, _) => $"⚠ 分歧 ({Ahead}推/{Behind}拉)",
            (GitSyncStatus.NoRemote, _) => "🔗 无远程",
            (GitSyncStatus.Error, _) => $"❌ {Error}",
            _ => "❓ 未知"
        };
    }

    /// <summary>
    /// 扫描发现的目录信息
    /// </summary>
    public class DiscoveredDir
    {
        /// <summary>目录名</summary>
        public string Name { get; set; } = "";
        /// <summary>目录完整路径</summary>
        public string Path { get; set; } = "";
        /// <summary>VCS 类型</summary>
        public VcsType VcsType { get; set; }
    }
}
