using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗总管——回合状态机。
/// 流程：抽牌 → 出牌 → 战斗 → 结束 → 换边 → 循环
/// </summary>
public class BattleManager : Singleton<BattleManager>
{
    [Header("玩家出牌时长（秒）")]
    public float playPhaseDuration = 15f;

    public TurnPhase CurrentPhase { get; private set; }// 当前阶段
    public bool IsPlayerTurn { get; private set; } = true;// 当前回合是玩家回合吗

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

    // 主循环
    private IEnumerator GameLoop()
    {
        while (!GameManager.Instance.IsGameOver)
        {
            // 玩家回合
            yield return StartCoroutine(RunTurn(true));
            if (GameManager.Instance.IsGameOver) break;

            // 敌人回合
            yield return StartCoroutine(RunTurn(false));
        }

        Debug.Log("游戏结束");
    }

    // 跑一个回合
    private IEnumerator RunTurn(bool isPlayerTurn)
    {
        IsPlayerTurn = isPlayerTurn;
        string who = isPlayerTurn ? "玩家" : "敌方";
        skipPlayPhase = false;

        // 1. 抽牌
        SetPhase(TurnPhase.Draw);
        Debug.Log("[" + who + "回合] 抽牌");
        yield return new WaitForSeconds(0.5f);

        // 2. 出牌
        SetPhase(TurnPhase.Play);
        Debug.Log("[" + who + "回合] 出牌");

        if (isPlayerTurn)
        {
            // 一直等玩家放牌，直到按铃铛（发 EndPlayPhaseEvent）
            while (!skipPlayPhase)
            {
                yield return null;
            }
        }
        else
        {
            // 敌方：预出排下移
            yield return new WaitForSeconds(0.8f);
            BoardManager.Instance.MovePreviewToCurrent();
            yield return new WaitForSeconds(0.5f);
        }

        // 3. 战斗
        SetPhase(TurnPhase.Battle);
        Debug.Log("[" + who + "回合] 战斗");
        yield return StartCoroutine(ResolveBattle(isPlayerTurn));

        // 4. 结束
        SetPhase(TurnPhase.End);
        Debug.Log("[" + who + "回合] 结束");
        yield return new WaitForSeconds(0.5f);
    }

    // 换阶段 + 发事件
    private void SetPhase(TurnPhase phase)
    {
        CurrentPhase = phase;
        EventBus.Publish(new PhaseChangedEvent
        {
            phase = phase,
            isPlayerTurn = IsPlayerTurn
        });
    }

    // ============================================================
    // 战斗结算：5 条路逐一互砍
    // ============================================================
    private IEnumerator ResolveBattle(bool isPlayerTurn)
    {
        for (int i = 0; i < 5; i++)
        {
            Card attacker = BoardManager.Instance.GetCardAt(i, isPlayerTurn);
            Card defender = BoardManager.Instance.GetCardAt(i, !isPlayerTurn);

            if (attacker == null) continue;

            if (defender != null)
            {
                // 双方都有卡 → 互殴
                attacker.Fight(defender);
            }
            else
            {
                // 没卡挡 → 打脸
                int damage = attacker.CurrentPower;
                if (isPlayerTurn)
                    GameManager.Instance.DamageEnemy(damage);
                else
                    GameManager.Instance.DamagePlayer(damage);

                Debug.Log("[战斗] " + attacker.CardName + " 打脸 " + damage + " 点");
            }

            yield return new WaitForSeconds(0.3f);
        }
    }

}
