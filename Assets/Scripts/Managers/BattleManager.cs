using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗总管——牌型攻击版（手牌制，槽位/拖牌上桌已删除）。
/// 只负责战斗流程编排和游戏逻辑（扣筹码/护盾/次数）。
/// 演出在 BattleView（同物体挂载），牌型计算在 PokerResolver，敌人在 EnemyController。
/// 交互：点手牌选中（BattleView 出虚影）→ 铃铛把牌飞到虚影位结算；弃牌按钮同理（isPlay 区分）。
/// </summary>
public class BattleManager : Singleton<BattleManager>
{
    public TurnPhase CurrentPhase { get; private set; }   // 当前阶段
    public bool IsPlayerTurn { get; private set; } = true;// 只剩玩家回合，恒为 true
    public bool IsInBattle { get; private set; }          // 是否正在战斗

    [Header("每关次数")]
    public int playsPerLevel = 8;      // 出牌次数+弃牌次数
    [Header("不勾选为call")]
    public bool isPlay = false;        // false 打人，true 加盾
    public int actionsLeft { get; private set; }

    [Header("护盾")]
    private int currentCheck = 0;      // 当前未使用，后期填（弃牌获得 80% 伤害的护盾，每回合衰减 1）

    private bool playRequested = false;   // 玩家按了铃铛
    private bool levelEnded = false;      // 本关结束（过关或玩家输）
    private bool resolving = false;       // 结算/弃牌演出中，锁操作
    private Coroutine battleRoutine;

    private void Start()
    {
        BattleView.Instance.Init();   // 初始化演出层（相机+阴影保底）
        EventBus.Subscribe<EndPlayPhaseEvent>(OnEndPlayPhase);
        EventBus.Subscribe<LevelClearedEvent>(OnLevelCleared);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<EndPlayPhaseEvent>(OnEndPlayPhase);
        EventBus.Unsubscribe<LevelClearedEvent>(OnLevelCleared);
    }

    private void OnEndPlayPhase(EndPlayPhaseEvent e)
    {
        playRequested = true;
        isPlay = e.isPlay;
    }

    private void OnLevelCleared(LevelClearedEvent e) { levelEnded = true; }

    // 道具占位：转发给 EnemyController（敌方回合后期接入）
    public void SkipEnemyAttack()
    {
        if (EnemyController.Instance != null) EnemyController.Instance.SkipEnemyAttack();
    }

    // 开始一关（MapManager 调用）
    public void StartLevel(LevelConfig cfg)
    {
        IsInBattle = true;
        levelEnded = false;
        resolving = false;
        playRequested = false;
        actionsLeft = playsPerLevel;
        BattleView.Instance.ClearGhosts();
        BattleView.Instance.ClearSelection();
        if (cfg.enemyConfig != null)
        {
            EnemyController.Instance.Initialize(cfg.enemyConfig);  
            EnemyController.Instance.PrepareIntent();            
        }
        if (battleRoutine != null) StopCoroutine(battleRoutine);
        battleRoutine = StartCoroutine(GameLoop());
    }

    // 主循环
    private IEnumerator GameLoop()
    {
        while (!levelEnded && !GameManager.Instance.IsGameOver)
        {
            // 出牌阶段：等玩家选牌 + 按铃铛（弃牌由事件单独处理，不走这个循环）
            SetPhase(TurnPhase.Draw);
            GameProgress.transitioning = false;
            SetPhase(TurnPhase.Play);
            playRequested = false;
            while (!playRequested)
            {
                if (levelEnded || GameManager.Instance.IsGameOver) yield break;
                yield return null;
            }

            SetPhase(TurnPhase.Battle);
            yield return StartCoroutine(ResolvePlayerHand());
            // 玩家出牌后接敌人回合
            if (levelEnded || GameManager.Instance.IsGameOver) yield break;
            yield return StartCoroutine(ResolveEnemyTurn());
        }
        IsInBattle = false;
    }

    // 铃铛结算：编排选牌→计算→演出→游戏逻辑→补牌
    private IEnumerator ResolvePlayerHand()
    {
        List<Card> cards = BattleView.Instance.GetSortedSelectedCards();

        if (cards.Count == 0)
        {
            Debug.Log("[铃铛] 没选牌，先点选手牌");
            yield break;
        }
        if (actionsLeft <= 0)
        {
            Debug.Log("[铃铛] 本关出牌次数用完了");
            yield break;
        }

        resolving = true;
        GameProgress.transitioning = true;   // 锁输入（InputLocked = mapOpen || transitioning）

        // 1. 清选择 + 移手牌列表
        BattleView.Instance.ClearSelection();
        foreach (Card c in cards)
        {
            if (c != null) DeckManager.Instance.RemoveFromHand(c);
        }
            

        // 2. 纯计算（PokerResolver）
        List<CardDataSO> datas = cards.ConvertAll(c => c.Data);
        HandType type = PokerResolver.Evaluate(datas);
        int baseChips = PokerResolver.GetBaseChips(type);
        int mult = PokerResolver.GetMultiplier(type);
        HashSet<int> coreIndices = PokerResolver.GetCoreCardIndices(datas, type);
        int cardBonus = PokerResolver.CalcCardBonus(cards, coreIndices);
        int damage = PokerResolver.CalcDamage(baseChips, cardBonus, mult);

        // 3. 纯演出（BattleView）：飞牌+计分+飞撞+销毁牌
        yield return BattleView.Instance.PlayResolveSequence(
            cards, type, baseChips, mult, damage, coreIndices, isPlay);

        // 4. 游戏逻辑（出牌打人扣敌人筹码；弃牌加盾后期填 currentCheck）
        if (!isPlay)
        {
            GameManager.Instance.EnemyLoseChips(damage);
        }
         else 
        { 
            currentCheck += Mathf.RoundToInt(damage * 0.8f); 
        }  // 后期填

        actionsLeft--;

        // 5. 补手牌
        if (DeckManager.Instance != null) yield return DeckManager.Instance.RefillHand();

        resolving = false;
        GameProgress.transitioning = false;
    }

        // 敌人回合：意图出牌→演出→扣玩家筹码→补牌
    private IEnumerator ResolveEnemyTurn()
    {
        if (EnemyController.Instance == null) yield break;

        resolving = true;
        GameProgress.transitioning = true;

        yield return new WaitForSeconds(0.4f);

        EnemyPlayData playData = EnemyController.Instance.BuildPlayData();

        // Check：不出牌
        if (playData.intent == EnemyIntentType.Check)
        {
            Debug.Log("[敌人] CHECK，跳过攻击");
            yield return new WaitForSeconds(0.4f);
            EnemyController.Instance.PrepareIntent();
            resolving = false;
            GameProgress.transitioning = false;
            yield break;
        }

        // Call/Raise：算伤害（纯牌型伤害 × 意图倍率）
        List<CardDataSO> datas = playData.cards;
        HandType type = PokerResolver.Evaluate(datas);
        int baseChips = PokerResolver.GetBaseChips(type);
        int mult = PokerResolver.GetMultiplier(type);
        HashSet<int> coreIndices = PokerResolver.GetCoreCardIndices(datas, type);
        int cardBonus = PokerResolver.CalcCardBonus(datas, coreIndices);
        int rawDamage = PokerResolver.CalcDamage(baseChips, cardBonus, mult);
        int damage = Mathf.RoundToInt(rawDamage * playData.damageMultiplier);

        // 演出
        yield return BattleView.Instance.PlayEnemyResolveSequence(playData, damage);

        // 游戏逻辑：先扣护盾，剩余扣玩家筹码
        int blocked = Mathf.Min(currentCheck, damage);
        currentCheck -= blocked;
        int actualDamage = damage - blocked;
        Debug.Log("[敌人攻击] 总:" + damage + " 护盾挡:" + blocked + " 实际:" + actualDamage);
        if (actualDamage > 0) GameManager.Instance.LoseChips(actualDamage);

        // 敌人手牌更新 + 补牌 + 下回合意图
        EnemyController.Instance.CommitPlayedCards(playData);
        EnemyController.Instance.RefillHand();
        BattleView.Instance.RefreshEnemyHand();
        EnemyController.Instance.PrepareIntent();

        resolving = false;
        GameProgress.transitioning = false;
    }
    // 换阶段 + 发事件
    private void SetPhase(TurnPhase phase)
    {
        CurrentPhase = phase;
        EventBus.Publish(new PhaseChangedEvent { phase = phase, isPlayerTurn = true });
    }
}
