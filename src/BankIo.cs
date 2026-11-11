using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PCLock
{
    /// <summary>
    /// 零依赖严格 JSON 解析器（P1-8）。.NET Framework 4 无内置 JSON 库，
    /// 且本项目不引入第三方 DLL；只支持题库/配置需要的对象、数组、字符串、数字、布尔、null。
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            int i = 0;
            object v = ParseValue(text, ref i);
            SkipWs(text, ref i);
            if (i != text.Length) throw new FormatException("JSON 存在尾随内容");
            return v;
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON 意外结束");
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't') { Expect(s, ref i, "true"); return true; }
            if (c == 'f') { Expect(s, ref i, "false"); return false; }
            if (c == 'n') { Expect(s, ref i, "null"); return null; }
            return ParseNumber(s, ref i);
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.Ordinal);
            i++; // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("JSON 对象键必须是字符串");
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("JSON 缺少冒号");
                i++;
                d[key] = ParseValue(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON 对象未闭合");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException("JSON 对象分隔符非法");
            }
        }

        static List<object> ParseArray(string s, ref int i)
        {
            List<object> a = new List<object>();
            i++; // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return a; }
            while (true)
            {
                a.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("JSON 数组未闭合");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return a; }
                throw new FormatException("JSON 数组分隔符非法");
            }
        }

        static string ParseString(string s, ref int i)
        {
            StringBuilder sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    if (i >= s.Length) throw new FormatException("JSON 转义截断");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException("JSON \\u 转义截断");
                            int code;
                            if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture, out code))
                                throw new FormatException("JSON \\u 转义非法");
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default: throw new FormatException("JSON 未知转义");
                    }
                }
                else sb.Append(c);
            }
            throw new FormatException("JSON 字符串未闭合");
        }

        static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && s[i] == '-') i++;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i < s.Length && s[i] == '.') { i++; while (i < s.Length && char.IsDigit(s[i])) i++; }
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                i++;
                if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
            double v;
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float,
                CultureInfo.InvariantCulture, out v))
                throw new FormatException("JSON 数字非法");
            return v;
        }

        static void Expect(string s, ref int i, string token)
        {
            if (i + token.Length > s.Length || s.Substring(i, token.Length) != token)
                throw new FormatException("JSON 字面量非法");
            i += token.Length;
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n')) i++;
        }

        public static string Escape(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>题库 JSON 导入导出（P1-8）。chinese.txt / english.txt 仍优先，JSON 用于批量交换与备份。</summary>
    public static class BankIo
    {
        static List<object> Items(object root, string altKey)
        {
            Dictionary<string, object> d = root as Dictionary<string, object>;
            if (d == null) throw new FormatException("题库 JSON 根节点必须是对象");
            object v;
            if (!d.TryGetValue("items", out v))
            {
                // 合并导出格式：{"chinese":{"items":[...]},"english":{"items":[...]}}
                if (!d.TryGetValue(altKey, out v)) throw new FormatException("题库 JSON 缺少 items 数组");
                Dictionary<string, object> sub = v as Dictionary<string, object>;
                if (sub != null)
                {
                    object inner;
                    if (sub.TryGetValue("items", out inner)) v = inner;
                }
            }
            List<object> list = v as List<object>;
            if (list == null) throw new FormatException("题库 JSON 的 items 必须是数组");
            return list;
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return "";
            return v is string ? (string)v : Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        static int Int(Dictionary<string, object> d, string key)
        {
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return 0;
            if (v is double) return (int)(double)v;
            int n;
            return int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out n) ? n : 0;
        }

        public static List<WordEntry> ImportChinese(string json)
        {
            List<WordEntry> r = new List<WordEntry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (object o in Items(MiniJson.Parse(json), "chinese"))
            {
                Dictionary<string, object> d = o as Dictionary<string, object>;
                if (d == null) continue;
                int g = Int(d, "grade");
                string w = Str(d, "word").Trim();
                string p = Str(d, "pinyin").Trim();
                string m = Str(d, "meaning");   // 释义保留原样，Trim 会吞掉有意义的空白字符
                if (g < 1 || g > 6 || w.Length == 0 || p.Length == 0) continue;
                string key = w + "|" + p;
                if (!seen.Add(key)) continue;
                WordEntry e = new WordEntry();
                e.Grade = g; e.Word = w; e.Pinyin = p; e.Meaning = m;
                r.Add(e);
            }
            return r;
        }

        public static List<EnglishEntry> ImportEnglish(string json)
        {
            List<EnglishEntry> r = new List<EnglishEntry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (object o in Items(MiniJson.Parse(json), "english"))
            {
                Dictionary<string, object> d = o as Dictionary<string, object>;
                if (d == null) continue;
                int g = Int(d, "grade");
                string en = Str(d, "en").Trim();
                string zh = Str(d, "zh").Trim();
                if (g < 1 || g > 6 || en.Length == 0 || zh.Length == 0) continue;
                string key = en + "|" + zh;
                if (!seen.Add(key)) continue;
                EnglishEntry e = new EnglishEntry();
                e.Grade = g; e.En = en; e.Zh = zh;
                r.Add(e);
            }
            return r;
        }

        public static string ExportChinese(IList<WordEntry> items)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"items\":[");
            if (items != null)
            {
                bool first = true;
                foreach (WordEntry e in items)
                {
                    if (e == null) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{\"grade\":").Append(e.Grade)
                      .Append(",\"word\":\"").Append(MiniJson.Escape(e.Word))
                      .Append("\",\"pinyin\":\"").Append(MiniJson.Escape(e.Pinyin))
                      .Append("\",\"meaning\":\"").Append(MiniJson.Escape(e.Meaning))
                      .Append("\"}");
                }
            }
            return sb.Append("]}").ToString();
        }

        public static string ExportEnglish(IList<EnglishEntry> items)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"items\":[");
            if (items != null)
            {
                bool first = true;
                foreach (EnglishEntry e in items)
                {
                    if (e == null) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{\"grade\":").Append(e.Grade)
                      .Append(",\"en\":\"").Append(MiniJson.Escape(e.En))
                      .Append("\",\"zh\":\"").Append(MiniJson.Escape(e.Zh))
                      .Append("\"}");
                }
            }
            return sb.Append("]}").ToString();
        }

        /// <summary>题库质量门禁：年级范围、必填字段、重复项</summary>
        public static string ValidateChinese(IList<WordEntry> items)
        {
            if (items == null || items.Count == 0) return "题库为空";
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (WordEntry e in items)
            {
                if (e == null) return "存在空词条";
                if (e.Grade < 1 || e.Grade > 6) return "年级越界：" + e.Word;
                if (string.IsNullOrEmpty(e.Word) || string.IsNullOrEmpty(e.Pinyin))
                    return "词条缺字段：" + e.Word;
                if (!seen.Add(e.Word + "|" + e.Pinyin)) return "重复词条：" + e.Word;
                if (e.Pinyin.IndexOf('?') >= 0) return "拼音含占位符：" + e.Word;
            }
            return null;
        }
    }

    /// <summary>7 天使用记录（P1-10）：日期序列与 CSV 生成均为纯函数</summary>
    public static class UsageCsv
    {
        public static DateTime[] LastNDays(DateTime today, int n)
        {
            if (n < 1) n = 1;
            DateTime[] days = new DateTime[n];
            DateTime d = today.Date;
            // days[0] = n-1 天前，days[n-1] = 今天（与 CSV 行顺序一致）
            for (int i = 0; i < n; i++) days[i] = d.AddDays(-(n - 1 - i));
            return days;
        }

        public static string Build(DateTime today, IList<int> seconds)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("date,seconds,minutes").Append(Environment.NewLine);
            DateTime[] days = LastNDays(today, seconds == null ? 7 : seconds.Count);
            for (int i = 0; i < days.Length; i++)
            {
                int sec = (seconds != null && i < seconds.Count) ? seconds[i] : 0;
                sb.Append(days[i].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                  .Append(',').Append(sec.ToString(CultureInfo.InvariantCulture))
                  .Append(',').Append((sec / 60).ToString(CultureInfo.InvariantCulture))
                  .Append(Environment.NewLine);
            }
            return sb.ToString();
        }
    }
}
