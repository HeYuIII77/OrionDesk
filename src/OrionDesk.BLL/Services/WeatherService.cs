using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;

namespace OrionDesk.BLL.Services
{
    /// <summary>
    /// 天气服务 - 调用和风天气 API 获取天气数据
    /// 支持：实时天气、空气质量、3天预报、天气预警、生活指数、日出日落
    /// 定位方式：1) 用户手动配置城市 2) IP 自动定位（ip-api.com）
    /// </summary>
    public class WeatherService : IDisposable
    {
        private readonly HttpClient _httpClient;

        // 缓存 - 实时天气 + 空气质量
        private WeatherInfo? _cachedWeather;
        private double _cachedLat;
        private double _cachedLon;
        private string _cachedCityName = "";
        private DateTimeOffset _lastWeatherRequest = DateTimeOffset.MinValue;
        private DateTimeOffset _lastLocationRequest = DateTimeOffset.MinValue;

        // 缓存 - 每日数据（预报/天文/指数），按日期判断
        private string _lastDailyDate = "";
        private List<ForecastDay>? _cachedForecast;
        private string _cachedSunrise = "";
        private string _cachedSunset = "";
        private List<WeatherIndex>? _cachedIndices;

        // 缓存 - 天气预警（15分钟刷新）
        private DateTimeOffset _lastWarningRequest = DateTimeOffset.MinValue;
        private List<WeatherWarning>? _cachedWarnings;

        public WeatherService()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "OrionDesk/1.0");
        }

        /// <summary>
        /// 获取实时天气（带频率控制）
        /// 优先使用用户配置的城市，未配置时使用 IP 定位
        /// </summary>
        /// <param name="apiKey">和风天气 API Key</param>
        /// <param name="apiHost">和风天气 API Host</param>
        /// <param name="refreshMinutes">刷新间隔（分钟）</param>
        /// <param name="cityLat">用户配置的城市纬度（0 表示未配置）</param>
        /// <param name="cityLon">用户配置的城市经度（0 表示未配置）</param>
        /// <param name="cityName">用户配置的城市名</param>
        /// <returns>天气信息，失败返回 null</returns>
        public async Task<WeatherInfo?> GetWeatherAsync(string apiKey, string apiHost, int refreshMinutes,
            double cityLat = 0, double cityLon = 0, string? cityName = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiHost))
                return null;

            // 频率控制：未过期直接返回缓存
            if (_cachedWeather != null &&
                (DateTimeOffset.Now - _lastWeatherRequest).TotalMinutes < refreshMinutes)
            {
                return _cachedWeather;
            }

            try
            {
                // 1. 确定坐标：优先用户配置，其次 IP 定位
                if (cityLat != 0 && cityLon != 0 && !string.IsNullOrWhiteSpace(cityName))
                {
                    // 用户手动配置了城市，直接使用
                    _cachedLat = cityLat;
                    _cachedLon = cityLon;
                    _cachedCityName = cityName;
                    _lastLocationRequest = DateTimeOffset.Now;
                    Debug.WriteLine($"[WeatherService] 使用配置城市: {_cachedCityName} ({_cachedLat},{_cachedLon})");
                }
                else if (_cachedLat == 0 && _cachedLon == 0 ||
                    (DateTimeOffset.Now - _lastLocationRequest).TotalHours > 24)
                {
                    // IP 定位获取坐标（缓存24小时）
                    Debug.WriteLine("[WeatherService] 开始IP定位...");
                    var location = await GetLocationByIpAsync();
                    if (location == null)
                    {
                        Debug.WriteLine("[WeatherService] IP定位失败，返回旧缓存");
                        return _cachedWeather;
                    }

                    _cachedLat = location.Value.Lat;
                    _cachedLon = location.Value.Lon;
                    _cachedCityName = location.Value.City;
                    _lastLocationRequest = DateTimeOffset.Now;
                    Debug.WriteLine($"[WeatherService] IP定位成功: {_cachedCityName} ({_cachedLat},{_cachedLon})");
                }

                // 2. 用坐标查询实时天气 + 空气质量
                Debug.WriteLine($"[WeatherService] 查询天气: {_cachedLat},{_cachedLon}");
                var weather = await GetWeatherNowAsync(apiKey, apiHost, _cachedLat, _cachedLon);
                if (weather != null)
                {
                    weather.CityName = _cachedCityName;
                    _cachedWeather = weather;
                    _lastWeatherRequest = DateTimeOffset.Now;
                    Debug.WriteLine($"[WeatherService] 天气查询成功: {weather.CityName} {weather.WeatherText} {weather.Temperature}°C");

                    // 3. 查询空气质量（与天气共用缓存周期）
                    var air = await GetAirNowAsync(apiKey, apiHost, _cachedLat, _cachedLon);
                    if (air != null)
                    {
                        weather.Aqi = air.Aqi;
                        weather.AirLevel = air.AirLevel;
                        weather.AirCategory = air.AirCategory;
                        weather.Pm2p5 = air.Pm2p5;
                        weather.Pm10 = air.Pm10;
                        Debug.WriteLine($"[WeatherService] 空气质量: AQI={air.Aqi} {air.AirCategory}");
                    }
                }
                else
                {
                    Debug.WriteLine("[WeatherService] 天气查询返回 null");
                }

                // 4. 每日数据（预报/天文/指数），每天只需请求一次
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                if (_lastDailyDate != today)
                {
                    Debug.WriteLine("[WeatherService] 请求每日数据...");

                    var forecast = await GetForecast3dAsync(apiKey, apiHost, _cachedLat, _cachedLon);
                    if (forecast != null)
                    {
                        _cachedForecast = forecast;
                        Debug.WriteLine($"[WeatherService] 预报获取成功: {forecast.Count}天");
                    }

                    var astronomy = await GetSunriseSunsetAsync(apiKey, apiHost, _cachedLat, _cachedLon);
                    if (astronomy != null)
                    {
                        _cachedSunrise = astronomy.Value.Sunrise;
                        _cachedSunset = astronomy.Value.Sunset;
                        Debug.WriteLine($"[WeatherService] 天文: 日出={_cachedSunrise} 日落={_cachedSunset}");
                    }

                    var indices = await GetIndices1dAsync(apiKey, apiHost, _cachedLat, _cachedLon);
                    if (indices != null)
                    {
                        _cachedIndices = indices;
                        Debug.WriteLine($"[WeatherService] 生活指数获取成功: {indices.Count}项");
                    }

                    _lastDailyDate = today;
                }

                // 5. 天气预警（15分钟刷新）
                if ((DateTimeOffset.Now - _lastWarningRequest).TotalMinutes >= 15)
                {
                    var warnings = await GetWarningNowAsync(apiKey, apiHost, _cachedLat, _cachedLon);
                    _cachedWarnings = warnings;
                    _lastWarningRequest = DateTimeOffset.Now;
                    Debug.WriteLine($"[WeatherService] 天气预警: {warnings?.Count ?? 0}条");
                }

                // 6. 合并所有数据到 WeatherInfo
                if (_cachedWeather != null)
                {
                    _cachedWeather.Forecast = _cachedForecast ?? new List<ForecastDay>();
                    _cachedWeather.Sunrise = _cachedSunrise;
                    _cachedWeather.Sunset = _cachedSunset;
                    _cachedWeather.Indices = _cachedIndices ?? new List<WeatherIndex>();
                    _cachedWeather.Warnings = _cachedWarnings ?? new List<WeatherWarning>();
                }

                return _cachedWeather;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WeatherService] 获取天气异常: {ex.Message}");
                return _cachedWeather;
            }
        }

        /// <summary>
        /// 搜索城市（本地城市数据库，支持模糊匹配）
        /// </summary>
        /// <param name="cityName">城市名关键词（如 "北京"、"武汉"、"wuhan"）</param>
        /// <returns>匹配的城市列表，最多返回 15 个</returns>
        public Task<List<CityInfo>> SearchCityAsync(string cityName)
        {
            var result = new List<CityInfo>();
            if (string.IsNullOrWhiteSpace(cityName))
                return Task.FromResult(result);

            var keyword = cityName.Trim().ToLower();

            // 模糊匹配：城市名或省份名包含关键词
            foreach (var city in ChineseCities)
            {
                if (city.Name.ToLower().Contains(keyword) ||
                    city.Adm1.ToLower().Contains(keyword) ||
                    city.Adm2.ToLower().Contains(keyword))
                {
                    result.Add(city);
                    if (result.Count >= 15)
                        break;
                }
            }

            Debug.WriteLine($"[WeatherService] 城市搜索 \"{cityName}\" 找到 {result.Count} 个结果");
            return Task.FromResult(result);
        }

        /// <summary>
        /// 中国主要城市数据库（省会 + 地级市 + 特别行政区）
        /// 坐标精确到小数点后两位，足够天气查询使用
        /// </summary>
        private static readonly List<CityInfo> ChineseCities = new()
        {
            // === 直辖市 ===
            new() { Name = "北京", Adm1 = "北京市", Adm2 = "北京市", Country = "中国", Lat = 39.90, Lon = 116.41 },
            new() { Name = "上海", Adm1 = "上海市", Adm2 = "上海市", Country = "中国", Lat = 31.23, Lon = 121.47 },
            new() { Name = "天津", Adm1 = "天津市", Adm2 = "天津市", Country = "中国", Lat = 39.13, Lon = 117.20 },
            new() { Name = "重庆", Adm1 = "重庆市", Adm2 = "重庆市", Country = "中国", Lat = 29.56, Lon = 106.55 },

            // === 特别行政区 ===
            new() { Name = "香港", Adm1 = "香港特别行政区", Adm2 = "香港特别行政区", Country = "中国", Lat = 22.32, Lon = 114.17 },
            new() { Name = "澳门", Adm1 = "澳门特别行政区", Adm2 = "澳门特别行政区", Country = "中国", Lat = 22.20, Lon = 113.55 },

            // === 河北省 ===
            new() { Name = "石家庄", Adm1 = "河北省", Adm2 = "石家庄市", Country = "中国", Lat = 38.04, Lon = 114.51 },
            new() { Name = "唐山", Adm1 = "河北省", Adm2 = "唐山市", Country = "中国", Lat = 39.63, Lon = 118.18 },
            new() { Name = "秦皇岛", Adm1 = "河北省", Adm2 = "秦皇岛市", Country = "中国", Lat = 39.93, Lon = 119.60 },
            new() { Name = "邯郸", Adm1 = "河北省", Adm2 = "邯郸市", Country = "中国", Lat = 36.63, Lon = 114.49 },
            new() { Name = "保定", Adm1 = "河北省", Adm2 = "保定市", Country = "中国", Lat = 38.87, Lon = 115.46 },
            new() { Name = "张家口", Adm1 = "河北省", Adm2 = "张家口市", Country = "中国", Lat = 40.77, Lon = 114.88 },
            new() { Name = "承德", Adm1 = "河北省", Adm2 = "承德市", Country = "中国", Lat = 40.97, Lon = 117.93 },

            // === 山西省 ===
            new() { Name = "太原", Adm1 = "山西省", Adm2 = "太原市", Country = "中国", Lat = 37.87, Lon = 112.55 },
            new() { Name = "大同", Adm1 = "山西省", Adm2 = "大同市", Country = "中国", Lat = 40.08, Lon = 113.30 },
            new() { Name = "临汾", Adm1 = "山西省", Adm2 = "临汾市", Country = "中国", Lat = 36.09, Lon = 111.52 },

            // === 内蒙古自治区 ===
            new() { Name = "呼和浩特", Adm1 = "内蒙古自治区", Adm2 = "呼和浩特市", Country = "中国", Lat = 40.84, Lon = 111.75 },
            new() { Name = "包头", Adm1 = "内蒙古自治区", Adm2 = "包头市", Country = "中国", Lat = 40.66, Lon = 109.84 },
            new() { Name = "鄂尔多斯", Adm1 = "内蒙古自治区", Adm2 = "鄂尔多斯市", Country = "中国", Lat = 39.61, Lon = 109.78 },

            // === 辽宁省 ===
            new() { Name = "沈阳", Adm1 = "辽宁省", Adm2 = "沈阳市", Country = "中国", Lat = 41.80, Lon = 123.43 },
            new() { Name = "大连", Adm1 = "辽宁省", Adm2 = "大连市", Country = "中国", Lat = 38.91, Lon = 121.61 },
            new() { Name = "鞍山", Adm1 = "辽宁省", Adm2 = "鞍山市", Country = "中国", Lat = 41.11, Lon = 122.99 },
            new() { Name = "抚顺", Adm1 = "辽宁省", Adm2 = "抚顺市", Country = "中国", Lat = 41.88, Lon = 123.96 },

            // === 吉林省 ===
            new() { Name = "长春", Adm1 = "吉林省", Adm2 = "长春市", Country = "中国", Lat = 43.88, Lon = 125.32 },
            new() { Name = "吉林", Adm1 = "吉林省", Adm2 = "吉林市", Country = "中国", Lat = 43.84, Lon = 126.55 },

            // === 黑龙江省 ===
            new() { Name = "哈尔滨", Adm1 = "黑龙江省", Adm2 = "哈尔滨市", Country = "中国", Lat = 45.75, Lon = 126.65 },
            new() { Name = "齐齐哈尔", Adm1 = "黑龙江省", Adm2 = "齐齐哈尔市", Country = "中国", Lat = 47.35, Lon = 123.97 },
            new() { Name = "大庆", Adm1 = "黑龙江省", Adm2 = "大庆市", Country = "中国", Lat = 46.59, Lon = 125.10 },

            // === 江苏省 ===
            new() { Name = "南京", Adm1 = "江苏省", Adm2 = "南京市", Country = "中国", Lat = 32.06, Lon = 118.80 },
            new() { Name = "苏州", Adm1 = "江苏省", Adm2 = "苏州市", Country = "中国", Lat = 31.30, Lon = 120.62 },
            new() { Name = "无锡", Adm1 = "江苏省", Adm2 = "无锡市", Country = "中国", Lat = 31.49, Lon = 120.31 },
            new() { Name = "常州", Adm1 = "江苏省", Adm2 = "常州市", Country = "中国", Lat = 31.81, Lon = 119.97 },
            new() { Name = "南通", Adm1 = "江苏省", Adm2 = "南通市", Country = "中国", Lat = 32.06, Lon = 120.87 },
            new() { Name = "徐州", Adm1 = "江苏省", Adm2 = "徐州市", Country = "中国", Lat = 34.26, Lon = 117.18 },
            new() { Name = "扬州", Adm1 = "江苏省", Adm2 = "扬州市", Country = "中国", Lat = 32.39, Lon = 119.42 },
            new() { Name = "镇江", Adm1 = "江苏省", Adm2 = "镇江市", Country = "中国", Lat = 32.19, Lon = 119.45 },
            new() { Name = "连云港", Adm1 = "江苏省", Adm2 = "连云港市", Country = "中国", Lat = 34.60, Lon = 119.22 },
            new() { Name = "盐城", Adm1 = "江苏省", Adm2 = "盐城市", Country = "中国", Lat = 33.35, Lon = 120.16 },
            new() { Name = "泰州", Adm1 = "江苏省", Adm2 = "泰州市", Country = "中国", Lat = 32.49, Lon = 119.92 },
            new() { Name = "淮安", Adm1 = "江苏省", Adm2 = "淮安市", Country = "中国", Lat = 33.60, Lon = 119.02 },
            new() { Name = "宿迁", Adm1 = "江苏省", Adm2 = "宿迁市", Country = "中国", Lat = 33.96, Lon = 118.28 },

            // === 浙江省 ===
            new() { Name = "杭州", Adm1 = "浙江省", Adm2 = "杭州市", Country = "中国", Lat = 30.27, Lon = 120.15 },
            new() { Name = "宁波", Adm1 = "浙江省", Adm2 = "宁波市", Country = "中国", Lat = 29.87, Lon = 121.55 },
            new() { Name = "温州", Adm1 = "浙江省", Adm2 = "温州市", Country = "中国", Lat = 28.00, Lon = 120.67 },
            new() { Name = "嘉兴", Adm1 = "浙江省", Adm2 = "嘉兴市", Country = "中国", Lat = 30.75, Lon = 120.76 },
            new() { Name = "绍兴", Adm1 = "浙江省", Adm2 = "绍兴市", Country = "中国", Lat = 30.00, Lon = 120.58 },
            new() { Name = "金华", Adm1 = "浙江省", Adm2 = "金华市", Country = "中国", Lat = 29.08, Lon = 119.65 },
            new() { Name = "台州", Adm1 = "浙江省", Adm2 = "台州市", Country = "中国", Lat = 28.68, Lon = 121.42 },
            new() { Name = "湖州", Adm1 = "浙江省", Adm2 = "湖州市", Country = "中国", Lat = 30.89, Lon = 120.09 },

            // === 安徽省 ===
            new() { Name = "合肥", Adm1 = "安徽省", Adm2 = "合肥市", Country = "中国", Lat = 31.82, Lon = 117.23 },
            new() { Name = "芜湖", Adm1 = "安徽省", Adm2 = "芜湖市", Country = "中国", Lat = 31.35, Lon = 118.38 },
            new() { Name = "蚌埠", Adm1 = "安徽省", Adm2 = "蚌埠市", Country = "中国", Lat = 32.92, Lon = 117.39 },

            // === 福建省 ===
            new() { Name = "福州", Adm1 = "福建省", Adm2 = "福州市", Country = "中国", Lat = 26.07, Lon = 119.30 },
            new() { Name = "厦门", Adm1 = "福建省", Adm2 = "厦门市", Country = "中国", Lat = 24.48, Lon = 118.09 },
            new() { Name = "泉州", Adm1 = "福建省", Adm2 = "泉州市", Country = "中国", Lat = 24.87, Lon = 118.68 },
            new() { Name = "漳州", Adm1 = "福建省", Adm2 = "漳州市", Country = "中国", Lat = 24.51, Lon = 117.65 },

            // === 江西省 ===
            new() { Name = "南昌", Adm1 = "江西省", Adm2 = "南昌市", Country = "中国", Lat = 28.68, Lon = 115.86 },
            new() { Name = "赣州", Adm1 = "江西省", Adm2 = "赣州市", Country = "中国", Lat = 25.83, Lon = 114.93 },
            new() { Name = "九江", Adm1 = "江西省", Adm2 = "九江市", Country = "中国", Lat = 29.71, Lon = 115.97 },

            // === 山东省 ===
            new() { Name = "济南", Adm1 = "山东省", Adm2 = "济南市", Country = "中国", Lat = 36.65, Lon = 116.99 },
            new() { Name = "青岛", Adm1 = "山东省", Adm2 = "青岛市", Country = "中国", Lat = 36.07, Lon = 120.38 },
            new() { Name = "烟台", Adm1 = "山东省", Adm2 = "烟台市", Country = "中国", Lat = 37.46, Lon = 121.45 },
            new() { Name = "潍坊", Adm1 = "山东省", Adm2 = "潍坊市", Country = "中国", Lat = 36.71, Lon = 119.16 },
            new() { Name = "临沂", Adm1 = "山东省", Adm2 = "临沂市", Country = "中国", Lat = 35.10, Lon = 118.35 },
            new() { Name = "淄博", Adm1 = "山东省", Adm2 = "淄博市", Country = "中国", Lat = 36.81, Lon = 118.05 },
            new() { Name = "威海", Adm1 = "山东省", Adm2 = "威海市", Country = "中国", Lat = 37.51, Lon = 122.12 },
            new() { Name = "济宁", Adm1 = "山东省", Adm2 = "济宁市", Country = "中国", Lat = 35.41, Lon = 116.59 },

            // === 河南省 ===
            new() { Name = "郑州", Adm1 = "河南省", Adm2 = "郑州市", Country = "中国", Lat = 34.75, Lon = 113.65 },
            new() { Name = "洛阳", Adm1 = "河南省", Adm2 = "洛阳市", Country = "中国", Lat = 34.62, Lon = 112.45 },
            new() { Name = "开封", Adm1 = "河南省", Adm2 = "开封市", Country = "中国", Lat = 34.80, Lon = 114.31 },
            new() { Name = "南阳", Adm1 = "河南省", Adm2 = "南阳市", Country = "中国", Lat = 33.00, Lon = 112.53 },
            new() { Name = "新乡", Adm1 = "河南省", Adm2 = "新乡市", Country = "中国", Lat = 35.30, Lon = 113.93 },

            // === 湖北省 ===
            new() { Name = "武汉", Adm1 = "湖北省", Adm2 = "武汉市", Country = "中国", Lat = 30.59, Lon = 114.31 },
            new() { Name = "宜昌", Adm1 = "湖北省", Adm2 = "宜昌市", Country = "中国", Lat = 30.69, Lon = 111.29 },
            new() { Name = "襄阳", Adm1 = "湖北省", Adm2 = "襄阳市", Country = "中国", Lat = 32.01, Lon = 112.14 },
            new() { Name = "荆州", Adm1 = "湖北省", Adm2 = "荆州市", Country = "中国", Lat = 30.33, Lon = 112.24 },
            new() { Name = "黄冈", Adm1 = "湖北省", Adm2 = "黄冈市", Country = "中国", Lat = 30.45, Lon = 114.87 },
            new() { Name = "十堰", Adm1 = "湖北省", Adm2 = "十堰市", Country = "中国", Lat = 32.63, Lon = 110.80 },
            new() { Name = "孝感", Adm1 = "湖北省", Adm2 = "孝感市", Country = "中国", Lat = 30.92, Lon = 113.91 },
            new() { Name = "荆门", Adm1 = "湖北省", Adm2 = "荆门市", Country = "中国", Lat = 31.04, Lon = 112.20 },
            new() { Name = "黄石", Adm1 = "湖北省", Adm2 = "黄石市", Country = "中国", Lat = 30.20, Lon = 115.04 },
            new() { Name = "咸宁", Adm1 = "湖北省", Adm2 = "咸宁市", Country = "中国", Lat = 29.84, Lon = 114.32 },
            new() { Name = "恩施", Adm1 = "湖北省", Adm2 = "恩施土家族苗族自治州", Country = "中国", Lat = 30.27, Lon = 109.49 },
            new() { Name = "仙桃", Adm1 = "湖北省", Adm2 = "仙桃市", Country = "中国", Lat = 30.33, Lon = 113.45 },
            new() { Name = "潜江", Adm1 = "湖北省", Adm2 = "潜江市", Country = "中国", Lat = 30.40, Lon = 112.90 },
            new() { Name = "天门", Adm1 = "湖北省", Adm2 = "天门市", Country = "中国", Lat = 30.66, Lon = 113.17 },
            new() { Name = "鄂州", Adm1 = "湖北省", Adm2 = "鄂州市", Country = "中国", Lat = 30.39, Lon = 114.89 },
            new() { Name = "随州", Adm1 = "湖北省", Adm2 = "随州市", Country = "中国", Lat = 31.69, Lon = 113.38 },

            // === 湖南省 ===
            new() { Name = "长沙", Adm1 = "湖南省", Adm2 = "长沙市", Country = "中国", Lat = 28.23, Lon = 112.94 },
            new() { Name = "株洲", Adm1 = "湖南省", Adm2 = "株洲市", Country = "中国", Lat = 27.83, Lon = 113.13 },
            new() { Name = "湘潭", Adm1 = "湖南省", Adm2 = "湘潭市", Country = "中国", Lat = 27.83, Lon = 112.94 },
            new() { Name = "岳阳", Adm1 = "湖南省", Adm2 = "岳阳市", Country = "中国", Lat = 29.36, Lon = 113.13 },
            new() { Name = "衡阳", Adm1 = "湖南省", Adm2 = "衡阳市", Country = "中国", Lat = 26.89, Lon = 112.57 },
            new() { Name = "常德", Adm1 = "湖南省", Adm2 = "常德市", Country = "中国", Lat = 29.03, Lon = 111.69 },

            // === 广东省 ===
            new() { Name = "广州", Adm1 = "广东省", Adm2 = "广州市", Country = "中国", Lat = 23.13, Lon = 113.26 },
            new() { Name = "深圳", Adm1 = "广东省", Adm2 = "深圳市", Country = "中国", Lat = 22.54, Lon = 114.06 },
            new() { Name = "东莞", Adm1 = "广东省", Adm2 = "东莞市", Country = "中国", Lat = 23.02, Lon = 113.75 },
            new() { Name = "佛山", Adm1 = "广东省", Adm2 = "佛山市", Country = "中国", Lat = 23.02, Lon = 113.12 },
            new() { Name = "珠海", Adm1 = "广东省", Adm2 = "珠海市", Country = "中国", Lat = 22.27, Lon = 113.58 },
            new() { Name = "中山", Adm1 = "广东省", Adm2 = "中山市", Country = "中国", Lat = 22.52, Lon = 113.39 },
            new() { Name = "惠州", Adm1 = "广东省", Adm2 = "惠州市", Country = "中国", Lat = 23.11, Lon = 114.42 },
            new() { Name = "汕头", Adm1 = "广东省", Adm2 = "汕头市", Country = "中国", Lat = 23.35, Lon = 116.68 },
            new() { Name = "湛江", Adm1 = "广东省", Adm2 = "湛江市", Country = "中国", Lat = 21.27, Lon = 110.36 },
            new() { Name = "茂名", Adm1 = "广东省", Adm2 = "茂名市", Country = "中国", Lat = 21.66, Lon = 110.93 },
            new() { Name = "江门", Adm1 = "广东省", Adm2 = "江门市", Country = "中国", Lat = 22.58, Lon = 113.08 },
            new() { Name = "肇庆", Adm1 = "广东省", Adm2 = "肇庆市", Country = "中国", Lat = 23.05, Lon = 112.47 },
            new() { Name = "梅州", Adm1 = "广东省", Adm2 = "梅州市", Country = "中国", Lat = 24.29, Lon = 116.12 },

            // === 广西壮族自治区 ===
            new() { Name = "南宁", Adm1 = "广西壮族自治区", Adm2 = "南宁市", Country = "中国", Lat = 22.82, Lon = 108.32 },
            new() { Name = "桂林", Adm1 = "广西壮族自治区", Adm2 = "桂林市", Country = "中国", Lat = 25.27, Lon = 110.29 },
            new() { Name = "柳州", Adm1 = "广西壮族自治区", Adm2 = "柳州市", Country = "中国", Lat = 24.33, Lon = 109.42 },

            // === 海南省 ===
            new() { Name = "海口", Adm1 = "海南省", Adm2 = "海口市", Country = "中国", Lat = 20.02, Lon = 110.35 },
            new() { Name = "三亚", Adm1 = "海南省", Adm2 = "三亚市", Country = "中国", Lat = 18.25, Lon = 109.50 },

            // === 四川省 ===
            new() { Name = "成都", Adm1 = "四川省", Adm2 = "成都市", Country = "中国", Lat = 30.57, Lon = 104.07 },
            new() { Name = "绵阳", Adm1 = "四川省", Adm2 = "绵阳市", Country = "中国", Lat = 31.47, Lon = 104.73 },
            new() { Name = "德阳", Adm1 = "四川省", Adm2 = "德阳市", Country = "中国", Lat = 31.13, Lon = 104.40 },
            new() { Name = "宜宾", Adm1 = "四川省", Adm2 = "宜宾市", Country = "中国", Lat = 28.77, Lon = 104.63 },
            new() { Name = "南充", Adm1 = "四川省", Adm2 = "南充市", Country = "中国", Lat = 30.84, Lon = 106.11 },

            // === 贵州省 ===
            new() { Name = "贵阳", Adm1 = "贵州省", Adm2 = "贵阳市", Country = "中国", Lat = 26.65, Lon = 106.63 },
            new() { Name = "遵义", Adm1 = "贵州省", Adm2 = "遵义市", Country = "中国", Lat = 27.73, Lon = 106.93 },

            // === 云南省 ===
            new() { Name = "昆明", Adm1 = "云南省", Adm2 = "昆明市", Country = "中国", Lat = 25.04, Lon = 102.68 },
            new() { Name = "大理", Adm1 = "云南省", Adm2 = "大理白族自治州", Country = "中国", Lat = 25.59, Lon = 100.23 },
            new() { Name = "丽江", Adm1 = "云南省", Adm2 = "丽江市", Country = "中国", Lat = 26.87, Lon = 100.23 },

            // === 陕西省 ===
            new() { Name = "西安", Adm1 = "陕西省", Adm2 = "西安市", Country = "中国", Lat = 34.26, Lon = 108.94 },
            new() { Name = "咸阳", Adm1 = "陕西省", Adm2 = "咸阳市", Country = "中国", Lat = 34.33, Lon = 108.72 },
            new() { Name = "宝鸡", Adm1 = "陕西省", Adm2 = "宝鸡市", Country = "中国", Lat = 34.36, Lon = 107.24 },

            // === 甘肃省 ===
            new() { Name = "兰州", Adm1 = "甘肃省", Adm2 = "兰州市", Country = "中国", Lat = 36.06, Lon = 103.83 },

            // === 青海省 ===
            new() { Name = "西宁", Adm1 = "青海省", Adm2 = "西宁市", Country = "中国", Lat = 36.62, Lon = 101.78 },

            // === 宁夏回族自治区 ===
            new() { Name = "银川", Adm1 = "宁夏回族自治区", Adm2 = "银川市", Country = "中国", Lat = 38.49, Lon = 106.23 },

            // === 新疆维吾尔自治区 ===
            new() { Name = "乌鲁木齐", Adm1 = "新疆维吾尔自治区", Adm2 = "乌鲁木齐市", Country = "中国", Lat = 43.83, Lon = 87.62 },

            // === 西藏自治区 ===
            new() { Name = "拉萨", Adm1 = "西藏自治区", Adm2 = "拉萨市", Country = "中国", Lat = 29.65, Lon = 91.13 },

            // === 台湾省 ===
            new() { Name = "台北", Adm1 = "台湾省", Adm2 = "台北市", Country = "中国", Lat = 25.03, Lon = 121.57 },
            new() { Name = "高雄", Adm1 = "台湾省", Adm2 = "高雄市", Country = "中国", Lat = 22.63, Lon = 120.30 },
        };

        /// <summary>
        /// IP 定位获取坐标（使用 ip-api.com，免费HTTP接口）
        /// </summary>
        private async Task<(double Lat, double Lon, string City)?> GetLocationByIpAsync()
        {
            // ip-api.com 免费接口，HTTP only
            var url = "http://ip-api.com/json/?lang=zh-CN";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (SafeGetString(root, "status") != "success")
            {
                Debug.WriteLine("IP定位失败");
                return null;
            }

            var lat = SafeGetDouble(root, "lat");
            var lon = SafeGetDouble(root, "lon");
            var city = SafeGetString(root, "city");

            // ip-api 返回的 city 可能是英文，优先用 regionName
            if (root.TryGetProperty("regionName", out var regionName))
            {
                city = regionName.GetString() ?? city;
            }

            return (lat, lon, city);
        }

        /// <summary>
        /// 用坐标查询实时天气
        /// </summary>
        private async Task<WeatherInfo?> GetWeatherNowAsync(string apiKey, string apiHost, double lat, double lon)
        {
            // 坐标格式：经度,纬度
            var location = $"{lon:F2},{lat:F2}";
            var url = $"https://{apiHost}/v7/weather/now?location={location}&key={apiKey}&lang=zh";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (SafeGetString(root, "code") != "200")
            {
                Debug.WriteLine($"天气查询失败: code={SafeGetString(root, "code")}");
                return null;
            }

            if (!root.TryGetProperty("now", out var now))
                return null;

            return new WeatherInfo
            {
                WeatherText = SafeGetString(now, "text"),
                Temperature = SafeGetString(now, "temp"),
                FeelsLike = SafeGetString(now, "feelsLike"),
                Humidity = SafeGetString(now, "humidity"),
                WindDir = SafeGetString(now, "windDir"),
                Icon = SafeGetString(now, "icon")
            };
        }

        /// <summary>
        /// 查询实时空气质量
        /// </summary>
        private async Task<WeatherInfo?> GetAirNowAsync(string apiKey, string apiHost, double lat, double lon)
        {
            var location = $"{lon:F2},{lat:F2}";
            var url = $"https://{apiHost}/v7/air/now?location={location}&key={apiKey}&lang=zh";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (SafeGetString(root, "code") != "200")
            {
                Debug.WriteLine($"空气质量查询失败: code={SafeGetString(root, "code")}");
                return null;
            }

            if (!root.TryGetProperty("now", out var now))
                return null;

            return new WeatherInfo
            {
                Aqi = SafeGetString(now, "aqi"),
                AirLevel = SafeGetString(now, "level"),
                AirCategory = SafeGetString(now, "category"),
                Pm2p5 = SafeGetString(now, "pm2p5"),
                Pm10 = SafeGetString(now, "pm10")
            };
        }

        /// <summary>
        /// 查询3天天气预报
        /// </summary>
        private async Task<List<ForecastDay>?> GetForecast3dAsync(string apiKey, string apiHost, double lat, double lon)
        {
            var location = $"{lon:F2},{lat:F2}";
            var url = $"https://{apiHost}/v7/weather/3d?location={location}&key={apiKey}&lang=zh";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (SafeGetString(root, "code") != "200")
                return null;

            if (!root.TryGetProperty("daily", out var daily))
                return null;

            var result = new List<ForecastDay>();
            foreach (var day in daily.EnumerateArray())
            {
                result.Add(new ForecastDay
                {
                    Date = SafeGetString(day, "fxDate"),
                    TextDay = SafeGetString(day, "textDay"),
                    TextNight = SafeGetString(day, "textNight"),
                    TempMax = SafeGetString(day, "tempMax"),
                    TempMin = SafeGetString(day, "tempMin"),
                    Humidity = SafeGetString(day, "humidity"),
                    WindDirDay = SafeGetString(day, "windDirDay"),
                    UvIndex = SafeGetString(day, "uvIndex")
                });
            }
            return result;
        }

        /// <summary>
        /// 查询天气预警
        /// </summary>
        private async Task<List<WeatherWarning>?> GetWarningNowAsync(string apiKey, string apiHost, double lat, double lon)
        {
            var location = $"{lon:F2},{lat:F2}";
            var url = $"https://{apiHost}/v7/warning/now?location={location}&key={apiKey}&lang=zh";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (SafeGetString(root, "code") != "200")
                return null;

            if (!root.TryGetProperty("warning", out var warning))
                return null;

            var result = new List<WeatherWarning>();
            foreach (var w in warning.EnumerateArray())
            {
                result.Add(new WeatherWarning
                {
                    Id = w.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    Title = w.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                    TypeName = w.TryGetProperty("typeName", out var tn) ? tn.GetString() ?? "" : "",
                    Level = w.TryGetProperty("level", out var lv) ? lv.GetString() ?? "" : "",
                    Status = w.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "",
                    Text = w.TryGetProperty("text", out var txt) ? txt.GetString() ?? "" : "",
                    StartTime = w.TryGetProperty("startTime", out var sTime) ? sTime.GetString() ?? "" : "",
                    EndTime = w.TryGetProperty("endTime", out var eTime) ? eTime.GetString() ?? "" : ""
                });
            }
            return result;
        }

        /// <summary>
        /// 查询生活指数（运动/洗车/穿衣/紫外线/感冒）
        /// </summary>
        private async Task<List<WeatherIndex>?> GetIndices1dAsync(string apiKey, string apiHost, double lat, double lon)
        {
            var location = $"{lon:F2},{lat:F2}";
            // type=1(运动) 2(洗车) 3(穿衣) 5(紫外线) 9(感冒)
            var url = $"https://{apiHost}/v7/indices/1d?type=1,2,3,5,9&location={location}&key={apiKey}&lang=zh";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (SafeGetString(root, "code") != "200")
                return null;

            if (!root.TryGetProperty("daily", out var daily))
                return null;

            var result = new List<WeatherIndex>();
            foreach (var item in daily.EnumerateArray())
            {
                result.Add(new WeatherIndex
                {
                    Type = item.TryGetProperty("type", out var tp) ? tp.GetString() ?? "" : "",
                    Name = item.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "",
                    Level = item.TryGetProperty("level", out var lv) ? lv.GetString() ?? "" : "",
                    Category = item.TryGetProperty("category", out var ct) ? ct.GetString() ?? "" : "",
                    Text = item.TryGetProperty("text", out var txt) ? txt.GetString() ?? "" : ""
                });
            }
            return result;
        }

        /// <summary>
        /// 查询日出日落时间
        /// </summary>
        private async Task<(string Sunrise, string Sunset)?> GetSunriseSunsetAsync(string apiKey, string apiHost, double lat, double lon)
        {
            var location = $"{lon:F2},{lat:F2}";
            var date = DateTime.Now.ToString("yyyyMMdd");
            var url = $"https://{apiHost}/v7/astronomy/sunrise-sunset?location={location}&date={date}&key={apiKey}&lang=zh";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (SafeGetString(root, "code") != "200")
                return null;

            var sunrise = root.TryGetProperty("sunrise", out var sr) ? sr.GetString() ?? "" : "";
            var sunset = root.TryGetProperty("sunset", out var ss) ? ss.GetString() ?? "" : "";

            return (sunrise, sunset);
        }

        /// <summary>
        /// 清除缓存（切换 API Key 时调用）
        /// </summary>
        public void ClearCache()
        {
            _cachedWeather = null;
            _cachedLat = 0;
            _cachedLon = 0;
            _cachedCityName = "";
            _lastWeatherRequest = DateTimeOffset.MinValue;
            _lastLocationRequest = DateTimeOffset.MinValue;

            // 清除每日数据缓存
            _lastDailyDate = "";
            _cachedForecast = null;
            _cachedSunrise = "";
            _cachedSunset = "";
            _cachedIndices = null;

            // 清除预警缓存
            _lastWarningRequest = DateTimeOffset.MinValue;
            _cachedWarnings = null;
        }

        /// <summary>
        /// 安全获取 JSON 字符串属性（不存在时返回默认值）
        /// </summary>
        private static string SafeGetString(JsonElement element, string propertyName, string defaultValue = "")
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString() ?? defaultValue;
            return defaultValue;
        }

        /// <summary>
        /// 安全获取 JSON 数字属性（不存在时返回默认值）
        /// </summary>
        private static double SafeGetDouble(JsonElement element, string propertyName, double defaultValue = 0)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
                return prop.GetDouble();
            return defaultValue;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// 天气信息（含空气质量、预报、预警、指数、天文）
    /// </summary>
    public class WeatherInfo
    {
        // === 实时天气 ===
        /// <summary>城市名</summary>
        public string CityName { get; set; } = "";
        /// <summary>天气文字（晴/多云/小雨...）</summary>
        public string WeatherText { get; set; } = "";
        /// <summary>当前温度（°C）</summary>
        public string Temperature { get; set; } = "";
        /// <summary>体感温度（°C）</summary>
        public string FeelsLike { get; set; } = "";
        /// <summary>湿度（%）</summary>
        public string Humidity { get; set; } = "";
        /// <summary>风向</summary>
        public string WindDir { get; set; } = "";
        /// <summary>天气图标代码</summary>
        public string Icon { get; set; } = "";

        // === 空气质量 ===
        /// <summary>空气质量指数</summary>
        public string Aqi { get; set; } = "";
        /// <summary>空气质量等级（1-优 2-良 3-轻度 4-中度 5-重度 6-严重）</summary>
        public string AirLevel { get; set; } = "";
        /// <summary>空气质量级别文字（优/良/轻度污染...）</summary>
        public string AirCategory { get; set; } = "";
        /// <summary>PM2.5 浓度（μg/m³）</summary>
        public string Pm2p5 { get; set; } = "";
        /// <summary>PM10 浓度（μg/m³）</summary>
        public string Pm10 { get; set; } = "";

        // === 3天预报 ===
        public List<ForecastDay> Forecast { get; set; } = new();

        // === 天气预警 ===
        public List<WeatherWarning> Warnings { get; set; } = new();

        // === 生活指数 ===
        public List<WeatherIndex> Indices { get; set; } = new();

        // === 天文 ===
        /// <summary>日出时间（如 "05:32"）</summary>
        public string Sunrise { get; set; } = "";
        /// <summary>日落时间（如 "19:15"）</summary>
        public string Sunset { get; set; } = "";
    }

    /// <summary>
    /// 预报每日数据
    /// </summary>
    public class ForecastDay
    {
        /// <summary>日期（yyyy-MM-dd）</summary>
        public string Date { get; set; } = "";
        /// <summary>白天天气文字</summary>
        public string TextDay { get; set; } = "";
        /// <summary>夜间天气文字</summary>
        public string TextNight { get; set; } = "";
        /// <summary>最高温度</summary>
        public string TempMax { get; set; } = "";
        /// <summary>最低温度</summary>
        public string TempMin { get; set; } = "";
        /// <summary>湿度</summary>
        public string Humidity { get; set; } = "";
        /// <summary>白天风向</summary>
        public string WindDirDay { get; set; } = "";
        /// <summary>紫外线指数</summary>
        public string UvIndex { get; set; } = "";
    }

    /// <summary>
    /// 天气预警
    /// </summary>
    public class WeatherWarning
    {
        /// <summary>预警 ID</summary>
        public string Id { get; set; } = "";
        /// <summary>预警标题</summary>
        public string Title { get; set; } = "";
        /// <summary>预警类型名称</summary>
        public string TypeName { get; set; } = "";
        /// <summary>预警等级</summary>
        public string Level { get; set; } = "";
        /// <summary>预警状态</summary>
        public string Status { get; set; } = "";
        /// <summary>预警详细描述</summary>
        public string Text { get; set; } = "";
        /// <summary>预警开始时间</summary>
        public string StartTime { get; set; } = "";
        /// <summary>预警结束时间</summary>
        public string EndTime { get; set; } = "";
    }

    /// <summary>
    /// 生活指数
    /// </summary>
    public class WeatherIndex
    {
        /// <summary>指数类型（1=运动 2=洗车 3=穿衣 5=紫外线 9=感冒）</summary>
        public string Type { get; set; } = "";
        /// <summary>指数名称</summary>
        public string Name { get; set; } = "";
        /// <summary>指数等级</summary>
        public string Level { get; set; } = "";
        /// <summary>指数级别文字（适宜/较适宜...）</summary>
        public string Category { get; set; } = "";
        /// <summary>详细描述</summary>
        public string Text { get; set; } = "";
    }

    /// <summary>
    /// 城市信息（GeoAPI 查询结果）
    /// </summary>
    public class CityInfo
    {
        /// <summary>
        /// 和风天气城市 ID
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// 城市名
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 国家
        /// </summary>
        public string Country { get; set; } = "";

        /// <summary>
        /// 省/州
        /// </summary>
        public string Adm1 { get; set; } = "";

        /// <summary>
        /// 市/区
        /// </summary>
        public string Adm2 { get; set; } = "";

        /// <summary>
        /// 纬度
        /// </summary>
        public double Lat { get; set; }

        /// <summary>
        /// 经度
        /// </summary>
        public double Lon { get; set; }

        /// <summary>
        /// 显示文本（如 "北京市, 中国"）
        /// </summary>
        public string DisplayText => string.IsNullOrEmpty(Adm1) || Adm1 == Name
            ? $"{Name}, {Country}"
            : $"{Name}, {Adm1}, {Country}";
    }
}
