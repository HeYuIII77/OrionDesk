namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// 文件夹映射组件设置
    /// </summary>
    public class FolderSettings
    {
        /// <summary>
        /// 映射的文件夹路径
        /// </summary>
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>
        /// 是否显示隐藏文件
        /// </summary>
        public bool ShowHiddenFiles { get; set; } = false;

        /// <summary>
        /// 是否显示文件大小
        /// </summary>
        public bool ShowFileSize { get; set; } = true;

        /// <summary>
        /// 是否显示修改时间
        /// </summary>
        public bool ShowModifiedTime { get; set; } = true;
    }
}
