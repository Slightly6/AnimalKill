using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 原子效果：对敌人造成 N 点筹码伤害。
/// </summary>
[CreateAssetMenu(fileName = "Damagehalf", menuName = "Effects/伤害敌人")]
public class Damagehalf : AbilityEffectSO
{
    [Header("敌人减伤倍率")]
    public float damagemult = 0.5f;

    public override bool Apply(Card self, Card target)
    {
        if (EnemyController.Instance == null) return false;
        EnemyController.Instance.MultiplyNextAttack(damagemult);
        return true;
    }
}