using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;   // 要用 Image 做血条

public class HeroHp : MonoBehaviour, HpInterface
{
    [Header("血量设置")]
    public int maxHp = 100; // 最大血量
    private int currentHp;  // 当前血量

    [Header("血条")]
    public Image hpFill;   // 血条填充（Image，Type=Filled），拖进来

    public int MaxHp { get { return maxHp; } }
    public int CurrentHp { get { return currentHp; } }

    void Start()
    {
        currentHp = maxHp;
        UpdateHpBar();   // 开局满血
    }

    public void TakeDamage(float damage)
    {
        currentHp -= (int)damage;
        if (currentHp < 0) currentHp = 0;   // 别扣成负数
        UpdateHpBar();                       // 每次扣血都刷新血条

        if (currentHp <= 0)
        {
            Die();
        }
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
        // 广播死亡事件（走全局事件总线，谁想听就 EventBus.Subscribe）
        EventBus.Publish(new DiedEvent { isPlayer = true });
        // 玩家死亡逻辑以后填：重生、回标题、扣筹码之类（别直接销毁玩家）
    }
}
