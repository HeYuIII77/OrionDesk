using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using OrionDesk.BLL.Models;
using OrionDesk.BLL.Services;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 设置窗口 - 配置天气 API Key、城市选择和刷新频率
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly WeatherSettings _weatherSettings;
        private readonly WeatherService _weatherService;
        private List<CityInfo> _searchResults = new();

        /// <summary>
        /// 是否已保存
        /// </summary>
        public bool IsSaved { get; private set; }

        /// <summary>
        /// Git 同步刷新频率（读取用）
        /// </summary>
        public int GitSyncRefreshMinutes { get; private set; }

        public SettingsWindow(WeatherSettings weatherSettings, WeatherService weatherService, int gitSyncRefreshMinutes = 10)
        {
            InitializeComponent();
            Topmost = true;

            _weatherSettings = weatherSettings;
            _weatherService = weatherService;
            GitSyncRefreshMinutes = gitSyncRefreshMinutes;

            // 加载当前设置
            ApiKeyBox.Text = _weatherSettings.ApiKey;
            ApiHostBox.Text = _weatherSettings.ApiHost;
            RefreshBox.Text = _weatherSettings.RefreshMinutes.ToString();
            GitRefreshBox.Text = gitSyncRefreshMinutes.ToString();

            // 显示当前城市配置
            UpdateCurrentCityDisplay();

            // 搜索框回车触发搜索
            CitySearchBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                    CitySearchButton_Click(s!, e);
            };

            // 窗口关闭时自动保存
            Closing += (s, e) => ApplySettings();
        }

        /// <summary>
        /// 更新当前城市显示
        /// </summary>
        private void UpdateCurrentCityDisplay()
        {
            if (!string.IsNullOrWhiteSpace(_weatherSettings.CityName) &&
                _weatherSettings.CityLat != 0 && _weatherSettings.CityLon != 0)
            {
                CurrentCityText.Text = $"当前：{_weatherSettings.CityName}（{_weatherSettings.CityLat:F2}, {_weatherSettings.CityLon:F2}）";
                ClearCityButton.Visibility = Visibility.Visible;
            }
            else
            {
                CurrentCityText.Text = "当前：自动定位（IP）";
                ClearCityButton.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 搜索城市按钮
        /// </summary>
        private async void CitySearchButton_Click(object sender, RoutedEventArgs e)
        {
            var cityName = CitySearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(cityName))
            {
                CitySearchStatus.Text = "请输入城市名";
                return;
            }

            CitySearchStatus.Text = "搜索中...";
            CitySearchButton.IsEnabled = false;
            CityResultList.Visibility = Visibility.Collapsed;

            try
            {
                _searchResults = await _weatherService.SearchCityAsync(cityName);

                if (_searchResults.Count == 0)
                {
                    CitySearchStatus.Text = "未找到匹配城市";
                }
                else
                {
                    CitySearchStatus.Text = $"找到 {_searchResults.Count} 个城市，请点击选择：";
                    CityResultList.ItemsSource = _searchResults;
                    CityResultList.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                CitySearchStatus.Text = $"搜索失败: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[设置] 城市搜索异常: {ex.Message}");
            }
            finally
            {
                CitySearchButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 选择城市
        /// </summary>
        private void CityResultList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CityResultList.SelectedItem is CityInfo city)
            {
                _weatherSettings.CityId = city.Id;
                _weatherSettings.CityName = city.Name;
                _weatherSettings.CityLat = city.Lat;
                _weatherSettings.CityLon = city.Lon;

                CityResultList.Visibility = Visibility.Collapsed;
                CitySearchBox.Text = "";
                CitySearchStatus.Text = $"已选择：{city.DisplayText}";
                UpdateCurrentCityDisplay();
            }
        }

        /// <summary>
        /// 清除城市选择，恢复自动定位
        /// </summary>
        private void ClearCityButton_Click(object sender, RoutedEventArgs e)
        {
            _weatherSettings.CityId = "";
            _weatherSettings.CityName = "";
            _weatherSettings.CityLat = 0;
            _weatherSettings.CityLon = 0;

            CitySearchStatus.Text = "已清除，恢复自动定位";
            UpdateCurrentCityDisplay();
        }

        /// <summary>
        /// 将界面值应用到设置对象
        /// </summary>
        private void ApplySettings()
        {
            if (int.TryParse(RefreshBox.Text, out var minutes) && minutes >= 10)
            {
                _weatherSettings.RefreshMinutes = minutes;
            }
            if (int.TryParse(GitRefreshBox.Text, out var gitMinutes) && gitMinutes >= 5)
            {
                GitSyncRefreshMinutes = gitMinutes;
            }
            _weatherSettings.ApiKey = ApiKeyBox.Text.Trim();
            _weatherSettings.ApiHost = ApiHostBox.Text.Trim();
            IsSaved = true;
        }

        /// <summary>
        /// 保存按钮
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(RefreshBox.Text, out var minutes) || minutes < 10)
            {
                System.Windows.MessageBox.Show("天气刷新频率不能小于 10 分钟", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshBox.Focus();
                return;
            }
            if (!int.TryParse(GitRefreshBox.Text, out var gitMinutes) || gitMinutes < 5)
            {
                System.Windows.MessageBox.Show("Git 刷新频率不能小于 5 分钟", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                GitRefreshBox.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 取消按钮
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // 关闭窗口，Closing 事件会自动保存
            Close();
        }
    }
}
