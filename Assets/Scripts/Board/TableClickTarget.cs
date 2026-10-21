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
    public RouletteSpin myRoulette; 
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

        // 非战斗节点：铃铛 = 收场离开本节点（商店 + 事件关）。
        // 注意不能只用 IsNonBattleNode —— 它只认商店；事件关如果漏判，会掉到下面的战斗分支，
        // 而 IsInBattle 是 false，直接 return，铃铛点了什么都不会发生。
        bool nonBattle = GameProgress.IsNonBattleNode()
                      || GameProgress.currentNodeType == MapNodeType.Event;

        if (GameProgress.currentNodeType == MapNodeType.Event)
        {
            // 事件关铃铛 = 离场。四类情况拦住：
            // ① 离场流程已在跑（点过一次铃铛）：从点击那刻到出地图彻底失效，
            //    宝箱选择阶段也不能跳——那时 levelEndBusy 会被 EnhanceManager 解锁，得用专用标记拦
            // ② 轮盘掉落/飞回/转动中：这时点离场 FlyBack 会被拒收，轮盘会赖在桌上不走
            // ③ 转动判定用程序查找，不依赖 Inspector 拖引用（myRoulette 忘拖也不漏拦）
            if (MapManager.Instance != null && MapManager.Instance.NodeFinishRunning) return;

            if (RouletteDrop.Instance != null && RouletteDrop.Instance.IsBusy) return;

            if (myRoulette != null && myRoulette.IsSpinning) return;
            RouletteSpin spin = FindObjectOfType<RouletteSpin>();
            if (spin != null && spin.IsSpinning) return;

            MapManager.Instance.FinishNonBattleNode();
            return;
        }

        if (nonBattle && actionType == TableActionType.EndTurn)
        {

            MapManager.Instance.FinishNonBattleNode();
            return;
        }

        if (BattleManager.Instance == null || !BattleManager.Instance.IsInBattle) return;
        BattleManager.Instance.HandleTableAction(actionType);
    }
}