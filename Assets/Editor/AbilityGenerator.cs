using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键生成 52 张动物卡的觉醒技能资产，并自动挂到对应 CardDataSO.awakenedAbility。
/// 用法：菜单栏 Tools → 一键生成卡牌+觉醒技能（或只生成技能：Tools → 生成52个觉醒技能）
///
/// 后期改技能不用写代码：
///   1. 打开 Assets/Data/Abilities/Effects/ 下对应效果资产，换 Kind、改 amount/amount2
///   2. 或打开 Assets/Data/Abilities/ 下技能资产，改 trigger（触发时机）/描述/图标
/// 重新跑本工具会按本表覆盖回默认值。
/// </summary>
public class AbilityGenerator
{
    const string CARD_PATH = "Assets/Data/Cards";
    const string ABILITY_PATH = "Assets/Data/Abilities";
    const string EFFECT_PATH = "Assets/Data/Abilities/Effects";

    // 一行 = 一个动物的觉醒技能配置
    struct Row
    {
        public AbilityKind kind;
        public int amount;
        public int amount2;
        public AbilityTrigger trigger;
        public string desc;

        public Row(AbilityKind kind, AbilityTrigger trigger, string desc, int amount = 0, int amount2 = 0)
        {
            this.kind = kind;
            this.trigger = trigger;
            this.desc = desc;
            this.amount = amount;
            this.amount2 = amount2;
        }
    }

    // 白板：不生成觉醒技能（觉醒槽留空，永远只能当白板用）
    static readonly Row Blank = new Row(AbilityKind.None, AbilityTrigger.OnPlay, "");

    // ♠ 猎杀系：A 松鼠白板；2~7 六个觉醒技能；8~K 留空白板
    static readonly Row[] spade =
    {
        Blank,
        new Row(AbilityKind.GrowAnyDeath,   AbilityTrigger.OnPlay,      "食腐：场上每有单位死亡（不分敌我），自身+1力量。", 1),
        new Row(AbilityKind.SkillImmune,    AbilityTrigger.OnPlay,      "鸮佑：不受敌方技能影响。"),
        new Row(AbilityKind.EvadeChance,    AbilityTrigger.OnPlay,      "闪避：被攻击时30%概率扑空，免受此次伤害。", 30),
        new Row(AbilityKind.Colony,         AbilityTrigger.OnPlay,      "团居：扑击伤害=自身力量×同阵营在场数量。"),
        new Row(AbilityKind.DeathRoll,      AbilityTrigger.OnPlay,      "死亡翻滚：每轮随机翻滚1~5次，每次造成等同于当前力量的伤害；目标死亡后剩余次数作废。"),
        new Row(AbilityKind.DeepHunter,     AbilityTrigger.OnPlay,      "深海猎手：敌人若非自己所伤而死亡，获得1颗牙；集满20颗进入潜水——首次被攻击无敌，伤害×牙齿数，每回合-2颗，归零退出。"),
        Blank, Blank, Blank, Blank, Blank, Blank,
    };

    // ♥ ♦ ♣ 全部白板（开发黑桃2-7期间暂停）
    static readonly Row[] heart =
    { Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank };

    static readonly Row[] diamond =
    { Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank };

    static readonly Row[] club =
    { Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank, Blank };

    [MenuItem("Tools/一键生成卡牌+觉醒技能")]
    public static void GenerateAll()
    {
        CardGenerator.RunSilent();
        Generate();
    }

    [MenuItem("Tools/生成52个觉醒技能")]
    public static void Generate()
    {
        EnsureFolder("Assets/Data", "Abilities");
        EnsureFolder(ABILITY_PATH, "Effects");

        // 载入全部卡牌，按 (花色,点数) 建索引
        string[] guids = AssetDatabase.FindAssets("t:CardDataSO", new[] { CARD_PATH });
        var cardMap = new Dictionary<CardSuit, List<CardDataSO>>();
        foreach (CardSuit s in new[] { CardSuit.Spade, CardSuit.Heart, CardSuit.Diamond, CardSuit.Club })
            cardMap[s] = new List<CardDataSO>();

        foreach (string g in guids)
        {
            var card = AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(g));
            if (card != null) cardMap[card.suit].Add(card);
        }
        foreach (var kv in cardMap)
            kv.Value.Sort((a, b) => a.rank.CompareTo(b.rank));

        int total = 0;
        total += BindSuit(CardSuit.Spade, cardMap[CardSuit.Spade], spade);
        total += BindSuit(CardSuit.Heart, cardMap[CardSuit.Heart], heart);
        total += BindSuit(CardSuit.Diamond, cardMap[CardSuit.Diamond], diamond);
        total += BindSuit(CardSuit.Club, cardMap[CardSuit.Club], club);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[觉醒] 技能生成完成，共 " + total + " 个");
        EditorUtility.DisplayDialog("完成",
            "已生成 " + total + " 个觉醒技能并挂到卡牌。\n技能资产：Assets/Data/Abilities/\n后期直接在 Inspector 改 Kind/数值即可。",
            "好");
    }

    static int BindSuit(CardSuit suit, List<CardDataSO> cards, Row[] rows)
    {
        if (cards.Count == 0)
        {
            Debug.LogWarning("[觉醒] 没找到 " + suit + " 花色的卡牌，请先跑「生成52张动物卡」");
            return 0;
        }

        string suitName = suit.ToString();
        int n = Mathf.Min(cards.Count, rows.Length);
        for (int i = 0; i < n; i++)
        {
            CardDataSO card = cards[i];
            Row row = rows[i];

            // 白板卡：觉醒槽置空，并清掉旧版本可能遗留的技能资产（防止孤儿资产误导）
            if (row.kind == AbilityKind.None)
            {
                string oldFx = EFFECT_PATH + "/FX_" + suitName + "_" + card.GetRankText() + ".asset";
                string oldAb = ABILITY_PATH + "/AB_" + suitName + "_" + card.GetRankText() + ".asset";
                if (AssetDatabase.LoadAssetAtPath<AbilityEffectSO>(oldFx) != null) AssetDatabase.DeleteAsset(oldFx);
                if (AssetDatabase.LoadAssetAtPath<AbilitySO>(oldAb) != null) AssetDatabase.DeleteAsset(oldAb);
                card.awakenedAbility = null;
                card.abilityName = "";
                EditorUtility.SetDirty(card);
                continue;
            }

            // 1) 效果资产（Kind + 数值），存在则覆盖字段
            string fxPath = EFFECT_PATH + "/FX_" + suitName + "_" + card.GetRankText() + ".asset";
            var fx = AssetDatabase.LoadAssetAtPath<AbilityEffectSO>(fxPath);
            if (fx == null)
            {
                fx = ScriptableObject.CreateInstance<AbilityEffectSO>();
                AssetDatabase.CreateAsset(fx, fxPath);
            }
            fx.kind = row.kind;
            fx.amount = row.amount;
            fx.amount2 = row.amount2;
            EditorUtility.SetDirty(fx);

            // 2) 技能资产（名字/描述/触发时机/图标 + 效果引用）
            string abPath = ABILITY_PATH + "/AB_" + suitName + "_" + card.GetRankText() + ".asset";
            var ab = AssetDatabase.LoadAssetAtPath<AbilitySO>(abPath);
            if (ab == null)
            {
                ab = ScriptableObject.CreateInstance<AbilitySO>();
                AssetDatabase.CreateAsset(ab, abPath);
            }
            ab.abilityName = card.abilityName;
            ab.description = row.desc;
            ab.trigger = row.trigger;
            ab.effect = fx;
            // 图标沿用卡牌上已配的技能图标（没有就留空，后期自己拖）
            if (ab.icon == null) ab.icon = card.abilityIcon;
            EditorUtility.SetDirty(ab);

            // 3) 挂到卡牌的觉醒槽
            card.awakenedAbility = ab;
            EditorUtility.SetDirty(card);
        }
        return n;
    }

    static void EnsureFolder(string parent, string folderName)
    {
        string path = parent + "/" + folderName;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folderName);
    }
}
