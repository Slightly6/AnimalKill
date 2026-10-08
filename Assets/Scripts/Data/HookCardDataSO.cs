using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum HookEffectType
{
    DrawCard,      // 抽牌
    GainChips,     // 获得筹码
    DealDamage,    // 造成伤害
    Heal,          // 治疗
    Buff,          // 增益
    Debuff           // 减益
}   
public enum Rarity
{
    Common,// 普通
    Rare,// 稀有
    Epic,// 史诗
    Legendary// 传奇
}
[CreateAssetMenu(
    fileName = "HookCardData"
)]
public class HookCardDataSO : ScriptableObject
{
    public string HookCardName;        // 名字
    public string description; // 描述
    public HookEffectType effectType; // 效果类型枚举
    public Rarity rarity;      // 稀有度
    public int price;          // 价格  

}
