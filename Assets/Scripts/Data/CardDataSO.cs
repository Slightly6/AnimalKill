using UnityEngine;

// ========== 花色 ==========
public enum CardSuit
{
    Spade,    // ♠ 黑桃  紫色
    Heart,    // ♥ 红桃  红色
    Diamond,  // ♦ 方片  金色
    Club      // ♣ 梅花  绿色
}

// ========== 点数 ==========
public enum CardRank
{
    Ace = 1,
    Two = 2, Three = 3, Four = 4, Five = 5,
    Six = 6, Seven = 7, Eight = 8, Nine = 9, Ten = 10,
    Jack = 11, Queen = 12, King = 13
}

// ========== 卡牌数据 ==========
[CreateAssetMenu(fileName = "NewCard", menuName = "Data/Card Data")]
public class CardDataSO : ScriptableObject
{
    public CardSuit suit;// 花色
    public CardRank rank;// 点数 1~13

    public string abilityName = "";
    public string description; // 描述
    public HookEffectType effectType; // 效果类型枚举
    public Rarity rarity;      // 稀有度
    public int price;          // 价格  
    // ---- 文字 ----

    public string GetSuitSymbol()
    {
        if (suit == CardSuit.Spade) return "♠";
        if (suit == CardSuit.Heart) return "♥";
        if (suit == CardSuit.Diamond) return "♦";
        if (suit == CardSuit.Club) return "♣";
        return "?";
    }

    public string GetRankText()
    {
        if (rank == CardRank.Ace) return "A";
        if (rank == CardRank.King) return "K";
        if (rank == CardRank.Queen) return "Q";
        if (rank == CardRank.Jack) return "J";
        return ((int)rank).ToString();
    }

}
