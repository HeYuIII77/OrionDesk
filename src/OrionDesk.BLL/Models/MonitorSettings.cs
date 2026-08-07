namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// 系统监控组件设置
    /// </summary>
    public class MonitorSettings
    {
        /// <summary>
        /// 是否显示CPU使用率
        /// </summary>
        public bool ShowCpu { get; set; } = true;

        /// <summary>
        /// 是否显示内存使用率
        /// </summary>
        public bool ShowMemory { get; set; } = true;

        /// <summary>
        /// 要监控的磁盘盘符列表（如 ["C:", "D:"]）
        /// </summary>
        public List<string> Drives { get; set; } = new List<string>();

        /// <summary>
        /// 刷新间隔（毫秒）
        /// </summary>
        public int RefreshInterval { get; set; } = 2000;
    }
}
