using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponDamage : MonoBehaviour
{
    public Collider attackCollider;
    private float currentDamage = 10f;

    // 临时调试：打印自己挂在哪个物体上
    void Start()
    {
        Debug.Log(gameObject.name + " 上的 WeaponDamage 初始化");
    }

    // 设置这一招的伤害
    public void SetDamage(float damage)
    {
        currentDamage = damage;
        Debug.Log(gameObject.name + " 伤害设为 " + damage);   // 临时调试
    }

    public void EnableWeaponDamage()
    {
        attackCollider.enabled = true;
        Debug.Log(gameObject.name + " 开碰撞盒");   // 临时调试
    }

    public void DisableWeaponDamage()
    {
        attackCollider.enabled = false;
        Debug.Log(gameObject.name + " 关碰撞盒");   // 临时调试
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(gameObject.name + " 撞到了: " + other.name);   // 临时调试
        if (other.TryGetComponent(out HpInterface hpInterface))
        {
            hpInterface.TakeDamage(currentDamage);
        }
        else
        {
            Debug.Log("但 " + other.name + " 没有 HpInterface");   // 临时调试
        }
    }
}