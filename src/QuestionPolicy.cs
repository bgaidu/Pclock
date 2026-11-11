using System;
using System.Collections.Generic;

namespace PCLock
{
    /// <summary>
    /// 题库治理纯函数（P1-8）：难度分档、年级防超纲校验、批次质量门禁。
    /// 借鉴 PoHW 的「每层兜底都带 grade 上限」与题库质量自动审核，
    /// 但 PCLock 题库由算法生成，门禁只校验产出的这一批题，不改抽题算法。
    /// </summary>
    public static class QuestionPolicy
    {
        /// <summary>1=易 2=中 3=难：按年级分档，口算题降一档</summary>
        public static int DifficultyOf(Question q)
        {
            if (q == null) return 1;
            int d = q.Grade <= 2 ? 1 : (q.Grade <= 4 ? 2 : 3);
            if (q.Type == QType.MathCalc && d > 1) d--;
            return d;
        }

        /// <summary>年级上限硬约束：任何兜底路径都不允许抽出高于选中年级范围的题</summary>
        public static bool IsGradeCapped(IList<Question> questions, int[] allowedGrades)
        {
            if (questions == null) return false;
            if (allowedGrades == null || allowedGrades.Length == 0) return true;
            foreach (Question q in questions)
            {
                if (q == null) return false;
                bool found = false;
                foreach (int g in allowedGrades) if (q.Grade == g) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        /// <summary>
        /// 批次质量门禁：返回 null 表示通过，否则返回首条错误描述。
        /// 检查题量、题干、选项合法性、正确答案下标、占位符、题干去重。
        /// </summary>
        public static string ValidateBatch(IList<Question> questions, int expectedCount)
        {
            if (questions == null || questions.Count != expectedCount)
                return "题量应为 " + expectedCount + "，实际 " +
                    (questions == null ? 0 : questions.Count);
            HashSet<string> prompts = new HashSet<string>(StringComparer.Ordinal);
            foreach (Question q in questions)
            {
                if (q == null) return "存在空题";
                if (string.IsNullOrEmpty(q.Prompt)) return "题干为空";
                if (!prompts.Add(q.Prompt)) return "题干重复：" + q.Prompt;
                if (q.Type == QType.MathCalc)
                {
                    if (q.Options != null) return "口算题不应带选项";
                }
                else
                {
                    if (q.Options == null || q.Options.Length < 2 || q.Options.Length > 4)
                        return "选项数量非法：" + q.Prompt;
                    if (q.CorrectIndex < 0 || q.CorrectIndex >= q.Options.Length)
                        return "正确答案下标非法：" + q.Prompt;
                    HashSet<string> opts = new HashSet<string>(StringComparer.Ordinal);
                    foreach (string o in q.Options)
                    {
                        if (string.IsNullOrEmpty(o) || o.IndexOf('?') >= 0)
                            return "选项含空值或占位符：" + q.Prompt;
                        if (!opts.Add(o)) return "选项重复：" + q.Prompt;
                    }
                }
            }
            return null;
        }
    }
}
