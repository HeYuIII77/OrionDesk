namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// 启动器中的应用项
    /// </summary>
    public class LauncherItem
    {
        /// <summary>
        /// 显示名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 应用路径（目标 .exe 路径）
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// 图标路径（可选，为空时自动提取）
        /// </summary>
        public string? IconPath { get; set; }

        /// <summary>
        /// 启动参数
        /// </summary>
        public string? Arguments { get; set; }

        /// <summary>
        /// 原始快捷方式文件名（用于恢复桌面图标）
        /// </summary>
        public string? ShortcutName { get; set; }
    }
}
