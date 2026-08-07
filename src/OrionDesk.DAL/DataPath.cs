using System;
using System.IO;

namespace OrionDesk.DAL
{
    /// <summary>
    /// 数据路径管理
    /// 管理配置文件和数据文件的存储位置
    /// </summary>
    public static class DataPath
    {
        // 应用数据根目录：AppData\Local\OrionDesk
        private static readonly string AppDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OrionDesk");

        // 配置文件路径
        public static readonly string ConfigFile = Path.Combine(AppDataRoot, "config.json");

        // 便签数据文件路径
        public static readonly string NotesFile = Path.Combine(AppDataRoot, "notes.json");

        // 日志文件目录
        public static readonly string LogDirectory = Path.Combine(AppDataRoot, "logs");

        /// <summary>
        /// 确保数据目录存在
        /// </summary>
        public static void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(AppDataRoot);
            Directory.CreateDirectory(LogDirectory);
        }

        /// <summary>
        /// 获取日志文件路径
        /// </summary>
        public static string GetLogFilePath(DateTime date)
        {
            return Path.Combine(LogDirectory, $"OrionDesk_{date:yyyy-MM-dd}.log");
        }
    }
}
