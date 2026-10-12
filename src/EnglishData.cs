using System.Collections.Generic;

namespace PCLock
{
    /// <summary>英语题库（看单词选汉语 / 看汉语选单词），按年级分组。</summary>
    public static class EnglishData
    {
        public static readonly List<EnglishEntry> All = new List<EnglishEntry>
        {
            // ===== 一年级 =====
            new EnglishEntry { Grade = 1, En = "apple", Zh = "苹果" },
            new EnglishEntry { Grade = 1, En = "cat", Zh = "猫" },
            new EnglishEntry { Grade = 1, En = "dog", Zh = "狗" },
            new EnglishEntry { Grade = 1, En = "book", Zh = "书" },
            new EnglishEntry { Grade = 1, En = "red", Zh = "红色" },
            new EnglishEntry { Grade = 1, En = "sun", Zh = "太阳" },
            new EnglishEntry { Grade = 1, En = "water", Zh = "水" },
            new EnglishEntry { Grade = 1, En = "bird", Zh = "鸟" },
            new EnglishEntry { Grade = 1, En = "milk", Zh = "牛奶" },
            new EnglishEntry { Grade = 1, En = "pen", Zh = "钢笔" },
            new EnglishEntry { Grade = 1, En = "eye", Zh = "眼睛" },
            new EnglishEntry { Grade = 1, En = "hand", Zh = "手" },

            // ===== 二年级 =====
            new EnglishEntry { Grade = 2, En = "teacher", Zh = "老师" },
            new EnglishEntry { Grade = 2, En = "school", Zh = "学校" },
            new EnglishEntry { Grade = 2, En = "happy", Zh = "快乐的" },
            new EnglishEntry { Grade = 2, En = "green", Zh = "绿色" },
            new EnglishEntry { Grade = 2, En = "fish", Zh = "鱼" },
            new EnglishEntry { Grade = 2, En = "tree", Zh = "树" },
            new EnglishEntry { Grade = 2, En = "run", Zh = "跑" },
            new EnglishEntry { Grade = 2, En = "eat", Zh = "吃" },
            new EnglishEntry { Grade = 2, En = "blue", Zh = "蓝色" },
            new EnglishEntry { Grade = 2, En = "chair", Zh = "椅子" },
            new EnglishEntry { Grade = 2, En = "door", Zh = "门" },
            new EnglishEntry { Grade = 2, En = "moon", Zh = "月亮" },

            // ===== 三年级 =====
            new EnglishEntry { Grade = 3, En = "library", Zh = "图书馆" },
            new EnglishEntry { Grade = 3, En = "breakfast", Zh = "早餐" },
            new EnglishEntry { Grade = 3, En = "computer", Zh = "电脑" },
            new EnglishEntry { Grade = 3, En = "weather", Zh = "天气" },
            new EnglishEntry { Grade = 3, En = "animal", Zh = "动物" },
            new EnglishEntry { Grade = 3, En = "yellow", Zh = "黄色" },
            new EnglishEntry { Grade = 3, En = "write", Zh = "写" },
            new EnglishEntry { Grade = 3, En = "sister", Zh = "姐妹" },
            new EnglishEntry { Grade = 3, En = "father", Zh = "父亲" },
            new EnglishEntry { Grade = 3, En = "flower", Zh = "花" },
            new EnglishEntry { Grade = 3, En = "swim", Zh = "游泳" },
            new EnglishEntry { Grade = 3, En = "music", Zh = "音乐" },

            // ===== 四年级 =====
            new EnglishEntry { Grade = 4, En = "beautiful", Zh = "美丽的" },
            new EnglishEntry { Grade = 4, En = "dictionary", Zh = "字典" },
            new EnglishEntry { Grade = 4, En = "vegetable", Zh = "蔬菜" },
            new EnglishEntry { Grade = 4, En = "dangerous", Zh = "危险的" },
            new EnglishEntry { Grade = 4, En = "mountain", Zh = "山" },
            new EnglishEntry { Grade = 4, En = "remember", Zh = "记得" },
            new EnglishEntry { Grade = 4, En = "language", Zh = "语言" },
            new EnglishEntry { Grade = 4, En = "science", Zh = "科学" },
            new EnglishEntry { Grade = 4, En = "comfortable", Zh = "舒适的" },
            new EnglishEntry { Grade = 4, En = "window", Zh = "窗户" },
            new EnglishEntry { Grade = 4, En = "difficult", Zh = "困难的" },
            new EnglishEntry { Grade = 4, En = "celebrate", Zh = "庆祝" },

            // ===== 五年级 =====
            new EnglishEntry { Grade = 5, En = "environment", Zh = "环境" },
            new EnglishEntry { Grade = 5, En = "experience", Zh = "经历" },
            new EnglishEntry { Grade = 5, En = "knowledge", Zh = "知识" },
            new EnglishEntry { Grade = 5, En = "delicious", Zh = "美味的" },
            new EnglishEntry { Grade = 5, En = "mysterious", Zh = "神秘的" },
            new EnglishEntry { Grade = 5, En = "responsible", Zh = "负责的" },
            new EnglishEntry { Grade = 5, En = "forest", Zh = "森林" },
            new EnglishEntry { Grade = 5, En = "bridge", Zh = "桥" },
            new EnglishEntry { Grade = 5, En = "island", Zh = "岛屿" },
            new EnglishEntry { Grade = 5, En = "volcano", Zh = "火山" },
            new EnglishEntry { Grade = 5, En = "convenient", Zh = "方便的" },
            new EnglishEntry { Grade = 5, En = "fountain", Zh = "喷泉" },

            // ===== 六年级 =====
            new EnglishEntry { Grade = 6, En = "ambitious", Zh = "有雄心的" },
            new EnglishEntry { Grade = 6, En = "atmosphere", Zh = "大气" },
            new EnglishEntry { Grade = 6, En = "demonstrate", Zh = "演示" },
            new EnglishEntry { Grade = 6, En = "extraordinary", Zh = "非凡的" },
            new EnglishEntry { Grade = 6, En = "fundamental", Zh = "基本的" },
            new EnglishEntry { Grade = 6, En = "guarantee", Zh = "保证" },
            new EnglishEntry { Grade = 6, En = "glacier", Zh = "冰川" },
            new EnglishEntry { Grade = 6, En = "lighthouse", Zh = "灯塔" },
            new EnglishEntry { Grade = 6, En = "phenomenon", Zh = "现象" },
            new EnglishEntry { Grade = 6, En = "sophisticated", Zh = "复杂的" },
            new EnglishEntry { Grade = 6, En = "tremendous", Zh = "巨大的" },
            new EnglishEntry { Grade = 6, En = "vocabulary", Zh = "词汇" },
        };
    }
}
