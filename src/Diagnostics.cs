using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PCLock
{
    /// <summary>
    /// 一键诊断包：把排障需要的信息打成一个 txt。
    /// 纯文本组装逻辑与磁盘 IO 分离，前者可被零依赖单测覆盖；
    /// 报告**不写入任何 PIN / 密码哈希**，并对日志行做兜底脱敏。
    /// </summary>
    public static class Diagnostics
    {
        public const int MaxTailChars = 8000;
        public const int MaxLogFiles = 7;

        // 命中即整行替换：宁可少一点信息，也不能把凭据带出本机
        static readonly string[] SensitiveKeys = new string[]
        {
            "pinhash", "passwordhash", "uninstallhash", "parentpin",
            "pbkdf2", "salt=", "secret", "token",
            "密码哈希", "卸载密码哈希", "家长pin值"
        };

        public static bool IsSensitive(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string s = line.ToLowerInvariant();
            foreach (string k in SensitiveKeys)
                if (s.IndexOf(k, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>逐行脱敏；普通日志行原样保留</summary>
        public static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
                sb.AppendLine(IsSensitive(raw) ? "[已脱敏：该行包含敏感字段]" : raw);
            return sb.ToString();
        }

        /// <summary>
        /// 读取错误日志文本：优先 log\error-*.log（取最近 2 天拼接），
        /// 兼容旧版本 exe 同目录的 err.txt（升级后旧文件不迁移也能看到）。
        /// </summary>
        public static string ReadErrorLogText(string installDir)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                string logDir = Path.Combine(installDir, "log");
                if (Directory.Exists(logDir))
                {
                    string[] errs = Directory.GetFiles(logDir, "error-*.log");
                    Array.Sort(errs, StringComparer.OrdinalIgnoreCase);
                    int start = Math.Max(0, errs.Length - 2);
                    for (int i = start; i < errs.Length; i++)
                    {
                        try { sb.Append(File.ReadAllText(errs[i], Encoding.UTF8)); } catch (Exception) { }
                        if (sb.Length > 0) sb.AppendLine();
                    }
                }
                if (sb.Length == 0)
                {
                    string legacy = Path.Combine(installDir, "err.txt");
                    if (File.Exists(legacy)) sb.Append(File.ReadAllText(legacy, Encoding.UTF8));
                }
            }
            catch (Exception) { }
            return sb.ToString();
        }

        /// <summary>只保留末尾 maxChars 个字符（避免错误日志无限增长把报告撑爆）</summary>
        public static string Tail(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return "(空)";
            if (maxChars <= 0 || text.Length <= maxChars) return text;
            return "...(前面已省略 " + (text.Length - maxChars) + " 字符)..." +
                Environment.NewLine + text.Substring(text.Length - maxChars);
        }

        public static string FileName(DateTime now)
        {
            return "PCLock-diag-" + now.ToString("yyyyMMdd-HHmmss") + ".txt";
        }

        /// <summary>报告组装（纯函数，单测可覆盖）</summary>
        public static string BuildReport(string version, string commit, string installDir,
            string[] fileLines, string errText, string[] logSections,
            string configSummary, DateTime now)
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("PCLock 诊断报告");
            b.AppendLine("生成时间: " + now.ToString("yyyy-MM-dd HH:mm:ss"));
            b.AppendLine("版本: v" + (version ?? "?") + " (" + (commit ?? "?") + ")");
            b.AppendLine("安装目录: " + (installDir ?? "?"));
            b.AppendLine("环境: " + Environment.OSVersion.VersionString + " / CLR " + Environment.Version);
            b.AppendLine("说明: 本报告不含家长 PIN 与密码哈希；发给维护者前可自行通读一遍。");

            b.AppendLine();
            b.AppendLine("===== [1] 安装目录文件清单 =====");
            if (fileLines != null)
                foreach (string l in fileLines) b.AppendLine(Redact(l));
            else b.AppendLine("(读取失败)");

            b.AppendLine();
            b.AppendLine("===== [2] 错误日志（末尾 " + MaxTailChars + " 字符） =====");
            b.AppendLine(Redact(Tail(errText, MaxTailChars)));

            b.AppendLine();
            b.AppendLine("===== [3] 最近 " + MaxLogFiles + " 个审计日志 =====");
            if (logSections != null && logSections.Length > 0)
                foreach (string s in logSections) b.AppendLine(Redact(Tail(s, MaxTailChars / MaxLogFiles)));
            else b.AppendLine("(无日志)");

            b.AppendLine();
            b.AppendLine("===== [4] 配置摘要（不含凭据） =====");
            b.AppendLine(Redact(configSummary ?? "(无)"));
            return b.ToString();
        }

        /// <summary>收集现场并写入文件；返回路径。任何单段读取失败都降级而非中断。</summary>
        public static string CollectAndSave(string installDir, string version, string commit,
            string configSummary, string outputPath)
        {
            string[] fileLines = null;
            try
            {
                List<string> lines = new List<string>();
                foreach (string f in Directory.GetFiles(installDir, "*", SearchOption.TopDirectoryOnly))
                {
                    FileInfo fi = new FileInfo(f);
                    lines.Add(fi.Name + "  " + fi.Length + " B  " + fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                }
                foreach (string d in Directory.GetDirectories(installDir)) lines.Add("[目录] " + Path.GetFileName(d));
                lines.Sort(StringComparer.OrdinalIgnoreCase);
                fileLines = lines.ToArray();
            }
            catch (Exception) { fileLines = null; }

            string errText;
            try { errText = ReadErrorLogText(installDir); }
            catch (Exception) { errText = "(读取错误日志失败)"; }

            List<string> logs = new List<string>();
            try
            {
                string logDir = Path.Combine(installDir, "log");
                if (!Directory.Exists(logDir)) logDir = Path.Combine(installDir, "logs");   // 兼容旧目录
                if (Directory.Exists(logDir))
                {
                    string[] files = Directory.GetFiles(logDir, "audit-*.log");
                    if (files.Length == 0) files = Directory.GetFiles(logDir, "*.log");
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    int start = Math.Max(0, files.Length - MaxLogFiles);
                    for (int i = start; i < files.Length; i++)
                    {
                        try { logs.Add("--- " + Path.GetFileName(files[i]) + " ---" + Environment.NewLine + File.ReadAllText(files[i], Encoding.UTF8)); }
                        catch (Exception) { logs.Add("--- " + Path.GetFileName(files[i]) + " --- (读取失败)"); }
                    }
                }
            }
            catch (Exception) { }

            string report = BuildReport(version, commit, installDir, fileLines, errText,
                logs.ToArray(), configSummary, DateTime.Now);
            File.WriteAllText(outputPath, report, new UTF8Encoding(true));   // BOM：Windows 记事本不乱码
            return outputPath;
        }
    }
}
