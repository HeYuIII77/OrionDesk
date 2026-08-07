namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// 天气设置
    /// </summary>
    public class WeatherSettings
    {
        /// <summary>
        /// 和风天气 API Key
        /// </summary>
        public string ApiKey { get; set; } = "";

        /// <summary>
        /// 和风天气 API Host（项目专属，如 m24d95fgre.re.qweatherapi.com）
        /// </summary>
        public string ApiHost { get; set; } = "";

        /// <summary>
        /// 刷新频率（分钟），默认30分钟
        /// 免费版每天5000次，30分钟 ≈ 48次/天
        /// </summary>
        public int RefreshMinutes { get; set; } = 30;

        /// <summary>
        /// 和风天气城市 ID（GeoAPI 查询结果）
        /// </summary>
        public string CityId { get; set; } = "";

        /// <summary>
        /// 用户选择的城市名（如 "北京市"、"上海市"）
        /// </summary>
        public string CityName { get; set; } = "";

        /// <summary>
        /// 城市经度（GeoAPI 查询结果，如 116.41）
        /// </summary>
        public double CityLon { get; set; }

        /// <summary>
        /// 城市纬度（GeoAPI 查询结果，如 39.92）
        /// </summary>
        public double CityLat { get; set; }
    }
}
