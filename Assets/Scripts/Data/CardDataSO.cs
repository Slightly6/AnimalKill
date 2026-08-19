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

    // 动物
    public string animalName = "新动物";
    public string description = "";

    // 技能
    public string abilityName = "";
    public string abilityDesc = "";
    public Sprite abilityIcon;   // 技能图标（显示在卡牌正面，以后图鉴也用）

    // 外观
    public Sprite artwork;

    // ---- 数值 ----

    // 基础战力（攻=血 同一个值）= 点数（觉醒/额外加成在出牌时由 Card 算）
    public int GetPower()
    {
        return (int)rank;
    }

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
