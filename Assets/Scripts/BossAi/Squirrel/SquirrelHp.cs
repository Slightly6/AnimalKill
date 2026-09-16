using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;   // 要用 Image 做血条

public class SquirrelHp : MonoBehaviour, HpInterface
{
    [Header("血量设置")]
    public int maxHp = 100; // 最大血量
    private int currentHp;  // 当前血量

    public bool isDie=false;
    [Header("血条")]
    public Image hpFill;   // 血条填充（Image，Type=Filled），拖进来
    private Animator anim;
    public int MaxHp { get { return maxHp; } }
    public int CurrentHp { get { return currentHp; } }
    
    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        currentHp = maxHp;
        UpdateHpBar();   // 开局满血

        // 打牌阶段结束（Boss 战）前血条先藏起来，Boss 战开始才显示
        ShowHpBar(false);
    }

    public void TakeDamage(float damage)
    {
        if (isDie) return; 
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

    // 显示/隐藏血条（打牌阶段藏起来，Boss 战开始才显示）
    public void ShowHpBar(bool on)
    {
        if (hpFill != null)
        {
            hpFill.gameObject.SetActive(on);
        }
    }

    public void Die()
    {
        if (isDie) return;
        Debug.Log("Squirrel died.");
        // 广播死亡事件（走全局事件总线，谁想听就 EventBus.Subscribe）
        EventBus.Publish(new DiedEvent { isPlayer = false });
        isDie=true;

        BossSquirrel boss = GetComponent<BossSquirrel>();
        if (boss != null)
        {
            boss.enabled = false;        // 停掉 Boss AI 的 Update（移动/攻击/转向）
            boss.StopAllCoroutines();    // 停掉还在跑的协程（登场怒吼等），防止它再 SetState 干扰死亡
        }

        if (anim != null)
        {
            anim.applyRootMotion = false;   // 关根运动：死亡时不再转发攻击位移，Boss 原地倒下不乱动

            // 把 State 设成不匹配任何「Any State 过渡」的值（0），
            // 否则死亡动画会被 State==3~11 的过渡立刻切走，只播一帧就被打断
            anim.SetInteger("State", 0);

            // 立即强制切到死亡动画（跳过过渡，打断当前攻击/移动/怒吼）
            anim.Play("Die", 0, 0f);
        }

        StartCoroutine(DieRoutine());
    }
    IEnumerator DieRoutine()
    {
        // 等死亡动画播完（比如 2 秒）
        yield return new WaitForSeconds(4f);

        Destroy(gameObject);
    }
}
