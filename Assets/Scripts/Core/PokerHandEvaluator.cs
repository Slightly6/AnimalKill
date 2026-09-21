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
/// 德州牌型判定：5 张牌 → 牌型，牌型 → 筹码。
/// 纯函数，不碰 Unity 对象，跟 CardAnimator 一样是静态工具类。
/// 点数 1~13，A = 1（最小），K = 13（最大）。最小顺子是 A-2-3-4-5，最大是 10-J-Q-K-A（A 当 14）。
                }
            }
        }
    // 1~5 张牌 → 牌型（玩家从手牌里选牌挂钩子，张数任意但不超过 5）
    // 对子/两对/三条/四条按实际张数判定；同花、顺子、葫芦、同花顺按扑克规则必须凑齐 5 张才算。
                // 把 A 从 1 挪到最后当 14，再判一次顺子（10-J-Q-K-A）
                int[] high = new int[ranks.Length];
                for (int i = 0; i < ranks.Length - 1; i++) high[i] = ranks[i + 1];
                high[ranks.Length - 1] = 14;
        // ① 判断同花（全部花色相同；少于 5 张不算同花）
                isStraight = IsConsecutive(high);
            }
        }

        // ④ 统计每种点数的张数（index 1~13）
        int[] count = new int[14];
        for (int i = 0; i < ranks.Length; i++) count[ranks[i]]++;

        // 最大重复张数 + 对子数量
        int maxSame = 0;
        // ② 取点数并排序（判断顺子用）
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
        // ③ 判断顺子（必须 5 张连续）。A 默认是 1（最小），但 10-J-Q-K-A 里 A 得当 14（最大）
        if (maxSame == 3) return HandType.ThreeOfAKind;
        if (pairCount == 2) return HandType.TwoPair;
        if (pairCount == 1) return HandType.OnePair;
        return HandType.HighCard;
    }

                // 把 A 从 1 挪到最后当 14，再判一次顺子（10-J-Q-K-A）
    // 判断数组是否连续（2,3,4,5,6）
    static bool IsConsecutive(int[] arr)
    {
        for (int i = 0; i < arr.Length - 1; i++)
        {
            if (arr[i + 1] != arr[i] + 1) return false;
        }
        // ④ 统计每种点数的张数（index 1~13）
        return true;
    }

        // 最大重复张数 + 对子数量
    // 牌型 → 筹码（想调数值就改这里）
    public static int GetChips(HandType type)
    {
        if (type == HandType.StraightFlush) return 50;
        if (type == HandType.FourOfAKind) return 35;
        if (type == HandType.FullHouse) return 25;
        if (type == HandType.Flush) return 18;
        if (type == HandType.Straight) return 15;
        // ⑤ 从高到低判定
        if (type == HandType.ThreeOfAKind) return 10;
        if (type == HandType.TwoPair) return 6;
        if (type == HandType.OnePair) return 3;
        return 1; // HighCard
    }
}
    // 判断数组是否连续（2,3,4,5,6）
        {
        }
