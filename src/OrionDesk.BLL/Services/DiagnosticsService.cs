using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using OrionDesk.DAL;

namespace OrionDesk.BLL.Services
{
    /// <summary>
    /// 诊断服务 - 采集进程级指标（内存/GDI/USER/线程/句柄/GC）
    /// 用于长时间运行稳定性验证
    /// </summary>
    public class DiagnosticsService : IDisposable
    {
        #region Win32 API

        private const uint GR_GDIOBJECTS = 0;
        private const uint GR_USEROBJECTS = 1;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetGuiResources(IntPtr hProcess, uint uiFlags);

        #endregion

        #region 数据模型

        /// <summary>
        /// 诊断快照 - 某一时刻的进程指标
        /// </summary>
        public class DiagnosticsSnapshot
        {
            public DateTime Timestamp { get; set; } = DateTime.Now;
            /// <summary>工作集内存 (MB)</summary>
            public double WorkingSetMB { get; set; }
            /// <summary>私有内存 (MB)</summary>
            public double PrivateMemoryMB { get; set; }
            /// <summary>托管堆大小 (MB)</summary>
            public double ManagedHeapMB { get; set; }
            /// <summary>GDI 对象数</summary>
            public uint GdiHandles { get; set; }
            /// <summary>USER 对象数</summary>
            public uint UserHandles { get; set; }
            /// <summary>内核句柄数</summary>
            public int HandleCount { get; set; }
            /// <summary>线程数</summary>
            public int ThreadCount { get; set; }
            /// <summary>GC 第0代回收次数</summary>
            public int Gen0Collections { get; set; }
            /// <summary>GC 第1代回收次数</summary>
            public int Gen1Collections { get; set; }
            /// <summary>GC 第2代回收次数</summary>
            public int Gen2Collections { get; set; }
        }

        #endregion

        #region 字段和属性

        private readonly System.Timers.Timer _timer;
        private readonly List<DiagnosticsSnapshot> _history = new();
        private readonly int _maxHistoryCount = 288; // 24h @ 5min interval
        private readonly object _csvLock = new();
        private bool _disposed = false;

        /// <summary>采集间隔（分钟）</summary>
        public int IntervalMinutes { get; }

        /// <summary>历史快照（只读副本）</summary>
        public IReadOnlyList<DiagnosticsSnapshot> History => _history.AsReadOnly();

        /// <summary>最新快照</summary>
        public DiagnosticsSnapshot? LatestSnapshot => _history.Count > 0 ? _history[^1] : null;

        /// <summary>快照采集事件（UI 订阅以刷新显示）</summary>
        public event Action<DiagnosticsSnapshot>? OnSnapshot;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建诊断服务
        /// </summary>
        /// <param name="intervalMinutes">采集间隔（分钟），默认 5</param>
        public DiagnosticsService(int intervalMinutes = 5)
        {
            IntervalMinutes = intervalMinutes;

            _timer = new System.Timers.Timer(intervalMinutes * 60 * 1000);
            _timer.Elapsed += (s, e) => TakeSnapshot();
            _timer.AutoReset = true;
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 启动定时采集
        /// </summary>
        public void Start()
        {
            // 立即采集一次
            TakeSnapshot();
            _timer.Start();
            Debug.WriteLine($"[Diagnostics] 服务启动，间隔 {IntervalMinutes} 分钟");
        }

        /// <summary>
        /// 停止定时采集
        /// </summary>
        public void Stop()
        {
            _timer.Stop();
            Debug.WriteLine("[Diagnostics] 服务停止");
        }

        /// <summary>
        /// 手动触发一次采集
        /// </summary>
        public DiagnosticsSnapshot TakeSnapshot()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var snapshot = new DiagnosticsSnapshot
                {
                    Timestamp = DateTime.Now,
                    WorkingSetMB = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1),
                    PrivateMemoryMB = Math.Round(process.PrivateMemorySize64 / 1024.0 / 1024.0, 1),
                    ManagedHeapMB = Math.Round(GC.GetTotalMemory(false) / 1024.0 / 1024.0, 1),
                    GdiHandles = GetGuiResources(process.Handle, GR_GDIOBJECTS),
                    UserHandles = GetGuiResources(process.Handle, GR_USEROBJECTS),
                    HandleCount = process.HandleCount,
                    ThreadCount = process.Threads.Count,
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2)
                };

                // 添加到历史
                _history.Add(snapshot);

                // 超过上限时移除最旧的
                while (_history.Count > _maxHistoryCount)
                    _history.RemoveAt(0);

                // 写入 CSV
                WriteToCsv(snapshot);

                // 通知订阅者
                OnSnapshot?.Invoke(snapshot);

                Debug.WriteLine($"[Diagnostics] 采集完成: 内存={snapshot.WorkingSetMB}MB, GDI={snapshot.GdiHandles}, USER={snapshot.UserHandles}, 线程={snapshot.ThreadCount}");
                return snapshot;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Diagnostics] 采集失败: {ex.Message}");
                return new DiagnosticsSnapshot(); // 返回空快照
            }
        }

        /// <summary>
        /// 清空历史记录
        /// </summary>
        public void ClearHistory()
        {
            _history.Clear();
        }

        /// <summary>
        /// 获取 CSV 日志目录路径
        /// </summary>
        public string GetLogDirectory()
        {
            return DataPath.LogDirectory;
        }

        #endregion

        #region CSV 写入

        /// <summary>
        /// 写入单条记录到 CSV 文件（按日分文件）
        /// </summary>
        private void WriteToCsv(DiagnosticsSnapshot snapshot)
        {
            try
            {
                DataPath.EnsureDirectoriesExist();
                var filePath = Path.Combine(DataPath.LogDirectory, $"diagnostics_{snapshot.Timestamp:yyyy-MM-dd}.csv");

                lock (_csvLock)
                {
                    var fileExists = File.Exists(filePath);
                    using var writer = new StreamWriter(filePath, append: true);

                    // 首次写入时添加表头
                    if (!fileExists)
                    {
                        writer.WriteLine("Timestamp,WorkingSetMB,PrivateMemoryMB,ManagedHeapMB,GdiHandles,UserHandles,HandleCount,ThreadCount,Gen0,Gen1,Gen2");
                    }

                    writer.WriteLine(
                        $"{snapshot.Timestamp:yyyy-MM-ddTHH:mm:ss}," +
                        $"{snapshot.WorkingSetMB}," +
                        $"{snapshot.PrivateMemoryMB}," +
                        $"{snapshot.ManagedHeapMB}," +
                        $"{snapshot.GdiHandles}," +
                        $"{snapshot.UserHandles}," +
                        $"{snapshot.HandleCount}," +
                        $"{snapshot.ThreadCount}," +
                        $"{snapshot.Gen0Collections}," +
                        $"{snapshot.Gen1Collections}," +
                        $"{snapshot.Gen2Collections}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Diagnostics] CSV 写入失败: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _timer.Stop();
                _disposed = true;
            }
        }

        #endregion
    }
}
