using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PCLock
{
    /// <summary>
    /// 多题库文件支持（教材/单元组织）。约定：
    ///   banks\*.txt        每个文件是一个题库，文件名（不含扩展名）即「题库名」，可在设置里按题库名过滤；
    ///   chinese.txt / english.txt  旧版单文件题库，仍然优先于内置题库加载；
    /// 文件内格式与旧版一致，行首可加头部默认值，减少逐行重复：
    ///   #grade:3   #term:上   #bank:PEP三上
    ///   3|春天|chūn tiān|季节名
    /// 语文行：年级|词|拼音|释义[|学期][|题库名]
    /// 英语行：年级|英文|中文[|学期][|题库名]
    /// 纯解析逻辑集中在此，便于单测；不做缓存，由 QuestionBank 在启动时加载一次。
    /// </summary>
    public static class BankSet
    {
        /// <summary>题库目录名（程序目录下）</summary>
        public const string DirName = "banks";

        /// <summary>从文件路径取题库名（文件名去扩展名）</summary>
        public static string BankNameFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string name = Path.GetFileNameWithoutExtension(path);
            return name == null ? "" : name.Trim();
        }

        /// <summary>列出题库目录下所有 *.txt（按文件名排序，保证加载顺序稳定）；目录不存在返回空数组</summary>
        public static string[] ListBankFiles(string appDir)
        {
            if (string.IsNullOrEmpty(appDir)) return new string[0];
            try
            {
                string dir = Path.Combine(appDir, DirName);
                if (!Directory.Exists(dir)) return new string[0];
                string[] files = Directory.GetFiles(dir, "*.txt");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                return files;
            }
            catch (Exception) { return new string[0]; }
        }

        /// <summary>学期标记：0=全学年 1=上册 2=下册；兼容「上/下/1/2」</summary>
        public static int ParseTerm(string s)
        {
            if (s == null) return 0;
            string t = s.Trim();
            if (t == "上" || t == "1") return 1;
            if (t == "下" || t == "2") return 2;
            return 0;
        }

        /// <summary>读取文件的所有行；失败返回 null（调用方按「无此文件」处理）</summary>
        public static string[] ReadLines(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return File.ReadAllLines(path, Encoding.UTF8);
            }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// 解析语文题库行。defaultBank 为文件级默认题库名（通常取文件名），
        /// 块头 #grade/#term/#bank 可覆盖默认值，行内显式列优先于块头。
        /// </summary>
        public static List<WordEntry> ParseChinese(string[] lines, string defaultBank)
        {
            List<WordEntry> list = new List<WordEntry>();
            if (lines == null) return list;
            int defGrade = 0, defTerm = 0;
            string defBank = defaultBank == null ? "" : defaultBank;
            foreach (string raw in lines)
            {
                string line = raw == null ? "" : raw.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#')
                {
                    ApplyHeader(line, ref defGrade, ref defTerm, ref defBank, defaultBank);
                    continue;
                }
                string[] p = line.Split('|');
                if (p.Length < 4) continue;
                int g;
                if (!int.TryParse(p[0].Trim(), out g))
                {
                    if (defGrade <= 0) continue;
                    g = defGrade;
                }
                WordEntry we = new WordEntry();
                we.Grade = g;
                we.Word = p[1].Trim();
                we.Pinyin = p[2].Trim();
                we.Meaning = p[3].Trim();
                we.Term = p.Length >= 5 ? ParseTerm(p[4]) : defTerm;
                we.Bank = p.Length >= 6 && p[5].Trim().Length > 0 ? p[5].Trim() : defBank;
                if (we.Word.Length == 0) continue;
                list.Add(we);
            }
            return list;
        }

        /// <summary>解析英语题库行（格式见类注释）</summary>
        public static List<EnglishEntry> ParseEnglish(string[] lines, string defaultBank)
        {
            List<EnglishEntry> list = new List<EnglishEntry>();
            if (lines == null) return list;
            int defGrade = 0, defTerm = 0;
            string defBank = defaultBank == null ? "" : defaultBank;
            foreach (string raw in lines)
            {
                string line = raw == null ? "" : raw.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#')
                {
                    ApplyHeader(line, ref defGrade, ref defTerm, ref defBank, defaultBank);
                    continue;
                }
                string[] p = line.Split('|');
                if (p.Length < 3) continue;
                int g;
                if (!int.TryParse(p[0].Trim(), out g))
                {
                    if (defGrade <= 0) continue;
                    g = defGrade;
                }
                EnglishEntry ee = new EnglishEntry();
                ee.Grade = g;
                ee.En = p[1].Trim();
                ee.Zh = p[2].Trim();
                ee.Term = p.Length >= 4 ? ParseTerm(p[3]) : defTerm;
                ee.Bank = p.Length >= 5 && p[4].Trim().Length > 0 ? p[4].Trim() : defBank;
                if (ee.En.Length == 0) continue;
                list.Add(ee);
            }
            return list;
        }

        /// <summary>解析 #key:value 头部；未知键按普通注释忽略</summary>
        static void ApplyHeader(string line, ref int defGrade, ref int defTerm, ref string defBank, string fileBank)
        {
            string body = line.Substring(1).Trim();
            int c = body.IndexOf(':');
            if (c < 0) return;
            string key = body.Substring(0, c).Trim().ToLowerInvariant();
            string val = body.Substring(c + 1).Trim();
            if (val.Length == 0)
            {
                // #bank: 空值 = 取消文件级默认（回到「默认题库」）
                if (key == "bank") defBank = "";
                return;
            }
            if (key == "grade")
            {
                int g;
                if (int.TryParse(val, out g) && g >= 1 && g <= 6) defGrade = g;
            }
            else if (key == "term") defTerm = ParseTerm(val);
            else if (key == "bank") defBank = val;
            else if (key == "file") defBank = fileBank == null ? "" : fileBank;
        }

        /// <summary>
        /// 加载语文题库：先读旧版单文件 chinese.txt，再合并 banks\*.txt（同名题库不去重，交给质量门禁）。
        /// 两者都没有时返回空列表，由调用方回退到内置题库。
        /// </summary>
        public static List<WordEntry> LoadChinese(string appDir)
        {
            List<WordEntry> all = new List<WordEntry>();
            string[] legacy = ReadLines(Path.Combine(appDir, "chinese.txt"));
            if (legacy != null) all.AddRange(ParseChinese(legacy, ""));
            foreach (string f in ListBankFiles(appDir))
                all.AddRange(ParseChinese(ReadLines(f), BankNameFromPath(f)));
            return all;
        }

        /// <summary>加载英语题库（规则同语文）</summary>
        public static List<EnglishEntry> LoadEnglish(string appDir)
        {
            List<EnglishEntry> all = new List<EnglishEntry>();
            string[] legacy = ReadLines(Path.Combine(appDir, "english.txt"));
            if (legacy != null) all.AddRange(ParseEnglish(legacy, ""));
            foreach (string f in ListBankFiles(appDir))
                all.AddRange(ParseEnglish(ReadLines(f), BankNameFromPath(f)));
            return all;
        }

        /// <summary>可用题库名列表（去重排序），供设置界面提示；空题库名不返回</summary>
        public static string[] CollectBankNames(string appDir)
        {
            List<string> names = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string f in ListBankFiles(appDir))
            {
                string n = BankNameFromPath(f);
                if (n.Length == 0 || !seen.Add(n)) continue;
                names.Add(n);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names.ToArray();
        }
    }
}
