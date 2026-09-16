using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponDamage : MonoBehaviour
{
    public Collider attackCollider;
    [Header("是不是 Boss 的武器（决定用哪套音效）")]
    public bool isBoss = false;   // Boss 的 WeaponDamage 勾上，音效就走 Boss 那套
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
        if (isBoss) AudioManager.Instance.PlayBossSwing();
        else AudioManager.Instance.PlaySwing();
        attackCollider.enabled = true;
        Debug.Log(gameObject.name + " 开碰撞盒");   // 临时调试
    }

    public void DisableWeaponDamage()
    {
        attackCollider.enabled = false;
        Debug.Log(gameObject.name + " 关碰撞盒");   // 临时调试
    }

    // 攻击动画「挥刀那一帧」→ 放挥剑破风音效（英雄和 Boss 的 clip 都填这个函数名）
    public void PlaySwingSound()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(gameObject.name + " 撞到了: " + other.name);   // 临时调试
        if (other.TryGetComponent(out HpInterface hpInterface))
        {
            // 打中了：放打击音效（英雄 / Boss 分开）
            if (isBoss) AudioManager.Instance.PlayBossHit();
            else AudioManager.Instance.PlayHit();
            hpInterface.TakeDamage(currentDamage);
        }
        else
        {
            Debug.Log("但 " + other.name + " 没有 HpInterface");   // 临时调试
        }
    }
}