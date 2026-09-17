using UnityEngine;

/// <summary>
/// 效果：直接扣敌方 amount 筹码（等于打脸伤害）。
/// </summary>
[CreateAssetMenu(fileName = "DamageEnemyEffect", menuName = "Shop Effects/Damage Enemy")]
public class DamageEnemyEffectSO : ItemEffectSO
{
    public int amount = 3;

    public override void Apply()
    {
        GameManager.Instance.EnemyLoseChips(amount);
        Debug.Log("[道具] 敌方 -" + amount + " 筹码");
    }
}
