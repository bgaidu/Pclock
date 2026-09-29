using System;
using System.Collections.Generic;

namespace PCLock
{
    public class MathQuestion
    {
        public string Text;
        public int Answer;
    }

    /// <summary>小学三年级水平口算题生成器（一批题目互不重复）</summary>
    public static class MathUtil
    {
        static Random rnd = new Random();

        /// <summary>生成 count 道互不重复的题目；重复的直接丢弃再抽，抽不出来时才允许重复兜底</summary>
        public static List<MathQuestion> NextBatch(int count)
        {
            List<MathQuestion> list = new List<MathQuestion>();
            HashSet<string> used = new HashSet<string>();
            int guard = 0;
            while (list.Count < count && guard < 2000)
            {
                guard++;
                int answer;
                string text = NextQuestion(out answer);
                if (used.Contains(text)) continue;
                used.Add(text);
                MathQuestion q = new MathQuestion();
                q.Text = text;
                q.Answer = answer;
                list.Add(q);
            }
            return list;
        }

        static string NextQuestion(out int answer)
        {
            int kind = rnd.Next(4);
            int a, b;
            switch (kind)
            {
                case 0: // 千以内加法
                    a = rnd.Next(56, 500);
                    b = rnd.Next(56, 500);
                    answer = a + b;
                    return a + " + " + b + " = ?";
                case 1: // 千以内减法（不出现负数）
                    a = rnd.Next(300, 1000);
                    b = rnd.Next(10, a);
                    answer = a - b;
                    return a + " - " + b + " = ?";
                case 2: // 两位数乘一位数
                    a = rnd.Next(13, 40);
                    b = rnd.Next(3, 10);
                    answer = a * b;
                    return a + " × " + b + " = ?";
                default: // 表内/整除除法
                    b = rnd.Next(3, 10);
                    answer = rnd.Next(12, 41);
                    a = b * answer;
                    return a + " ÷ " + b + " = ?";
            }
        }
    }
}
