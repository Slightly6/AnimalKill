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
    public int price;          // 价格  

    [Header("道具卡")]
    public int itemCost = 1;               // 道具卡消耗的点数
    [Header("道具卡效果（cardType=Item 专用；拖 ItemEffectSO 资产，使用时全部触发）")]
    public List<ItemEffectSO> itemEffects = new();

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

// ========== 卡牌显示信息统一出口 ==========
// 名字/描述全部读效果自带的（效果资产是唯一数据源），卡资产上不再重复填。
// 普通无效果牌回退到 花色+点数 当标题。
public static class CardInfo
{
    // 显示标题：有效果用第一个效果的 effectName，否则花色+点数
    public static string GetTitle(CardDataSO data)
    {
        if (data.cardType == CardType.Item)
        {
            var e = (data.itemEffects != null && data.itemEffects.Count > 0) ? data.itemEffects[0] : null;
            if (e != null && !string.IsNullOrEmpty(e.effectName)) return e.effectName;
        }
        else if (data.effects != null && data.effects.Count > 0)
        {
            var e = data.effects[0];
            if (e != null && !string.IsNullOrEmpty(e.effectName)) return e.effectName;
        }
        return data.GetSuitSymbol() + data.GetRankText();
    }

    // 显示描述：把卡上所有效果的 description 拼起来（道具读 itemEffects，普通牌读 effects）
    public static string GetDescription(CardDataSO data)
    {
        var parts = new List<string>();
        if (data.cardType == CardType.Item)
        {
            if (data.itemEffects != null)
                foreach (var e in data.itemEffects)
                    if (e != null && !string.IsNullOrEmpty(e.description)) parts.Add(e.description);
        }
        else if (data.effects != null)
        {
            foreach (var e in data.effects)
                if (e != null && !string.IsNullOrEmpty(e.description)) parts.Add(e.description);
        }
        return string.Join("\n", parts);
    }
}
