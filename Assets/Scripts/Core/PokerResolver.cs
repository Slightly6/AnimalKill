using System.Collections.Generic;

/// <summary>
/// 纯牌型/分数/伤害计算工具类。不碰 Unity 对象，方便单独测试和复用。
/// 包装 PokerHandEvaluator，集中加成/伤害/文本格式化逻辑。
/// </summary>
public static class PokerResolver
{
    // 转发 PokerHandEvaluator（牌型升级时在这里覆盖底分/倍率即可，调用方不用改）
    public static HandType Evaluate(List<CardDataSO> cards)
        => PokerHandEvaluator.Evaluate(cards);

    public static int GetBaseChips(HandType type)
        => PokerHandEvaluator.GetBaseChips(type);

    public static int GetMultiplier(HandType type)
        => PokerHandEvaluator.GetMultiplier(type);

    public static HashSet<int> GetCoreCardIndices(List<CardDataSO> cards, HandType type)
        => PokerHandEvaluator.GetCoreCardIndices(cards, type);

    // 点数→显示文本（A 当 14，其余按点数；返回 "+14" 形式，飘字用）
    public static string RankDisplay(CardRank r)
    {
        int v = (int)r;
        if (v == 1) v = 14;
        return "+" + v.ToString();
    }

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

    // 主牌加成总和：遍历 coreIndices 中的牌，累加点数（A=14）
    // 等价于原代码 RankDisplay+TrimStart+Parse，用数值更干净
    public static int CalcCardBonus(List<Card> cards, HashSet<int> coreIndices)
    {
        int bonus = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (coreIndices.Contains(i))
            {
                int v = (int)cards[i].Data.rank;
                if (v == 1) v = 14;   // A 当 14，和 RankDisplay 一致
                bonus += v;
            }
        }
        return bonus;
    }

        // 主牌加成总和（敌人回合用：只有 CardDataSO 数据，没有 Card 对象）
    public static int CalcCardBonus(List<CardDataSO> cards, HashSet<int> coreIndices)
    {
        int bonus = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (coreIndices.Contains(i))
            {
                int v = (int)cards[i].rank;
                if (v == 1) v = 14;   // A 当 14
                bonus += v;
            }
        }
        return bonus;
    }

    // 最终伤害 = (底分 + 主牌加成) × 倍率
    public static int CalcDamage(int baseChips, int cardBonus, int mult)
        => (baseChips + cardBonus) * mult;
}
