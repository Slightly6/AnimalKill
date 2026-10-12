using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 纯牌型/分数/伤害计算工具类。不碰 Unity 对象，方便单独测试和复用。
/// 包装 PokerHandEvaluator，集中加成/伤害/文本格式化逻辑。
/// </summary>
public static class PokerResolver
{
    // 按牌型的永久伤害加成（掷骰子时累加：掷到几，对应牌型伤害+1）
    private static readonly Dictionary<HandType, int> chipAdd = new Dictionary<HandType, int>();

    /// <summary>某个牌型永久伤害加成（掷骰子时累加）</summary>
    public static void AddBonus(HandType type, int chip)
    {
        if (!chipAdd.ContainsKey(type)) chipAdd[type] = 0;
        chipAdd[type] += chip;
    }

    /// <summary>所有牌型永久伤害加成（掷到10点时调）</summary>
    public static void AddBonusToAll(int chip)
    {
        foreach (HandType ht in System.Enum.GetValues(typeof(HandType)))
            AddBonus(ht, chip);
    }

    // 1-9 → 对应牌型
    private static readonly HandType[] diceToHandType = new HandType[]
    {
        HandType.HighCard,      // 1
        HandType.OnePair,       // 2
        HandType.TwoPair,       // 3
        HandType.ThreeOfAKind,  // 4
        HandType.Straight,      // 5
        HandType.Flush,         // 6
        HandType.FullHouse,     // 7
        HandType.FourOfAKind,   // 8
        HandType.StraightFlush, // 9
    };

    public static HandType DiceToHandType(int faceValue)
    {
        if (faceValue >= 1 && faceValue <= 9)
            return diceToHandType[faceValue - 1];
        return HandType.HighCard;
    }

    // ===== 新体系：牌型直接对应固定伤害（高牌1 ~ 同花顺9），不要底分/倍率 =====
    static readonly Dictionary<HandType, int> handDamageTable = new Dictionary<HandType, int>
    {
        { HandType.HighCard, 1 },
        { HandType.OnePair, 2 },
        { HandType.TwoPair, 3 },
        { HandType.ThreeOfAKind, 4 },
        { HandType.Straight, 5 },
        { HandType.Flush, 6 },
        { HandType.FullHouse, 7 },
        { HandType.FourOfAKind, 8 },
        { HandType.StraightFlush, 9 },
    };

    // 牌型 → 最终伤害 = 固定值 + 掷骰永久加成（掷到几，对应牌型伤害+1；掷到10全加）
    public static int GetHandDamage(HandType type)
    {
        int dmg = handDamageTable.TryGetValue(type, out int v) ? v : 1;
        if (chipAdd.TryGetValue(type, out int bonus)) dmg += bonus;
        return dmg;
    }

    // 转发 PokerHandEvaluator
    public static HandType Evaluate(List<CardDataSO> cards)
        => PokerHandEvaluator.Evaluate(cards);



    public static HashSet<int> GetCoreCardIndices(List<CardDataSO> cards, HandType type)
        => PokerHandEvaluator.GetCoreCardIndices(cards, type);

    // 牌型→中文名（计分板用）
    public static string HandName(HandType t)
    {
        switch (t)
        {
            case HandType.StraightFlush: return "同花顺";
            case HandType.FourOfAKind: return "四条";
            case HandType.FullHouse: return "葫芦";
            case HandType.Flush: return "同花";
            case HandType.Straight: return "顺子";
            case HandType.ThreeOfAKind: return "三条";
            case HandType.TwoPair: return "两对";
            case HandType.OnePair: return "一对";
            default: return "高牌";
        }
    }

    static readonly Dictionary<HandType, int> pointCostTable = new Dictionary<HandType, int>
    {
        { HandType.HighCard, 1 }, 
        { HandType.OnePair, 1 },
        { HandType.TwoPair, 2 },
        { HandType.ThreeOfAKind, 2 },
        { HandType.Straight, 2 },
        { HandType.Flush, 2 },
        { HandType.FullHouse, 3 },
        { HandType.FourOfAKind, 3 },
        { HandType.StraightFlush, 3 },
    };

    public static int GetPointCost(HandType type)
    {
        return pointCostTable.TryGetValue(type, out int cost) ? cost : 1;
    }
}
