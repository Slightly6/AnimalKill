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
    private bool skipEnemyAttack = false;  // 道具效果：本回合跳过敌方攻击
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

    // 道具调用：本回合跳过敌方攻击阶段
    public void SkipEnemyAttack()
    {
        skipEnemyAttack = true;
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
        skipEnemyAttack = false;   // 每回合重置

        // 1. 抽牌
        SetPhase(TurnPhase.Draw);
        // 回合开始结算：双方毒发 → 回合开始技能（再生/嗜血/蛰伏/食利）
        yield return StartCoroutine(ProcessTurnStart());
        // 进入摸牌阶段 = 切关过渡结束，解锁玩家交互（首回合生效；后续回合保持 false 无害）。
        // 卷轴飞走期间 root 全屏 Image 仍挡射线，提前解锁不会误点 3D 物体。
        GameProgress.transitioning = false;
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
        ProcessTurnEnd();
        yield return new WaitForSeconds(0.5f);
    }

    // 回合开始：先毒发（可能死人），再触发双方在场牌的 OnTurnStart 技能
    IEnumerator ProcessTurnStart()
    {
        var board = BoardManager.Instance;

        // 快照，避免死亡/繁殖改变槽位导致遍历错乱
        var all = new System.Collections.Generic.List<Card>();
        board.ForEachCard(true, c => all.Add(c));
        board.ForEachCard(false, c => all.Add(c));

        for (int i = 0; i < all.Count; i++)
        {
            Card c = all[i];
            if (c == null || c.IsDead) continue;
            c.TickPoison();   // 毒发（无视护甲，可能直接毒死）
            yield return new WaitForSeconds(0.12f);
        }

        for (int i = 0; i < all.Count; i++)
        {
            Card c = all[i];
            if (c == null || c.IsDead) continue;
            c.TriggerAbility(AbilityTrigger.OnTurnStart, null);
            yield return new WaitForSeconds(0.08f);
        }
    }

    // 回合结束：触发双方 OnTurnEnd 技能（目前留口，后期自己配）
    void ProcessTurnEnd()
    {
        var board = BoardManager.Instance;
        board.ForEachCard(true, c => c.TriggerAbility(AbilityTrigger.OnTurnEnd, null));
        board.ForEachCard(false, c => c.TriggerAbility(AbilityTrigger.OnTurnEnd, null));
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
        var board = BoardManager.Instance;

        // 阶段1：玩家所有卡挨个攻击
        for (int i = 0; i < 5; i++)
        {
            if (levelEnded || GameManager.Instance.IsGameOver) yield break;
            Card attacker = board.GetCardAt(i, true);
            if (attacker == null) continue;

            // 被蛛网/冰冻封住：这一轮不能攻击（攻击后解除，下一轮恢复）
            if (attacker.flagWeb)
            {
                attacker.flagWeb = false;
                Debug.Log("[战斗] " + attacker.CardName + " 被封住，无法攻击");
                yield return new WaitForSeconds(0.2f);
                continue;
            }

            // 连击：每轮攻击次数（豹/犀牛=2）
            int strikes = attacker.flagDoubleStrike ? 2 : 1;
            for (int s = 0; s < strikes; s++)
            {
                if (levelEnded || GameManager.Instance.IsGameOver) yield break;
                if (attacker.IsDead) break;

                Card defender = board.GetCardAt(i, false);

                // 越道猎杀：无视对位，扑杀全场最强敌
                if (attacker.flagCrossLane)
                {
                    Card strongest = board.FindStrongestEnemy();
                    if (strongest != null) defender = strongest;
                }

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
        }

        if (levelEnded || GameManager.Instance.IsGameOver) yield break;
        if (skipEnemyAttack)
        {
            Debug.Log("[回合] 敌方攻击被道具跳过");
        }
        else
        {
            // 阶段2：敌方补牌上前（预出排填到空位）
        yield return new WaitForSeconds(0.3f);
        BoardManager.Instance.MovePreviewToCurrent();
        yield return new WaitForSeconds(0.4f);

        // 阶段3：敌方所有卡挨个攻击（道具可跳过）

            for (int i = 0; i < 5; i++)
            {
                if (levelEnded || GameManager.Instance.IsGameOver) yield break;
                Card attacker = board.GetCardAt(i, false);
                if (attacker == null) continue;

                // 蛛网：敌方被封一回合，攻击后解封
                if (attacker.flagWeb)
                {
                    attacker.flagWeb = false;
                    Debug.Log("[战斗] " + attacker.CardName + " 被蛛网封住，无法攻击");
                    yield return new WaitForSeconds(0.2f);
                    continue;
                }

                Card defender = board.GetCardAt(i, true);

                if (defender != null && !defender.flagUntargetable)
                {
                    // 对面有卡且可命中 → 冲过来打
                    yield return attacker.StrikeAndReturn(defender);
                }
                else if (defender != null && defender.flagUntargetable)
                {
                    // 对面遁地：照冲，但扑空不结算伤害，也不能转打脸
                    yield return attacker.StrikeAndReturn(defender, false);
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
}

