using System.Collections;
using UnityEngine;

/// <summary>
/// 战斗总管——单关回合状态机。
/// 由 MapManager 调 StartLevel 开一关，跑到过关或玩家输。
/// 流程：玩家出牌 → 战斗（玩家攻击 → 敌方补牌 → 敌方攻击）→ 循环
/// </summary>
public class BattleManager : Singleton<BattleManager>
{
    public TurnPhase CurrentPhase { get; private set; }// 当前阶段
    public bool IsPlayerTurn { get; private set; } = true;// 只剩玩家回合，恒为 true
    public bool IsInBattle { get; private set; }   // 是否正在战斗

    private bool skipPlayPhase = false;  // 玩家点了结束回合
    private bool levelEnded = false;     // 本关结束（过关或玩家输）
    private Coroutine battleRoutine;

    private void Start()
    {
        // 监听结束出牌按钮
        EventBus.Subscribe<EndPlayPhaseEvent>(OnEndPlayPhase);
        EventBus.Subscribe<LevelClearedEvent>(OnLevelCleared);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<EndPlayPhaseEvent>(OnEndPlayPhase);
        EventBus.Unsubscribe<LevelClearedEvent>(OnLevelCleared);
    }

    // 玩家点了"结束回合"
    private void OnEndPlayPhase(EndPlayPhaseEvent e)
    {
        skipPlayPhase = true;
    }

    // 过关了（敌人筹码打光）
    private void OnLevelCleared(LevelClearedEvent e)
    {
        levelEnded = true;
    }

    // 开始一关（MapManager 调用）
    public void StartLevel(LevelConfig cfg)
    {
        IsInBattle = true;
        levelEnded = false;
        skipPlayPhase = false;
        if (battleRoutine != null) StopCoroutine(battleRoutine);
        battleRoutine = StartCoroutine(GameLoop());
    }

    // 主循环：跑到本关结束
    private IEnumerator GameLoop()
    {
        while (!levelEnded && !GameManager.Instance.IsGameOver)
        {
            yield return StartCoroutine(RunTurn());
        }
        IsInBattle = false;
    }

    // 跑一个玩家回合
    private IEnumerator RunTurn()
    {
        if (levelEnded || GameManager.Instance.IsGameOver) yield break;

        skipPlayPhase = false;

        // 1. 抽牌
        SetPhase(TurnPhase.Draw);
        yield return new WaitForSeconds(0.5f);

        // 2. 出牌（一直等玩家放牌，直到按铃铛）
        SetPhase(TurnPhase.Play);
        while (!skipPlayPhase)
        {
            if (levelEnded || GameManager.Instance.IsGameOver) yield break;
            yield return null;
        }

        // 3. 战斗：玩家攻击 → 敌方补牌 → 敌方攻击
        SetPhase(TurnPhase.Battle);
        yield return StartCoroutine(ResolveBattle());
        if (levelEnded || GameManager.Instance.IsGameOver) yield break;

        // 4. 结束
        SetPhase(TurnPhase.End);
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
            if (levelEnded || GameManager.Instance.IsGameOver) yield break;
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

        if (levelEnded || GameManager.Instance.IsGameOver) yield break;

        // 阶段2：敌方补牌上前（预出排填到空位）
        yield return new WaitForSeconds(0.3f);
        BoardManager.Instance.MovePreviewToCurrent();
        yield return new WaitForSeconds(0.4f);

        // 阶段3：敌方所有卡挨个攻击
        for (int i = 0; i < 5; i++)
        {
            if (levelEnded || GameManager.Instance.IsGameOver) yield break;
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
