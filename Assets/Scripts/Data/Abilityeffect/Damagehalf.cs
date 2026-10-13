using UnityEngine;

/// 敌人下次攻击减伤
[CreateAssetMenu(fileName = "Damagehalf", menuName = "Effects/伤害敌人")]
public class Damagehalf : AbilityEffectSO
{
    public float damagemult = 0.5f;

    public override void Apply()
    {
        if (EnemyController.Instance == null) return;
        EnemyController.Instance.MultiplyNextAttack(damagemult);
    }
}
