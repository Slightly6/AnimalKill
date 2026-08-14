using UnityEngine;
using UnityEditor;

/// <summary>
/// 一键生成 52 张动物卡
/// 用法：菜单栏 Tools → 生成52张动物卡
/// </summary>
public class CardGenerator
{
    [MenuItem("Tools/生成52张动物卡")]
    public static void Run()
    {
        string path = "Assets/Data/Cards";

        // 没有文件夹就建一个
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder("Assets/Data", "Cards");
        }

        // 清空旧的
        string[] old = AssetDatabase.FindAssets("t:CardDataSO", new[] { path });
        if (old.Length > 0)
        {
            if (!EditorUtility.DisplayDialog("提示", "文件夹里已有 " + old.Length + " 张卡，覆盖？", "覆盖", "取消"))
                return;

            for (int i = 0; i < old.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(old[i]);
                AssetDatabase.DeleteAsset(p);
            }
        }

        // 4 种花色
        CardSuit[] suits = { CardSuit.Spade, CardSuit.Heart, CardSuit.Diamond, CardSuit.Club };

        // 13 个点数（从 2 到 A，2 最弱 A 最强 = 14 点）
        CardRank[] ranks = {
            CardRank.Two, CardRank.Three, CardRank.Four, CardRank.Five, CardRank.Six,
            CardRank.Seven, CardRank.Eight, CardRank.Nine, CardRank.Ten,
            CardRank.Jack, CardRank.Queen, CardRank.King, CardRank.Ace
        };

        // 每种花色的 13 只动物
        string[] spadeAnimals = { "蜘蛛","鼠","猫头鹰","貂","狐獴","鳄鱼","鲨鱼","鹰","蛇","豹","狼","虎","龙" };
        string[] heartAnimals = { "蜜蜂","鸡","绵羊","企鹅","海獭","羚羊","水牛","海豚","兔","鹿","马","象","凤凰" };
        string[] diamondAnimals = { "蜗牛","蝾螈","萤火虫","刺猬","箭毒蛙","电鳗","蝎子","独角兽","冰狼","雷鸟","梦貘","九尾狐","麒麟" };
        string[] clubAnimals = { "甲虫","蟾蜍","穿山甲","豪猪","螃蟹","水獭","犰狳","龟","猩猩","野牛","河马","犀牛","玄武" };

        // 技能名
        string[] spadeAbilities = { "织网","瘟疫","夜视","潜行","毒牙","死亡翻滚","血腥追踪","俯冲","绞杀","伏击","协同狩猎","统率","龙息" };
        string[] heartAbilities = { "授粉","晨鸣","温顺","团结","筑坝","疾驰","冲锋","声呐","繁殖","优雅","领袖","践踏","涅槃" };
        string[] diamondAbilities = { "黏液","再生","荧光","蜷缩","剧毒","电击","尾针","净化","冰冻","雷霆","入梦","魅惑","祥瑞" };
        string[] clubAbilities = { "硬壳","毒腺","鳞甲","尖刺","蟹钳","潜水","铁壁","龟缩","蛮力","顶撞","巨颚","角突","镇守" };

        int count = 0;

        for (int s = 0; s < 4; s++)
        {
            CardSuit suit = suits[s];

            // 选当前花色的动物数组和技能数组
            string[] animals;
            string[] abilities;
            if (s == 0) { animals = spadeAnimals; abilities = spadeAbilities; }
            else if (s == 1) { animals = heartAnimals; abilities = heartAbilities; }
            else if (s == 2) { animals = diamondAnimals; abilities = diamondAbilities; }
            else { animals = clubAnimals; abilities = clubAbilities; }

            for (int r = 0; r < 13; r++)
            {
                CardRank rank = ranks[r];
                string animal = animals[r];
                string ability = abilities[r];

                CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();

                card.suit = suit;
                card.rank = rank;
                card.animalName = animal;
                card.description = "战力 " + card.GetPower();
                card.abilityName = ability;
                card.abilityDesc = GetDesc(suit, ability);
                card.isAwakened = false;
                card.artwork = null;

                string fileName = suit + "_" + card.GetRankText() + "_" + animal + ".asset";
                AssetDatabase.CreateAsset(card, path + "/" + fileName);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("生成完成！共 " + count + " 张卡");
        EditorUtility.DisplayDialog("完成", "已生成 " + count + " 张动物卡到\n" + path, "好");
    }

    // 根据花色返回技能描述
    private static string GetDesc(CardSuit suit, string ability)
    {
        if (suit == CardSuit.Spade)
            return "【" + ability + "】击杀敌方后 +3 筹码。觉醒攻+3。";
        if (suit == CardSuit.Heart)
            return "【" + ability + "】相邻友方 +2 攻。觉醒血+3。";
        if (suit == CardSuit.Diamond)
            return "【" + ability + "】可改一张公共牌花色。觉醒可改两张。";
        if (suit == CardSuit.Club)
            return "【" + ability + "】登场获得 3 点护盾。觉醒每回合刷新。";
        return "";
    }
}
