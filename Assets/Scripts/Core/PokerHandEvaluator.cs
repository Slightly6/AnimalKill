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
/// 点数 1~13，A = 1（最小），K = 13（最大）。最小顺子是 A-2-3-4-5，最大是 9-10-J-Q-K。
/// </summary>
public static class PokerHandEvaluator
{
    // 5 张牌 → 牌型
    public static HandType Evaluate(List<CardDataSO> cards)
    {
        // ① 判断同花（5 张花色全相同）
        bool isFlush = true;
        CardSuit firstSuit = cards[0].suit;
        for (int i = 1; i < cards.Count; i++)
        {
            if (cards[i].suit != firstSuit) { isFlush = false; break; }
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

        // ③ 判断顺子（排序后连续）
        bool isStraight = IsConsecutive(ranks);

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

    // 牌型 → 筹码（想调数值就改这里）
    public static int GetChips(HandType type)
    {
        if (type == HandType.StraightFlush) return 50;
        if (type == HandType.FourOfAKind) return 35;
        if (type == HandType.FullHouse) return 25;
        if (type == HandType.Flush) return 18;
        if (type == HandType.Straight) return 15;
        if (type == HandType.ThreeOfAKind) return 10;
        if (type == HandType.TwoPair) return 6;
        if (type == HandType.OnePair) return 3;
        return 1; // HighCard
    }
}
