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

    // 回合开始：鲨鱼掉牙 → 触发双方在场牌的 OnTurnStart 技能
    IEnumerator ProcessTurnStart()
    {
        var board = BoardManager.Instance;

        // 快照，避免死亡改变槽位导致遍历错乱
        var all = new System.Collections.Generic.List<Card>();
        board.ForEachCard(true, c => all.Add(c));
        board.ForEachCard(false, c => all.Add(c));

        for (int i = 0; i < all.Count; i++)
        {
            Card c = all[i];
            if (c == null || c.IsDead) continue;
            c.TickTeeth();   // ♠7 潜水态每回合 -2 牙
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

            // ♠6 死亡翻滚：随机扑 1~5 次；普通牌 1 次
            int strikes = attacker.GetStrikeCount();
            for (int s = 0; s < strikes; s++)
            {
                if (levelEnded || GameManager.Instance.IsGameOver) yield break;
                if (attacker.IsDead) break;

                // 翻滚每次扑击都让技能特效闪一下（让玩家看到技能在持续发动）
                if (strikes > 1) attacker.FlashSkill();

                Card defender = board.GetCardAt(i, false);
                if (defender != null)
                {
                    // ♠7 潜水首击无敌 → ♠4 闪避 → 正常命中（判定敌我对称）
                    bool miss = defender.ConsumeDiveInvincible() || defender.RollEvade();
                    yield return attacker.StrikeAndReturn(defender, !miss);
                    // 翻滚目标中途死亡：剩余次数作废，不转打脸
                    if (defender.IsDead) break;
                }
                else
                {
                    // 对面没卡 → 打脸（只有第一段可以打脸，避免翻滚一轮连脸带牌全打）
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

                // ♠6 死亡翻滚：敌方鳄鱼也随机扑 1~5 次（敌我对称）
                int strikes = attacker.GetStrikeCount();
                for (int s = 0; s < strikes; s++)
                {
                    if (levelEnded || GameManager.Instance.IsGameOver) yield break;
                    if (attacker.IsDead) break;

                    // 翻滚每次扑击都让技能特效闪一下（敌我一致）
                    if (strikes > 1) attacker.FlashSkill();

                    Card defender = board.GetCardAt(i, true);
                    if (defender != null)
                    {
                        // 玩家方潜水无敌 / 闪避：敌方扑空，不能转打脸
                        bool miss = defender.ConsumeDiveInvincible() || defender.RollEvade();
                        yield return attacker.StrikeAndReturn(defender, !miss);
                        if (defender.IsDead) break;
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
}

