using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;   // 要用 Image 做血条
using System;           // 要用 Action 做事件

public class HeroHp : MonoBehaviour, HpInterface
{
    [Header("血量设置")]
    public int maxHp = 100; // 最大血量
    private int currentHp;  // 当前血量

    [Header("血条")]
    public Image hpFill;   // 血条填充（Image，Type=Filled），拖进来

    [Header("音效")]
    public bool playHitSound = true;   // 掉血时放命中音效

    // 掉血事件：每次扣血广播一次，参数是这次扣的血量。
    // 谁想监听（放音效、屏幕震动、飘伤害数字），就 += 订阅它。
    public event Action<float> OnDamaged;

    public int MaxHp { get { return maxHp; } }
    public int CurrentHp { get { return currentHp; } }

    void Start()
    {
        currentHp = maxHp;
        UpdateHpBar();   // 开局满血

        // 订阅掉血事件：掉血就放音效（方法订阅，不是 lambda）
        if (playHitSound) OnDamaged += PlayHitSound;
    }

    public void TakeDamage(float damage)
    {
        currentHp -= (int)damage;
        if (currentHp < 0) currentHp = 0;   // 别扣成负数
        UpdateHpBar();                       // 每次扣血都刷新血条

        // 广播掉血事件（有订阅者才通知，比如放音效）
        if (OnDamaged != null) OnDamaged(damage);

        if (currentHp <= 0)
        {
            Die();
        }
    }

    // 订阅的方法：掉血时放命中音效
    void PlayHitSound(float damage)
    {
        AudioManager.Instance.PlayHit();
    }

    // 血条填充比例 = 当前血 / 最大血（0=空 1=满）
    private void UpdateHpBar()
    {
        if (hpFill != null)
        {
            hpFill.fillAmount = (float)currentHp / (float)maxHp;
        }
    }

    private void Die()
    {
        Debug.Log("Hero died.");
        // 玩家死亡逻辑以后填：重生、回标题、扣筹码之类（别直接销毁玩家）
    }
}
