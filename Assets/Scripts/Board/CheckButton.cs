using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Check 弃牌按钮。挂在桌面上的弃牌区域碰撞体上（BoxCollider/SphereCollider）。
/// 点了 = 把当前选中的手牌弃掉，获得 Check 护盾。
/// </summary>
public class CheckButton : MonoBehaviour
{
    void Awake()
    {
        // 确保是触发器
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Start()
    {
        if (GameProgress.IsNonBattleNode()) gameObject.SetActive(false);
    }

    void OnMouseDown()
    {
        if (GameProgress.InputLocked)
        {
            Narrator.Say(SpeakTopic.ActionDuringMap);
            return;
        }

        // 发出"弃牌"信号，BattleManager 收到就跑 DiscardSelected
        EventBus.Publish(new DiscardHandEvent());
        Debug.Log("[Check] 弃牌按钮被点击");
    }
}
