using OrionDesk.DAL;
using OrionDesk.BLL.Models;

namespace OrionDesk.BLL.Services
{
    public class WidgetManager
    {
        private readonly ConfigRepository _repo = new ConfigRepository();
        private AppSettings _settings = new AppSettings();

        public AppSettings Settings => _settings;

        /// <summary>
        /// 恢复期间为 true，此时不保存（防止初始化时覆盖配置）
        /// </summary>
        public bool IsRestoring { get; set; } = false;

        public void Load()
        {
            DataPath.EnsureDirectoriesExist();
            var loaded = _repo.Load<AppSettings>(DataPath.ConfigFile);
            if (loaded != null)
            {
                _settings = loaded;
                // 首次加载成功后创建 .gold 快照（只创建一次，之后不覆盖）
                _repo.CreateGoldSnapshot(DataPath.ConfigFile);
            }
        }

        public void Save()
        {
            if (IsRestoring) return;
            _settings.LastSaved = DateTime.Now;
            var count = _settings.Widgets.Count;
            _repo.Save(DataPath.ConfigFile, _settings);
            // 写日志到文件
            try
            {
                var logFile = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OrionDesk", "startup.log");
                System.IO.File.AppendAllText(logFile,
                    $"{DateTime.Now:HH:mm:ss.fff} [Save] 组件数={count}\n");
            }
            catch { }
        }

        public WidgetConfig AddWidget(string type, double x, double y, double width = 200, double height = 100)
        {
            var config = new WidgetConfig
            {
                Type = type,
                Position = new WidgetPosition { X = x, Y = y, Width = width, Height = height }
            };
            _settings.Widgets.Add(config);
            return config;
        }

        public bool RemoveWidget(string id)
        {
            var w = _settings.Widgets.FirstOrDefault(w => w.Id == id);
            if (w != null) { _settings.Widgets.Remove(w); return true; }
            return false;
        }

        public List<WidgetConfig> GetAllWidgets() => _settings.Widgets.ToList();
    }
}
