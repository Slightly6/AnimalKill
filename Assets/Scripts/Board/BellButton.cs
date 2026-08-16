using UnityEngine;

/// <summary>
/// 铃铛按钮。挂在铃铛 GameObject 上（带 SphereCollider）。
/// 点了 = 结束出牌阶段，进入战斗。
/// </summary>
public class BellButton : MonoBehaviour
{
    void Awake()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        col.isTrigger = true;
    }

    void OnMouseDown()
    {
        // 发出"结束出牌阶段"信号，BattleManager 收到就进入战斗
        EventBus.Publish(new EndPlayPhaseEvent());
        Debug.Log("[铃铛] 结束出牌，开始战斗");
    }
}
