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
    Two = 2, Three = 3, Four = 4, Five = 5,
    Six = 6, Seven = 7, Eight = 8, Nine = 9, Ten = 10,
    Jack = 11, Queen = 12, King = 13, Ace = 14
}

// ========== 卡牌数据 ==========
[CreateAssetMenu(fileName = "NewCard", menuName = "Data/Card Data")]
public class CardDataSO : ScriptableObject
{
    public CardSuit suit;
    public CardRank rank;

    // 动物
    public string animalName = "新动物";
    public string description = "";

    // 技能
    public string abilityName = "";
    public string abilityDesc = "";
    public bool isAwakened = false;

    // 外观
    public Sprite artwork;

    // 运行时叠加
    [System.NonSerialized] public int bonusPower = 0;

    // ---- 数值 ----

    // 战力（攻=血 同一个值）= 点数 + 觉醒+3 + 额外叠加
    public int GetPower()
    {
        int p = (int)rank;
        if (isAwakened) p += 3;
        p += bonusPower;
        return p;
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

    public string GetFullName()
    {
        string star = isAwakened ? "⭐" : "";
        return star + GetSuitSymbol() + GetRankText() + " " + animalName;
    }

    // ---- 外观 ----

    public Color GetSuitColor()
    {
        if (suit == CardSuit.Spade) return new Color(0.2f, 0.2f, 0.35f);
        if (suit == CardSuit.Heart) return new Color(0.85f, 0.2f, 0.2f);
        if (suit == CardSuit.Diamond) return new Color(0.9f, 0.7f, 0.05f);
        if (suit == CardSuit.Club) return new Color(0.1f, 0.5f, 0.15f);
        return Color.gray;
    }

    // ---- 操作 ----

    public CardDataSO Clone()
    {
        var copy = Instantiate(this);
        copy.name = name;
        copy.bonusPower = 0;
        return copy;
    }

    public void Awaken()
    {
        isAwakened = true;
    }

    public void AddBonus(int amount)
    {
        bonusPower += amount;
    }
}
