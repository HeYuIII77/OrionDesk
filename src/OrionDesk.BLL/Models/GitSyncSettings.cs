namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// Git 同步监控设置
    /// </summary>
    public class GitSyncSettings
    {
        /// <summary>
        /// 自动扫描的根目录（如 D:\Project\C#）
        /// 会自动发现该目录下所有包含 .git 的仓库
        /// </summary>
        public string ScanPath { get; set; } = "";

        /// <summary>
        /// 额外手动添加的仓库路径（不在扫描目录下的）
        /// </summary>
        public List<string> ExtraRepos { get; set; } = new();

        /// <summary>
        /// 检查间隔（分钟），默认 10 分钟
        /// </summary>
        public int RefreshMinutes { get; set; } = 10;
    }
}
