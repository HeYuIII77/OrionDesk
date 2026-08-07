using System;
using System.Windows;
using System.Windows.Controls;
using OrionDesk.BLL.Services;

// 避免 System.Drawing / System.Windows.Forms 命名冲突
using Media = System.Windows.Media;
using Orientation = System.Windows.Controls.Orientation;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 天气详情弹窗 - 展示完整天气信息（实时/空气/预报/预警/指数/天文）
    /// </summary>
    public partial class WeatherDetailWindow : Window
    {
        public WeatherDetailWindow(WeatherInfo weather)
        {
            InitializeComponent();
            Loaded += (s, e) => BuildContent(weather);
        }

        /// <summary>
        /// 动态构建天气详情内容
        /// </summary>
        private void BuildContent(WeatherInfo weather)
        {
            ContentPanel.Children.Clear();

            if (weather == null)
            {
                ContentPanel.Children.Add(new TextBlock
                {
                    Text = "暂无天气数据",
                    Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0x88, 0xFF, 0xFF)),
                    FontSize = 13,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0)
                });
                return;
            }

            // === 城市标题 ===
            ContentPanel.Children.Add(new TextBlock
            {
                Text = weather.CityName,
                Foreground = Media.Brushes.White,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            // === 实时天气 ===
            AddSectionTitle("☁ 实时天气");
            AddInfoRow("天气", weather.WeatherText);
            AddInfoRow("温度", $"{weather.Temperature}°C");
            AddInfoRow("体感", $"{weather.FeelsLike}°C");
            AddInfoRow("湿度", $"{weather.Humidity}%");
            AddInfoRow("风向", weather.WindDir);

            // === 空气质量 ===
            if (!string.IsNullOrEmpty(weather.Aqi))
            {
                AddSectionTitle("🌿 空气质量");
                var airColor = GetAirColor(weather.AirLevel);
                AddInfoRow("AQI", weather.Aqi, airColor);
                AddInfoRow("等级", weather.AirCategory, airColor);
                AddInfoRow("PM2.5", $"{weather.Pm2p5} μg/m³");
                AddInfoRow("PM10", $"{weather.Pm10} μg/m³");
            }

            // === 天气预警 ===
            if (weather.Warnings.Count > 0)
            {
                AddSectionTitle("⚠ 天气预警");
                foreach (var w in weather.Warnings)
                {
                    var warningBorder = new Border
                    {
                        Background = new Media.SolidColorBrush(Media.Color.FromArgb(0x33, 0xE7, 0x48, 0x56)),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(10, 8, 10, 8),
                        Margin = new Thickness(0, 0, 0, 6)
                    };
                    var panel = new StackPanel();
                    panel.Children.Add(new TextBlock
                    {
                        Text = w.Title,
                        Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0xFF, 0x6B, 0x6B)),
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold
                    });
                    if (!string.IsNullOrEmpty(w.TypeName) || !string.IsNullOrEmpty(w.Level))
                    {
                        panel.Children.Add(new TextBlock
                        {
                            Text = $"{w.TypeName} {w.Level}",
                            Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0xFF, 0xAA, 0xAA)),
                            FontSize = 11,
                            Margin = new Thickness(0, 2, 0, 0)
                        });
                    }
                    if (!string.IsNullOrEmpty(w.Text))
                    {
                        panel.Children.Add(new TextBlock
                        {
                            Text = w.Text,
                            Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0xCC, 0xCC, 0xCC)),
                            FontSize = 11,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 4, 0, 0)
                        });
                    }
                    warningBorder.Child = panel;
                    ContentPanel.Children.Add(warningBorder);
                }
            }

            // === 3天预报 ===
            if (weather.Forecast.Count > 0)
            {
                AddSectionTitle("📅 未来天气");
                foreach (var day in weather.Forecast)
                {
                    var card = new Border
                    {
                        Style = (Style)FindResource("ForecastCard"),
                        Margin = new Thickness(0, 0, 0, 6)
                    };
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

                    // 日期
                    var dateText = new TextBlock
                    {
                        Text = FormatDate(day.Date),
                        Foreground = Media.Brushes.White,
                        FontSize = 12,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center
                    };
                    Grid.SetColumn(dateText, 0);
                    grid.Children.Add(dateText);

                    // 天气
                    var weatherText = new TextBlock
                    {
                        Text = day.TextDay == day.TextNight
                            ? day.TextDay
                            : $"{day.TextDay} → {day.TextNight}",
                        Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0xCC, 0xFF, 0xCC)),
                        FontSize = 12,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center
                    };
                    Grid.SetColumn(weatherText, 1);
                    grid.Children.Add(weatherText);

                    // 温度
                    var tempText = new TextBlock
                    {
                        Text = $"{day.TempMin}° ~ {day.TempMax}°",
                        Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0xFF, 0xCC, 0x88)),
                        FontSize = 12,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center
                    };
                    Grid.SetColumn(tempText, 2);
                    grid.Children.Add(tempText);

                    card.Child = grid;
                    ContentPanel.Children.Add(card);
                }
            }

            // === 生活指数 ===
            if (weather.Indices.Count > 0)
            {
                AddSectionTitle("💡 生活指数");
                foreach (var idx in weather.Indices)
                {
                    var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
                    var header = new StackPanel { Orientation = Orientation.Horizontal };
                    header.Children.Add(new TextBlock
                    {
                        Text = $"{GetIndexIcon(idx.Type)} {idx.Name}",
                        Foreground = Media.Brushes.White,
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold
                    });
                    header.Children.Add(new TextBlock
                    {
                        Text = $"  {idx.Category}",
                        Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0xFF, 0xCC, 0x88)),
                        FontSize = 12,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center
                    });
                    panel.Children.Add(header);

                    if (!string.IsNullOrEmpty(idx.Text))
                    {
                        panel.Children.Add(new TextBlock
                        {
                            Text = idx.Text,
                            Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0xAA, 0xAA, 0xAA)),
                            FontSize = 11,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 2, 0, 0)
                        });
                    }
                    ContentPanel.Children.Add(panel);
                }
            }

            // === 天文 ===
            if (!string.IsNullOrEmpty(weather.Sunrise) || !string.IsNullOrEmpty(weather.Sunset))
            {
                AddSectionTitle("🌅 天文");
                var astroPanel = new StackPanel { Orientation = Orientation.Horizontal };
                if (!string.IsNullOrEmpty(weather.Sunrise))
                {
                    astroPanel.Children.Add(new TextBlock
                    {
                        Text = $"☀ 日出 {weather.Sunrise}",
                        Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0xFF, 0xDD, 0x88)),
                        FontSize = 12,
                        Margin = new Thickness(0, 0, 20, 0)
                    });
                }
                if (!string.IsNullOrEmpty(weather.Sunset))
                {
                    astroPanel.Children.Add(new TextBlock
                    {
                        Text = $"🌙 日落 {weather.Sunset}",
                        Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0xAA, 0xCC, 0xFF)),
                        FontSize = 12
                    });
                }
                ContentPanel.Children.Add(astroPanel);
            }

            // 底部间距
            ContentPanel.Children.Add(new FrameworkElement { Height = 10 });
        }

        #region 辅助方法

        private void AddSectionTitle(string title)
        {
            ContentPanel.Children.Add(new TextBlock
            {
                Text = title,
                Style = (Style)FindResource("SectionTitle"),
                Margin = new Thickness(0, 12, 0, 8)
            });
        }

        private void AddInfoRow(string label, string value, Media.Color? valueColor = null)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                Style = (Style)FindResource("InfoLabel")
            });
            var valueBlock = new TextBlock
            {
                Text = value,
                Style = (Style)FindResource("InfoValue")
            };
            if (valueColor.HasValue)
                valueBlock.Foreground = new Media.SolidColorBrush(valueColor.Value);
            panel.Children.Add(valueBlock);
            ContentPanel.Children.Add(panel);
        }

        private static Media.Color GetAirColor(string level)
        {
            return level switch
            {
                "1" => Media.Color.FromRgb(0x30, 0xBB, 0x43), // 优 - 绿
                "2" => Media.Color.FromRgb(0xFF, 0xB9, 0x00), // 良 - 黄
                "3" => Media.Color.FromRgb(0xFF, 0x8C, 0x00), // 轻度 - 橙
                "4" => Media.Color.FromRgb(0xE7, 0x48, 0x56), // 中度 - 红
                "5" => Media.Color.FromRgb(0xCC, 0x00, 0x33), // 重度 - 深红
                "6" => Media.Color.FromRgb(0x99, 0x00, 0x33), // 严重 - 紫红
                _ => Media.Colors.White
            };
        }

        private static string GetIndexIcon(string type)
        {
            return type switch
            {
                "1" => "🏃", // 运动
                "2" => "🚗", // 洗车
                "3" => "👔", // 穿衣
                "5" => "☀",  // 紫外线
                "9" => "🤧", // 感冒
                _ => "📊"
            };
        }

        private static string FormatDate(string dateStr)
        {
            if (DateTime.TryParse(dateStr, out var date))
            {
                var today = DateTime.Today;
                if (date == today) return "今天";
                if (date == today.AddDays(1)) return "明天";
                if (date == today.AddDays(2)) return "后天";
                return date.ToString("MM/dd ddd");
            }
            return dateStr;
        }

        #endregion
    }
}
