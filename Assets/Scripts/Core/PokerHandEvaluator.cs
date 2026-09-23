using System.Collections.Generic;

// ========== 牌型（从高到低） ==========
public enum HandType
{
    HighCard,      // 高牌
    OnePair,       // 一对
    TwoPair,       // 两对
    ThreeOfAKind,  // 三条
    Straight,      // 顺子
    Flush,         // 同花
    FullHouse,     // 葫芦（三条+一对）
    FourOfAKind,   // 四条
    StraightFlush  // 同花顺
}

/// <summary>
/// 德州牌型判定：5 张牌 → 牌型，牌型 → 筹码。
/// 纯函数，不碰 Unity 对象，跟 CardAnimator 一样是静态工具类。
/// 点数 1~13，A = 1（最小），K = 13（最大）。最小顺子是 A-2-3-4-5，最大是 10-J-Q-K-A（A 当 14）。
/// </summary>
public static class PokerHandEvaluator
{
    // 1~5 张牌 → 牌型（玩家从手牌里选牌挂钩子，张数任意但不超过 5）
    // 对子/两对/三条/四条按实际张数判定；同花、顺子、葫芦、同花顺按扑克规则必须凑齐 5 张才算。
    public static HandType Evaluate(List<CardDataSO> cards)
    {
        bool five = cards.Count == 5;

        // ① 判断同花（全部花色相同；少于 5 张不算同花）
        bool isFlush = five;
        if (five)
        {
            CardSuit firstSuit = cards[0].suit;
            for (int i = 1; i < cards.Count; i++)
            {
                if (cards[i].suit != firstSuit) { isFlush = false; break; }
            }
        }

        // ② 取点数并排序（判断顺子用）
        int[] ranks = new int[cards.Count];
        for (int i = 0; i < cards.Count; i++) ranks[i] = (int)cards[i].rank;
        for (int i = 0; i < ranks.Length; i++)
        {
            for (int j = i + 1; j < ranks.Length; j++)
            {
                if (ranks[j] < ranks[i])
                {
                    int tmp = ranks[i]; ranks[i] = ranks[j]; ranks[j] = tmp;
                }
            }
        }

        // ③ 判断顺子（必须 5 张连续）。A 默认是 1（最小），但 10-J-Q-K-A 里 A 得当 14（最大）
        bool isStraight = false;
        if (five)
        {
            isStraight = IsConsecutive(ranks);
            if (!isStraight && ranks[0] == 1 && ranks[ranks.Length - 1] == 13)
            {
                // 把 A 从 1 挪到最后当 14，再判一次顺子（10-J-Q-K-A）
                int[] high = new int[ranks.Length];
                for (int i = 0; i < ranks.Length - 1; i++) high[i] = ranks[i + 1];
                high[ranks.Length - 1] = 14;
                isStraight = IsConsecutive(high);
            }
        }

        // ④ 统计每种点数的张数（index 1~13）
        int[] count = new int[14];
        for (int i = 0; i < ranks.Length; i++) count[ranks[i]]++;

        // 最大重复张数 + 对子数量
        int maxSame = 0;
        int pairCount = 0;
        for (int i = 1; i <= 13; i++)
        {
            if (count[i] > maxSame) maxSame = count[i];
            if (count[i] == 2) pairCount++;
        }

        // ⑤ 从高到低判定
        if (isFlush && isStraight) return HandType.StraightFlush;
        if (maxSame == 4) return HandType.FourOfAKind;
        if (maxSame == 3 && pairCount == 1) return HandType.FullHouse;
        if (isFlush) return HandType.Flush;
        if (isStraight) return HandType.Straight;
        if (maxSame == 3) return HandType.ThreeOfAKind;
        if (pairCount == 2) return HandType.TwoPair;
        if (pairCount == 1) return HandType.OnePair;
        return HandType.HighCard;
    }

    // 判断数组是否连续（2,3,4,5,6）
    static bool IsConsecutive(int[] arr)
    {
        for (int i = 0; i < arr.Length - 1; i++)
        {
            if (arr[i + 1] != arr[i] + 1) return false;
        }
        return true;
    }

        // 牌型 → 底分（Balatro 式：基础分，还没乘倍率）
    public static int GetBaseChips(HandType type)
    {
        if (type == HandType.StraightFlush) return 50;
        if (type == HandType.FourOfAKind)   return 35;
        if (type == HandType.FullHouse)      return 28;
        if (type == HandType.Flush)          return 24;
        if (type == HandType.Straight)       return 20;
        if (type == HandType.ThreeOfAKind)   return 16;
        if (type == HandType.TwoPair)        return 12;
        if (type == HandType.OnePair)        return 8;
        return 4; // 高牌
    }

    // 牌型 → 倍率
    public static int GetMultiplier(HandType type)
    {
        if (type == HandType.StraightFlush) return 5;
        if (type == HandType.FourOfAKind)   return 4;
        if (type == HandType.FullHouse)      return 3;
        if (type == HandType.Flush)          return 3;
        if (type == HandType.Straight)       return 3;
        if (type == HandType.ThreeOfAKind)   return 2;
        if (type == HandType.TwoPair)        return 2;
        if (type == HandType.OnePair)        return 1;
        return 1; // 高牌
    }

    // 最终伤害 = 底分 × 倍率（未加成时的纯牌型伤害）
    public static int GetDamage(HandType type, int extraChips, float extraMult)
    {
        int chips = GetBaseChips(type) + extraChips;
        float mult = GetMultiplier(type) + extraMult;
        return (int)(chips * mult);
    }
    //判断计分的牌
    public static HashSet<int> GetCoreCardIndices(List<CardDataSO> cards, HandType type)
    {
        HashSet<int> core = new HashSet<int>();
        int n = cards.Count;
        if (n == 0) return core;

        // 顺子/同花/葫芦/同花顺：全部是主牌
        if (type == HandType.Straight || type == HandType.Flush ||
            type == HandType.FullHouse || type == HandType.StraightFlush)
        {
            for (int i = 0; i < n; i++) core.Add(i);
            return core;
        }

        // 四条/三条/两对/一对：成对的才是主牌（点数出现 ≥2 次）
        if (type == HandType.FourOfAKind || type == HandType.ThreeOfAKind ||
            type == HandType.TwoPair || type == HandType.OnePair)
        {
            int[] count = new int[14];
            for (int i = 0; i < n; i++) count[(int)cards[i].rank]++;
            for (int i = 0; i < n; i++)
                if (count[(int)cards[i].rank] >= 2) core.Add(i);
            return core;
        }

        // 高牌：只有最大的那张算（A 当 14 最大）
        int bestIdx = 0;
        int bestVal = (int)cards[0].rank; if (bestVal == 1) bestVal = 14;
        for (int i = 1; i < n; i++)
        {
            int v = (int)cards[i].rank; if (v == 1) v = 14;
            if (v > bestVal) { bestVal = v; bestIdx = i; }
        }
        core.Add(bestIdx);
        return core;
    }
}
