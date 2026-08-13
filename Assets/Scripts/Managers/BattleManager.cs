using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗总管——回合状态机。
/// 流程：玩家出牌 → 战斗（玩家攻击 → 敌方补牌 → 敌方攻击）→ 循环
/// </summary>
public class BattleManager : Singleton<BattleManager>
{
    public TurnPhase CurrentPhase { get; private set; }// 当前阶段
    public bool IsPlayerTurn { get; private set; } = true;// 只剩玩家回合，恒为 true

    private bool skipPlayPhase = false;  // 玩家点了结束回合

    private void Start()
    {
        // 监听结束出牌按钮
        EventBus.Subscribe<EndPlayPhaseEvent>(OnEndPlayPhase);
        StartCoroutine(GameLoop());
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<EndPlayPhaseEvent>(OnEndPlayPhase);
    }

    // 玩家点了"结束回合"
    private void OnEndPlayPhase(EndPlayPhaseEvent e)
    {
        skipPlayPhase = true;
    }

    // 主循环：只跑玩家回合
    private IEnumerator GameLoop()
    {
        while (!GameManager.Instance.IsGameOver)
        {
            yield return StartCoroutine(RunTurn());
        }

        Debug.Log("游戏结束");
    }

    // 跑一个玩家回合
    private IEnumerator RunTurn()
    {
        skipPlayPhase = false;

        // 1. 抽牌
        SetPhase(TurnPhase.Draw);
        Debug.Log("[玩家回合] 抽牌");
        yield return new WaitForSeconds(0.5f);

        // 2. 出牌（一直等玩家放牌，直到按铃铛）
        SetPhase(TurnPhase.Play);
        Debug.Log("[玩家回合] 出牌");
        while (!skipPlayPhase)
        {
            yield return null;
        }

        // 3. 战斗：玩家攻击 → 敌方补牌 → 敌方攻击
        SetPhase(TurnPhase.Battle);
        Debug.Log("[玩家回合] 战斗");
        yield return StartCoroutine(ResolveBattle());

        // 4. 结束
        SetPhase(TurnPhase.End);
        Debug.Log("[玩家回合] 结束");
        yield return new WaitForSeconds(0.5f);
    }

    // 换阶段 + 发事件
    private void SetPhase(TurnPhase phase)
    {
        CurrentPhase = phase;
        EventBus.Publish(new PhaseChangedEvent
        {
            phase = phase,
            isPlayerTurn = true
        });
    }

    // ============================================================
    // 战斗结算：玩家攻击 → 敌方补牌 → 敌方攻击
    // ============================================================
    private IEnumerator ResolveBattle()
    {
        // 阶段1：玩家所有卡挨个攻击
        for (int i = 0; i < 5; i++)
        {
            Card attacker = BoardManager.Instance.GetCardAt(i, true);
            Card defender = BoardManager.Instance.GetCardAt(i, false);

            if (attacker == null) continue;

            if (defender != null)
            {
                // 对面有卡 → 冲过去打
                yield return attacker.StrikeAndReturn(defender);
            }
            else
            {
                // 对面没卡 → 打脸
                yield return attacker.FaceAnim();
            }

            yield return new WaitForSeconds(0.25f);
        }

        // 阶段2：敌方补牌上前（预出排填到空位）
        yield return new WaitForSeconds(0.3f);
        BoardManager.Instance.MovePreviewToCurrent();
        yield return new WaitForSeconds(0.4f);

        // 阶段3：敌方所有卡挨个攻击
        for (int i = 0; i < 5; i++)
        {
            Card attacker = BoardManager.Instance.GetCardAt(i, false);
            Card defender = BoardManager.Instance.GetCardAt(i, true);

            if (attacker == null) continue;

            if (defender != null)
            {
                // 对面有卡 → 冲过来打
                yield return attacker.StrikeAndReturn(defender);
            }
            else
            {
                // 对面没卡 → 打玩家脸
                yield return attacker.FaceAnim();
            }

            yield return new WaitForSeconds(0.25f);
        }
    }
}
