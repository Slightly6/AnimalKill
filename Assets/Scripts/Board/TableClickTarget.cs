using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TableActionType
{
    Play,      // 出牌堆：选中的牌按牌型打人
    Discard,   // 弃牌堆：选中的牌弃进主池
    EndTurn    // 铃铛：结束回合，敌人行动
}

/// <summary>
/// 桌面可点击目标（出牌堆/弃牌堆/铃铛）。物体需要 Collider（isTrigger）。
/// </summary>
public class TableClickTarget : MonoBehaviour
{
    public TableActionType actionType;

    void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // // 商店/奖励关没有战斗，三个目标全藏
        // if (GameProgress.IsNonBattleNode())
        //     gameObject.SetActive(false);
    }

    void OnMouseDown()
    {
        
        if (GameProgress.InputLocked) return;
        if (GameProgress.IsNonBattleNode() && actionType == TableActionType.EndTurn)
        {
            if (MapManager.Instance != null) MapManager.Instance.ClearSpawnedNonBattleObjects();
            return;
        }

        if (BattleManager.Instance == null || !BattleManager.Instance.IsInBattle) return;
        BattleManager.Instance.HandleTableAction(actionType);
    }
}