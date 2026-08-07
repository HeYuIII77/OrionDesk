using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace OrionDesk.BLL.Services
{
    /// <summary>
    /// 系统监控服务 - 获取CPU、内存、磁盘使用情况
    /// </summary>
    public class SystemMonitorService : IDisposable
    {
        private PerformanceCounter? _cpuCounter;
        private bool _disposed = false;

        // Win32 API for memory info
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(this);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public SystemMonitorService()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                // 预热：第一次调用通常返回0
                _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化CPU计数器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取CPU使用率（百分比）
        /// </summary>
        public float GetCpuUsage()
        {
            try
            {
                return _cpuCounter?.NextValue() ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取系统内存使用情况
        /// </summary>
        public (long used, long total, float percentage) GetMemoryUsage()
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    var totalMemory = (long)memStatus.ullTotalPhys;
                    var availableMemory = (long)memStatus.ullAvailPhys;
                    var usedMemory = totalMemory - availableMemory;
                    var percentage = (float)memStatus.dwMemoryLoad;

                    return (usedMemory, totalMemory, percentage);
                }

                return (0, 0, 0);
            }
            catch
            {
                return (0, 0, 0);
            }
        }

        /// <summary>
        /// 获取磁盘使用情况
        /// </summary>
        public List<Models.DriveInfo> GetDriveUsage(List<string>? driveLetters = null)
        {
            var drives = new List<Models.DriveInfo>();

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    // 过滤指定盘符
                    if (driveLetters != null && !driveLetters.Contains(drive.Name.TrimEnd('\\')))
                        continue;

                    // 只处理固定磁盘和可移动磁盘
                    if (drive.DriveType != System.IO.DriveType.Fixed &&
                        drive.DriveType != System.IO.DriveType.Removable)
                        continue;

                    try
                    {
                        var info = new Models.DriveInfo
                        {
                            Letter = drive.Name.TrimEnd('\\'),
                            TotalSize = drive.TotalSize,
                            FreeSpace = drive.TotalFreeSpace,
                            UsedSpace = drive.TotalSize - drive.TotalFreeSpace,
                            Percentage = (float)(drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize * 100
                        };
                        drives.Add(info);
                    }
                    catch
                    {
                        // 无法访问的磁盘跳过
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取磁盘信息失败: {ex.Message}");
            }

            return drives;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cpuCounter?.Dispose();
                _disposed = true;
            }
        }
    }

    namespace Models
    {
        /// <summary>
        /// 磁盘信息
        /// </summary>
        public class DriveInfo
        {
            public string Letter { get; set; } = string.Empty;
            public long TotalSize { get; set; }
            public long FreeSpace { get; set; }
            public long UsedSpace { get; set; }
            public float Percentage { get; set; }
        }
    }
}
