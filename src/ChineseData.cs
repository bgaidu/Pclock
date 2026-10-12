using System.Collections.Generic;

namespace PCLock
{
    /// <summary>语文题库（看词语选拼音 + 词语解释），按年级分组。</summary>
    public static class ChineseData
    {
        public static readonly List<WordEntry> All = new List<WordEntry>
        {
            // ===== 一年级 =====
            new WordEntry { Grade = 1, Word = "太阳", Pinyin = "tài yáng", Meaning = "恒星，白天发光发热的天体。" },
            new WordEntry { Grade = 1, Word = "月亮", Pinyin = "yuè liang", Meaning = "地球的卫星，夜晚天空中明亮的球体。" },
            new WordEntry { Grade = 1, Word = "星星", Pinyin = "xīng xing", Meaning = "夜空中闪烁的小光点，是遥远的恒星。" },
            new WordEntry { Grade = 1, Word = "朋友", Pinyin = "péng you", Meaning = "互相友好、常在一起玩的人。" },
            new WordEntry { Grade = 1, Word = "书包", Pinyin = "shū bāo", Meaning = "用来装书本和文具的包。" },
            new WordEntry { Grade = 1, Word = "高兴", Pinyin = "gāo xìng", Meaning = "心情愉快、开心。" },
            new WordEntry { Grade = 1, Word = "苹果", Pinyin = "píng guǒ", Meaning = "一种常见的圆形水果，红色或绿色。" },
            new WordEntry { Grade = 1, Word = "火车", Pinyin = "huǒ chē", Meaning = "在铁轨上行驶的长形交通工具。" },
            new WordEntry { Grade = 1, Word = "下雨", Pinyin = "xià yǔ", Meaning = "云中的水滴落下成为雨。" },
            new WordEntry { Grade = 1, Word = "早上", Pinyin = "zǎo shang", Meaning = "从天亮到八九点钟的一段时间。" },
            new WordEntry { Grade = 1, Word = "红花", Pinyin = "hóng huā", Meaning = "红色的花朵。" },
            new WordEntry { Grade = 1, Word = "老师", Pinyin = "lǎo shī", Meaning = "在学校里教学生知识的人。" },

            // ===== 二年级 =====
            new WordEntry { Grade = 2, Word = "蝴蝶", Pinyin = "hú dié", Meaning = "一种翅膀色彩鲜艳、会飞的昆虫。" },
            new WordEntry { Grade = 2, Word = "青蛙", Pinyin = "qīng wā", Meaning = "生活在水边、会游泳会跳的绿色小动物。" },
            new WordEntry { Grade = 2, Word = "草原", Pinyin = "cǎo yuán", Meaning = "生长着大片牧草的辽阔土地。" },
            new WordEntry { Grade = 2, Word = "冬天", Pinyin = "dōng tiān", Meaning = "一年中较冷的季节，常下雪。" },
            new WordEntry { Grade = 2, Word = "跑步", Pinyin = "pǎo bù", Meaning = "快速地两脚交替向前跑。" },
            new WordEntry { Grade = 2, Word = "美丽", Pinyin = "měi lì", Meaning = "好看、漂亮。" },
            new WordEntry { Grade = 2, Word = "认真", Pinyin = "rèn zhēn", Meaning = "做事专心、不马虎。" },
            new WordEntry { Grade = 2, Word = "干净", Pinyin = "gān jìng", Meaning = "没有尘土和污垢，整洁。" },
            new WordEntry { Grade = 2, Word = "水果", Pinyin = "shuǐ guǒ", Meaning = "可以生吃、含水分多的植物果实。" },
            new WordEntry { Grade = 2, Word = "学校", Pinyin = "xué xiào", Meaning = "学生读书、学习的地方。" },
            new WordEntry { Grade = 2, Word = "黑板", Pinyin = "hēi bǎn", Meaning = "教室里用粉笔写字的深色平板。" },
            new WordEntry { Grade = 2, Word = "风筝", Pinyin = "fēng zheng", Meaning = "用线牵着在风中飞翔的玩具。" },

            // ===== 三年级 =====
            new WordEntry { Grade = 3, Word = "清晨", Pinyin = "qīng chén", Meaning = "日出前后的清早时分。" },
            new WordEntry { Grade = 3, Word = "温暖", Pinyin = "wēn nuǎn", Meaning = "暖和，使人感到舒服。" },
            new WordEntry { Grade = 3, Word = "勇敢", Pinyin = "yǒng gǎn", Meaning = "不怕危险和困难，有胆量。" },
            new WordEntry { Grade = 3, Word = "诚实", Pinyin = "chéng shí", Meaning = "说话做事不虚假，守信用。" },
            new WordEntry { Grade = 3, Word = "忽然", Pinyin = "hū rán", Meaning = "发生得迅速又出乎意料。" },
            new WordEntry { Grade = 3, Word = "热闹", Pinyin = "rè nao", Meaning = "人多欢腾、气氛活跃。" },
            new WordEntry { Grade = 3, Word = "奇怪", Pinyin = "qí guài", Meaning = "跟平常不一样，令人觉得奇异。" },
            new WordEntry { Grade = 3, Word = "希望", Pinyin = "xī wàng", Meaning = "心里想着达到某种目的。" },
            new WordEntry { Grade = 3, Word = "观察", Pinyin = "guān chá", Meaning = "仔细察看事物或现象。" },
            new WordEntry { Grade = 3, Word = "鼓励", Pinyin = "gǔ lì", Meaning = "用言语激发、勉励别人。" },
            new WordEntry { Grade = 3, Word = "疲倦", Pinyin = "pí juàn", Meaning = "疲乏、困倦，没有力气。" },
            new WordEntry { Grade = 3, Word = "犹豫", Pinyin = "yóu yù", Meaning = "拿不定主意。" },

            // ===== 四年级 =====
            new WordEntry { Grade = 4, Word = "攀登", Pinyin = "pān dēng", Meaning = "抓住东西爬上去，比喻努力向上。" },
            new WordEntry { Grade = 4, Word = "储蓄", Pinyin = "chǔ xù", Meaning = "把钱存起来不花掉。" },
            new WordEntry { Grade = 4, Word = "惬意", Pinyin = "qiè yì", Meaning = "满意、舒服自在。" },
            new WordEntry { Grade = 4, Word = "尴尬", Pinyin = "gān gà", Meaning = "处境难堪、不自然。" },
            new WordEntry { Grade = 4, Word = "斟酌", Pinyin = "zhēn zhuó", Meaning = "仔细考虑、推敲字句。" },
            new WordEntry { Grade = 4, Word = "蜿蜒", Pinyin = "wān yán", Meaning = "弯弯曲曲地延伸。" },
            new WordEntry { Grade = 4, Word = "寂静", Pinyin = "jì jìng", Meaning = "没有声音，十分安静。" },
            new WordEntry { Grade = 4, Word = "憧憬", Pinyin = "chōng jǐng", Meaning = "向往美好的事物。" },
            new WordEntry { Grade = 4, Word = "吝啬", Pinyin = "lìn sè", Meaning = "过分爱惜财物，不肯用。" },
            new WordEntry { Grade = 4, Word = "蹒跚", Pinyin = "pán shān", Meaning = "走路缓慢摇摆的样子。" },
            new WordEntry { Grade = 4, Word = "翱翔", Pinyin = "áo xiáng", Meaning = "在空中回旋地飞。" },
            new WordEntry { Grade = 4, Word = "忐忑", Pinyin = "tǎn tè", Meaning = "心神不定、不安。" },

            // ===== 五年级 =====
            new WordEntry { Grade = 5, Word = "静谧", Pinyin = "jìng mì", Meaning = "安静，平静。" },
            new WordEntry { Grade = 5, Word = "璀璨", Pinyin = "cuǐ càn", Meaning = "光彩夺目。" },
            new WordEntry { Grade = 5, Word = "逶迤", Pinyin = "wēi yí", Meaning = "道路或山脉弯弯曲曲延续不绝。" },
            new WordEntry { Grade = 5, Word = "踌躇", Pinyin = "chóu chú", Meaning = "犹豫不决。" },
            new WordEntry { Grade = 5, Word = "澎湃", Pinyin = "péng pài", Meaning = "波浪互相撞击，也比喻声势浩大。" },
            new WordEntry { Grade = 5, Word = "踉跄", Pinyin = "liàng qiàng", Meaning = "走路不稳，跌跌撞撞。" },
            new WordEntry { Grade = 5, Word = "拮据", Pinyin = "jié jū", Meaning = "经济困难，手头不宽裕。" },
            new WordEntry { Grade = 5, Word = "氤氲", Pinyin = "yīn yūn", Meaning = "形容烟或云气浓郁。" },
            new WordEntry { Grade = 5, Word = "婆娑", Pinyin = "pó suō", Meaning = "盘旋舞动的样子。" },
            new WordEntry { Grade = 5, Word = "徜徉", Pinyin = "cháng yáng", Meaning = "闲游、安闲自在地步行。" },
            new WordEntry { Grade = 5, Word = "跌宕", Pinyin = "diē dàng", Meaning = "性格洒脱或文章富于变化。" },
            new WordEntry { Grade = 5, Word = "馥郁", Pinyin = "fù yù", Meaning = "香气浓厚。" },

            // ===== 六年级 =====
            new WordEntry { Grade = 6, Word = "稽首", Pinyin = "qǐ shǒu", Meaning = "古时的一种跪拜礼。" },
            new WordEntry { Grade = 6, Word = "龃龉", Pinyin = "jǔ yǔ", Meaning = "上下牙齿对不上，比喻意见不合。" },
            new WordEntry { Grade = 6, Word = "囹圄", Pinyin = "líng yǔ", Meaning = "监狱。" },
            new WordEntry { Grade = 6, Word = "觊觎", Pinyin = "jì yú", Meaning = "希望得到不该得到的东西。" },
            new WordEntry { Grade = 6, Word = "腌臜", Pinyin = "ā zā", Meaning = "脏、不干净。" },
            new WordEntry { Grade = 6, Word = "龌龊", Pinyin = "wò chuò", Meaning = "不干净，也指人品卑劣。" },
            new WordEntry { Grade = 6, Word = "踟蹰", Pinyin = "chí chú", Meaning = "心里迟疑，要走不走。" },
            new WordEntry { Grade = 6, Word = "氍毹", Pinyin = "qú shū", Meaning = "借指舞台。" },
            new WordEntry { Grade = 6, Word = "饕餮", Pinyin = "tāo tiè", Meaning = "传说中贪吃的猛兽，比喻贪吃的人。" },
            new WordEntry { Grade = 6, Word = "魑魅", Pinyin = "chī mèi", Meaning = "传说中山林里的妖怪。" },
            new WordEntry { Grade = 6, Word = "睥睨", Pinyin = "pì nì", Meaning = "斜着眼睛看，形容傲视。" },
            new WordEntry { Grade = 6, Word = "纨绔", Pinyin = "wán kù", Meaning = "富贵人家不务正业的子弟。" },
        };
    }
}
