using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Win32;

namespace PCLock
{
    /// <summary>配置换机迁移、时段格子规则生成、崩溃日志本地聚合（全部本地，不联网）。</summary>
    public static class ConfigIo
    {
        // 运行态不参与导出/导入，避免换机后把「正在锁屏 / 剩余秒数」也带过去
        static readonly string[] RuntimeKeys = new string[] {
            "LockFlag", "RemainingSeconds", "LastSeenUtc", "RestUntilUtc",
            "PendingRewardSec", "TamperLock", "LastKnownUtc"
        };
        static readonly string[] GlobalKeys = new string[] {
            "FirstRunDone", "PinSetupDone", "PinHash", "UnPinHash", "Salt", "USalt",
            "DurationMinutes", "Grade", "Subject", "AntiTamper"
        };

        static bool IsAllowedKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.StartsWith("U:", StringComparison.Ordinal)) return true;
            foreach (string k in GlobalKeys) if (k == name) return true;
            return false;
        }

        static bool IsRuntimeKey(string name)
        {
            foreach (string k in RuntimeKeys) if (k == name) return true;
            return false;
        }

        public static string ExportConfig()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"app\":\"PCLock\",\"schema\":1,\"values\":{");
            bool first = true;
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(Constants.RegPath))
                {
                    if (k != null)
                    {
                        foreach (string name in k.GetValueNames())
                        {
                            if (!IsAllowedKey(name) || IsRuntimeKey(name)) continue;
                            object v = k.GetValue(name);
                            if (v == null) continue;
                            if (!first) sb.Append(',');
                            first = false;
                            sb.Append('"').Append(MiniJson.Escape(name)).Append("\":{");
                            if (v is int || v is long)
                            {
                                sb.Append("\"t\":\"n\",\"v\":").Append(
                                    Convert.ToString(v, CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                sb.Append("\"t\":\"s\",\"v\":\"").Append(
                                    MiniJson.Escape(Convert.ToString(v, CultureInfo.InvariantCulture)))
                                    .Append('"');
                            }
                            sb.Append('}');
                        }
                    }
                }
            }
            catch (Exception) { return ""; }
            return sb.Append("}}").ToString();
        }

        /// <summary>返回 null 表示成功，否则返回首条错误。只写白名单键，运行态一律跳过。</summary>
        public static string ImportConfig(string json)
        {
            object root;
            try { root = MiniJson.Parse(json); }
            catch (Exception ex) { return "JSON 解析失败：" + ex.Message; }
            Dictionary<string, object> doc = root as Dictionary<string, object>;
            if (doc == null) return "根节点必须是对象";
            object app;
            if (!doc.TryGetValue("app", out app) || !(app is string) || (string)app != "PCLock")
                return "不是 PCLock 配置文件";
            object vals;
            if (!doc.TryGetValue("values", out vals)) return "缺少 values";
            Dictionary<string, object> values = vals as Dictionary<string, object>;
            if (values == null) return "values 必须是对象";

            int imported = 0;
            using (RegistryKey k = Registry.LocalMachine.CreateSubKey(Constants.RegPath))
            {
                if (k == null) return "无法写入注册表（需管理员）";
                foreach (KeyValuePair<string, object> pair in values)
                {
                    if (!IsAllowedKey(pair.Key) || IsRuntimeKey(pair.Key)) continue;
                    Dictionary<string, object> item = pair.Value as Dictionary<string, object>;
                    if (item == null) continue;
                    object t, v;
                    if (!item.TryGetValue("t", out t) || !item.TryGetValue("v", out v)) continue;
                    try
                    {
                        if (t is string && (string)t == "n")
                        {
                            long n;
                            if (v is double) n = (long)(double)v;
                            else if (!long.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out n)) continue;
                            if (n >= int.MinValue && n <= int.MaxValue)
                                k.SetValue(pair.Key, (int)n, RegistryValueKind.DWord);
                            else
                                k.SetValue(pair.Key, n, RegistryValueKind.QWord);
                        }
                        else k.SetValue(pair.Key, Convert.ToString(v, CultureInfo.InvariantCulture));
                        imported++;
                    }
                    catch (Exception) { return "写入失败：" + pair.Key; }
                }
            }
            return imported == 0 ? "没有可导入的配置项" : null;
        }

        /// <summary>7×24 小时布尔网格（day 0=周一）→ 多规则时段串；纯函数</summary>
        public static string ScheduleGridToRule(bool[,] grid, bool quizOnly)
        {
            if (grid == null || grid.GetLength(0) < 7 || grid.GetLength(1) < 24) return "";
            List<string> segs = new List<string>();
            string suffix = quizOnly ? "@quiz" : "";
            for (int d = 0; d < 7; d++)
            {
                int start = -1;
                for (int h = 0; h <= 24; h++)
                {
                    bool on = h < 24 && grid[d, h];
                    if (on && start < 0) start = h;
                    if (!on && start >= 0)
                    {
                        int end = h;
                        string e = end == 24 ? "23:59" : end.ToString("00") + ":00";
                        segs.Add((d + 1) + "|" + start.ToString("00") + ":00-" + e + suffix);
                        start = -1;
                    }
                }
            }
            return string.Join(";", segs.ToArray());
        }

        /// <summary>错误日志文本 → 异常类型计数摘要（本地聚合，不上传）</summary>
        public static string[] CrashSummary(string text)
        {
            string[] kinds = new string[] { "UI THREAD EXCEPTION", "UNHANDLED EXCEPTION",
                "WATCHDOG EXCEPTION", "Store.Get", "Store.Set", "DeductOffline", "FlushUsed" };
            int[] counts = new int[kinds.Length];
            if (!string.IsNullOrEmpty(text))
            {
                foreach (string line in text.Split('\n'))
                    for (int i = 0; i < kinds.Length; i++)
                        if (line.IndexOf(kinds[i], StringComparison.OrdinalIgnoreCase) >= 0) counts[i]++;
            }
            List<string> r = new List<string>();
            for (int i = 0; i < kinds.Length; i++)
                if (counts[i] > 0) r.Add(kinds[i] + " × " + counts[i]);
            if (r.Count == 0) r.Add("近期无异常记录");
            return r.ToArray();
        }
    }
}
