using System.Collections.Generic;
using UnityEngine;

public enum HoleCard
{
    None = 0,
    Spade2 = 1,      // ♠2
    Spade3 = 2,      // ♠3
    Spade4 = 3,      // ♠4
    Spade5 = 4,      // ♠5
    Spade6 = 5,      // ♠6
    Spade7 = 6,      // ♠7
}

public abstract class AbilityEffectSO : ScriptableObject
{
    public string description;   // 这个效果是干啥的（鼠标悬停时显示）
    
    // 子类实现：抽牌、伤害、加筹码……
    public abstract bool Apply(Card self, Card target);
}
