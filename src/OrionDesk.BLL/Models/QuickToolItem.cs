namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// 快捷工具类型
    /// </summary>
    public enum QuickToolType
    {
        /// <summary>应用程序（Process.Start）</summary>
        App,
        /// <summary>文件夹（explorer.exe 打开）</summary>
        Folder,
        /// <summary>URL（默认浏览器打开）</summary>
        Url,
        /// <summary>Shell 命令（cmd /k 执行）</summary>
        Shell
    }

    /// <summary>
    /// 快捷工具项
    /// </summary>
    public class QuickToolItem
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// 显示名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 图标（emoji 字符或图标文件路径）
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// 程序路径 / URL / Shell 命令
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// 启动参数（仅 App 类型有效）
        /// </summary>
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// 工具类型
        /// </summary>
        public QuickToolType Type { get; set; } = QuickToolType.App;

        /// <summary>
        /// 是否以管理员权限启动
        /// </summary>
        public bool RunAsAdmin { get; set; } = false;

        /// <summary>
        /// 是否为预置工具（不可删除）
        /// </summary>
        public bool IsPreset { get; set; } = false;

        /// <summary>
        /// 分类标识（system/dev/custom）
        /// </summary>
        public string Category { get; set; } = "custom";
    }
}
