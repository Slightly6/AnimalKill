using UnityEngine;
using System.Collections.Generic;

// ========== 卡牌类型 ==========
public enum CardType
{
    Normal,   // 普通扑克牌（参与牌型判定）
    Item      // 道具卡（打出时触发效果，不参与牌型）
}

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
    [Header("类型")]
    public CardType cardType = CardType.Normal;

    public CardSuit suit;// 花色
    public CardRank rank;// 点数 1~13

    [Header("牌面贴图")]
    public Texture2D faceTexture;   // 整张牌面图（画好的黑桃1~7拖这里）；没拖则回退到花色+点数文字
    [Header("效果（直接拖入 AbilityEffectSO 资产，打出时全部触发）")]
    public List<AbilityEffectSO> effects = new();   // 卡牌效果列表（普通牌留空即可）
    public string abilityName = "";
    public string description; // 描述
    public HookEffectType effectType; // 效果类型枚举
    public Rarity rarity;      // 稀有度
    public int price;          // 价格  

    [Header("道具卡")]
    public int itemCost = 1;               // 道具卡消耗的点数

    [Header("拟态（特殊牌：手牌里完全变成最左那张，打出时随机变成手里一张）")]
    public bool isMimic;

    [System.NonSerialized] public bool isRuntimeClone;   // 运行时克隆体标记：true = 牌库里的独立个体，可安全强化（原资产永远只读）
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
