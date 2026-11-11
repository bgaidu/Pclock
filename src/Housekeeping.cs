using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PCLock
{
    /// <summary>
    /// 运行期维护（日志保留期、注册表统计键上限）。
    /// 判定逻辑全部是纯函数，便于单测；文件操作只做薄封装，任何失败都静默，
    /// 绝不因为清理失败影响主程序（与 AuditLog 的容错口径一致）。
    /// </summary>
    public static class Housekeeping
    {
        // ===== 审计日志保留期 =====

        /// <summary>
        /// 从文件名列表里挑出超过保留期的日志（形如 yyyy-MM-dd.log）。
        /// keepDays &lt;= 0 表示永久保留，返回空列表；其他文件一律忽略。
        /// 语义：保留 today-keepDays 当天及之后，只删更早的。
        /// </summary>
        public static List<string> SelectExpiredLogs(string[] fileNames, DateTime today, int keepDays)
        {
            List<string> r = new List<string>();
            if (fileNames == null || keepDays <= 0) return r;
            DateTime cutoff = today.Date.AddDays(-keepDays);
            foreach (string name in fileNames)
            {
                DateTime d;
                if (!TryParseLogDay(name, out d)) continue;
                if (d < cutoff) r.Add(name);
            }
            return r;
        }

        /// <summary>解析日志文件名里的日期；非日期命名或非 .log 返回 false</summary>
        public static bool TryParseLogDay(string fileName, out DateTime day)
        {
            day = DateTime.MinValue;
            if (string.IsNullOrEmpty(fileName)) return false;
            string n = fileName;
            int slash = n.LastIndexOfAny(new char[] { '\\', '/' });
            if (slash >= 0) n = n.Substring(slash + 1);
            if (!n.EndsWith(".log", StringComparison.OrdinalIgnoreCase)) return false;
            string stem = n.Substring(0, n.Length - 4);
            return DateTime.TryParseExact(stem, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
        }

        /// <summary>删除超过保留期的日志文件，返回实际删除数量（IO 薄封装，失败静默）</summary>
        public static int CleanupLogs(string dir, DateTime today, int keepDays)
        {
            int n = 0;
            if (string.IsNullOrEmpty(dir) || keepDays <= 0) return 0;
            try
            {
                if (!Directory.Exists(dir)) return 0;
                string[] files = Directory.GetFiles(dir, "*.log");
                List<string> expired = SelectExpiredLogs(files, today, keepDays);
                foreach (string path in expired)
                {
                    try { File.Delete(path); n++; }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
            return n;
        }

        // ===== 按题统计键上限（防注册表无限膨胀）=====

        /// <summary>
        /// 统计键超过 cap 时，挑出应删除的 key，使剩余恰好 cap 个。
        /// 优先级：先删「作答次数少」的；次数相同再删「正确率高」的
        /// （正确率高说明孩子已经掌握，留着价值最低）。
        /// </summary>
        public static List<string> SelectKeysToDrop(string[] keys, string[] values, int cap)
        {
            List<string> drop = new List<string>();
            if (keys == null || keys.Length == 0) return drop;
            if (cap < 0) cap = 0;
            if (keys.Length <= cap) return drop;

            int n = keys.Length;
            int[] answered = new int[n];
            int[] accuracy = new int[n];
            for (int i = 0; i < n; i++)
            {
                ParseStat(values != null && i < values.Length ? values[i] : null, out answered[i], out accuracy[i]);
            }

            List<int> idx = new List<int>();
            for (int i = 0; i < n; i++) idx.Add(i);
            idx.Sort(delegate(int a, int b)
            {
                if (answered[a] != answered[b]) return answered[a].CompareTo(answered[b]); // 次数少的先删
                if (accuracy[a] != accuracy[b]) return accuracy[b].CompareTo(accuracy[a]); // 正确率高的先删
                return string.CompareOrdinal(keys[a], keys[b]);                            // 稳定排序
            });

            int excess = n - cap;
            for (int i = 0; i < excess && i < idx.Count; i++) drop.Add(keys[idx[i]]);
            return drop;
        }

        /// <summary>解析 shown|answered|correct；格式非法按「0 次、0%」处理（最该删）</summary>
        static void ParseStat(string stored, out int answered, out int accuracy)
        {
            answered = 0;
            accuracy = 0;
            if (string.IsNullOrEmpty(stored)) return;
            string[] p = stored.Split('|');
            if (p.Length != 3) return;
            int shown, right;
            if (!int.TryParse(p[1], out answered)) { answered = 0; return; }
            if (!int.TryParse(p[2], out right)) return;
            if (!int.TryParse(p[0], out shown)) shown = answered;
            if (answered > 0) accuracy = (int)(100L * right / answered);
        }

        /// <summary>是否需要触发一次清理（超过上限 + 缓冲，避免每次作答都扫注册表）</summary>
        public static bool ShouldPruneStat(int keyCount, int cap)
        {
            if (cap <= 0) cap = 1;
            return keyCount > cap + 50;
        }
    }
}
