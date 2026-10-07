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


/// <summary>
/// 通用参数化技能效果。所有技能共用这一个类：
/// 选 Kind（做什么）+ amount/amount2（数值），触发时机在外层 AbilitySO.trigger 上配。
/// 六个战斗技能全是"登场挂标记，战斗流程读标记"，这里的 case 只负责挂标记。
/// </summary>
[CreateAssetMenu(fileName = "New Ability Effect", menuName = "Data/Ability Effect")]
public class AbilityEffectSO : ScriptableObject
{
    [Tooltip("技能种类（决定做什么）")]
    public HoleCard kind = HoleCard.None;
    [Tooltip("副数值（备用）")]
    public int amount2 = 0;

    // 返回 true = 技能真的发动了（用于触发闪光特效）；false = 条件不满足/被免疫，没发动
    public bool Apply(Card self, Card target)
    {
        if (self == null || self.IsDead) return false;

        return true;
    }
}
