using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace OrionDesk.DAL
{
    /// <summary>
    /// 配置仓库 - JSON 文件读写（线程安全，带重试）
    /// </summary>
    public class ConfigRepository
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static readonly object _saveLock = new object();

        /// <summary>
        /// 读取配置。主文件损坏时用 .bak 恢复，.bak 也损坏时用 .gold 恢复。
        /// </summary>
        public T? Load<T>(string filePath)
        {
            var data = TryRead<T>(filePath);
            if (data != null) return data;

            // 主文件损坏，用 .bak
            var bak = filePath + ".bak";
            if (File.Exists(bak))
            {
                data = TryRead<T>(bak);
                if (data != null)
                {
                    try { File.Copy(bak, filePath, overwrite: true); } catch (Exception ex) { Debug.WriteLine($"[配置] .bak 恢复复制失败: {ex.Message}"); }
                    return data;
                }
            }

            // .bak 也损坏，用 .gold（只读快照，永远不覆盖）
            var gold = filePath + ".gold";
            if (File.Exists(gold))
            {
                data = TryRead<T>(gold);
                if (data != null)
                {
                    try { File.Copy(gold, filePath, overwrite: true); } catch (Exception ex) { Debug.WriteLine($"[配置] .gold 恢复复制失败: {ex.Message}"); }
                    return data;
                }
            }

            return default;
        }

        /// <summary>
        /// 保存配置。原子写入 + 写之前备份 + 线程安全 + 重试。
        /// </summary>
        public void Save<T>(string filePath, T data)
        {
            lock (_saveLock)
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(data, JsonOpts);

                // 写之前备份：只有当前主文件有效时才备份（防止覆盖好的 .bak）
                if (File.Exists(filePath) && TryRead<T>(filePath) != null)
                {
                    try { File.Copy(filePath, filePath + ".bak", overwrite: true); }
                    catch (Exception ex) { Debug.WriteLine($"[配置] .bak 备份失败: {ex.Message}"); }
                }

                // 原子写入：tmp → rename，带重试（防杀毒软件/索引服务占用）
                var tmp = filePath + ".tmp";
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        File.WriteAllText(tmp, json);
                        File.Move(tmp, filePath, overwrite: true);
                        return;
                    }
                    catch (IOException) when (i < 2)
                    {
                        Thread.Sleep(100 * (i + 1));
                    }
                }
            }
        }

        /// <summary>
        /// 首次加载成功后调用，创建 .gold 只读快照。
        /// 如果 .gold 已存在则跳过（保留最初的完好配置）。
        /// </summary>
        public void CreateGoldSnapshot(string filePath)
        {
            var gold = filePath + ".gold";
            if (!File.Exists(gold) && File.Exists(filePath))
            {
                try { File.Copy(filePath, gold); }
                catch (Exception ex) { Debug.WriteLine($"[配置] .gold 快照创建失败: {ex.Message}"); }
            }
        }

        private T? TryRead<T>(string filePath)
        {
            if (!File.Exists(filePath)) return default;
            try
            {
                var json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return default;
                return JsonSerializer.Deserialize<T>(json, JsonOpts);
            }
            catch
            {
                return default;
            }
        }
    }
}
