using UnityEngine;

/// <summary>
/// 弃牌按钮。挂在弃牌 GameObject 上（带 Collider）。
/// 点了 = 把当前选中的手牌弃进弃牌堆，立刻补牌（每关次数由 BattleManager 管）。
/// 场景里可以直接复制铃铛物体改个颜色/名字挂这个脚本。
/// </summary>
public class DiscardButton : MonoBehaviour
{
    void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Start()
    {
        // 商店/奖励关没有战斗，藏掉弃牌按钮
        if (GameProgress.IsNonBattleNode()) gameObject.SetActive(false);
    }

    void OnMouseDown()
    {
        if (GameProgress.InputLocked)   // 卷轴地图打开/结算中不能弃牌
        {
            Narrator.Say(SpeakTopic.ActionDuringMap);
            return;
        }

        EventBus.Publish(new DiscardHandEvent());
        Debug.Log("[弃牌按钮] 请求弃牌");
    }
}
