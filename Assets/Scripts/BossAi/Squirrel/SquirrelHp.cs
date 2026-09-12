using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SquirrelHp : MonoBehaviour, HpInterface
{
    [Header("血量设置")]
    public int maxHp = 100; // 最大血量
    private int currentHp;  // 当前血量

    public int MaxHp { get { return maxHp; } }
    public int CurrentHp { get { return currentHp; } }

    void Start()
    {
        currentHp = maxHp;
    }

    public void TakeDamage(float damage)
    {
        currentHp -= (int)damage;
        if (currentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Squirrel died.");
        // 在这里添加死亡逻辑，例如播放死亡动画、销毁对象等
        Destroy(gameObject);
    }
}
