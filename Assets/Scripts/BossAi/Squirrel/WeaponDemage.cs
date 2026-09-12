using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponDemage : MonoBehaviour
{
    [Header("武器伤害")]
    public float damage = 10f; // 武器造成的伤害值

    private void OnTriggerEnter(Collider collider)
    {
        // 检查碰撞的对象是否有 HpInterface 接口
        HpInterface hpInterface = collider.GetComponent<HpInterface>();
        if (hpInterface != null)
        {
            // 对敌人造成伤害
            hpInterface.TakeDamage(damage);
            Debug.Log("Weapon hit: " + collider.name + ", Damage: " + damage);
        }
    }
}
