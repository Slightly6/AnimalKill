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
    [Header("点数（杀戮尖塔式）")]
    public int pointsPerTurn = 3;        // 每回合点数上限
    public int PointsLeft { get; private set; }   // 剩余点数

    [Header("回合抽牌动画预留（帧）")]
    public int drawFrames = 10;       

    public TurnPhase CurrentPhase { get; private set; }   // 当前阶段
    public bool IsPlayerTurn { get; private set; } = true;// 只剩玩家回合，恒为 true
    public bool IsInBattle { get; private set; }          // 是否正在战斗

    [Header("不勾选为call")]
    public bool isPlayfold = false;        // false 打人，true 弃牌进主池

    private bool endTurnRequested = false;   // 玩家按了铃铛
    private bool levelEnded = false;      // 本关结束（过关或玩家输）
    private bool resolving = false;       // 结算/弃牌演出中，锁操作
    private Coroutine battleRoutine;

    private void Start()
    {
        BattleView.Instance.Init();   // 初始化演出层（相机+阴影保底）
        EventBus.Subscribe<LevelClearedEvent>(OnLevelCleared);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<LevelClearedEvent>(OnLevelCleared);
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
        Time.timeScale = 1f;
        IsInBattle = true;
        levelEnded = false;
        resolving = false;
        endTurnRequested = false;
        PointsLeft = pointsPerTurn;
        BattleView.Instance.ClearGhosts();
        BattleView.Instance.ClearSelection();
        if (cfg.enemyConfig != null)
        {
            Debug.Log("[Battle] PrepareIntent 前");
            EnemyController.Instance.Initialize(cfg.enemyConfig);
            EnemyController.Instance.PrepareIntent();
            Debug.Log("[Battle] PrepareIntent 完成");
        }
        if (battleRoutine != null) StopCoroutine(battleRoutine);
        Debug.Log("[Battle] 启动 GameLoop");
        battleRoutine = StartCoroutine(GameLoop());
        Debug.Log("[Battle] GameLoop 已启动");
    }
    // 桌面点击统一入口
    public void HandleTableAction(TableActionType type)
    {
        if (CurrentPhase != TurnPhase.Play || resolving) return;  // 只在出牌阶段、且没在结算时响应

        switch (type)
        {
            case TableActionType.Play:
                TryResolveOneHand(false);
                break;
            case TableActionType.Discard:
                TryResolveOneHand(true);
                break;
            case TableActionType.EndTurn:
                AudioManager.Instance.PlayBell();
                endTurnRequested = true;
                break;
        }
    }

    // 尝试打出/弃掉当前选中的一手牌
    private void TryResolveOneHand(bool fold)
    {
        List<Card> cards = BattleView.Instance.GetSortedSelectedCards();
        if (cards.Count == 0)
        {
            Debug.Log(fold ? "[弃牌堆] 没选牌" : "[出牌堆] 没选牌");
            // TODO: 以后这里播"请先选牌"的提示/抖动
            return;
        }

        var dataList = cards.ConvertAll(c => c.Data);   // 1. 取出每张牌的数据
        var hand = PokerResolver.Evaluate(dataList);     // 2. 判断是什么牌型
        int cost = PokerResolver.GetPointCost(hand);     // 3. 算出点数消耗
        if (PointsLeft < cost)
        {
            Debug.Log($"点数不足：需要 {cost}，剩余 {PointsLeft}");
            // TODO: 以后这里让选中的牌抖动/变红，然后 ClearSelection
            return;
        }

        PointsLeft -= cost;
        isPlayfold = fold;
        StartCoroutine(ResolvePlayerHand());   // 立即结算，不离开出牌阶段
    }
    // 主循环
    private IEnumerator GameLoop()
    {
        while (!levelEnded && !GameManager.Instance.IsGameOver)
        {
            SetPhase(TurnPhase.Draw);
            GameProgress.transitioning = false;
            PointsLeft = pointsPerTurn;        // 每回合点数补满

            int need = GameProgress.targetHandSize - DeckManager.Instance.HandCards.Count;
            if (need > 0)
                yield return DeckManager.Instance.DrawCards(need);

            // 抽完再空等几帧，给飞牌动画留位置
            for (int i = 0; i < drawFrames; i++)
                yield return null;

            SetPhase(TurnPhase.Play);
            endTurnRequested = false;

            // 出牌阶段：想出几手出几手（受点数限制），点铃铛才离开
            while (!endTurnRequested)
            {
                if (levelEnded || GameManager.Instance.IsGameOver) break;
                yield return null;
            }
            if (levelEnded || GameManager.Instance.IsGameOver) break;
            BattleView.Instance.ClearSelection();    // 清残留选中（引用的是即将销毁的牌）
            BattleView.Instance.ClearGhosts();       // 清桌上虚影+阴影
            DeckManager.Instance.DiscardAllHand();   // 回合结束，手牌全弃
            SetPhase(TurnPhase.EnemyBattle);
            yield return StartCoroutine(ResolveEnemyTurn());
        }

        if (levelEnded && !GameManager.Instance.IsGameOver)
        {
            while (!BattleQueue.Instance.IsEmpty)
                yield return null;
            EventBus.Publish(new LevelClearedEvent());

        }
        IsInBattle = false;
    }

    // 编排选牌→计算→演出→游戏逻辑
    private IEnumerator ResolvePlayerHand()
    {
        List<Card> cards = BattleView.Instance.GetSortedSelectedCards();

        if (cards.Count == 0)
        {
            Debug.Log("[铃铛] 没选牌，先点选手牌");
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
        yield return BattleView.Instance.PlayResolveSequence(cards, type, baseChips, mult, damage, coreIndices, isPlayfold);

        // 4. 游戏逻辑：isPlayfold=false 出牌扣敌人筹码；isPlayfold=true 弃牌进主池
        if (!isPlayfold)
        {
            if (GameManager.Instance.EnemyChips < damage)
            {
                GameManager.Instance.EnemyLoseChips(GameManager.Instance.EnemyChips);
            }
            else
            {
                GameManager.Instance.EnemyLoseChips(damage);
            }
            
        }
        else
        {
            GameManager.Instance.DiscardToPot(damage);
        }

        // 5. 补手牌
        if (GameManager.Instance.EnemyChips > 0)
        {
            int need = GameProgress.targetHandSize - DeckManager.Instance.HandCards.Count;
            if (need > 0)
                yield return DeckManager.Instance.DrawCards(need);
        }
        resolving = false;
        GameProgress.transitioning = false;
    }

        // 敌人回合：意图出牌→演出→扣玩家筹码→补牌
    private IEnumerator ResolveEnemyTurn()
    {
        while (GameProgress.chipFly)
            yield return null;
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

        resolving = true;
        GameProgress.transitioning = true;   // 锁输入（InputLocked = mapOpen || transitioning）

        // Call/Raise：算伤害（纯牌型伤害 × 意图倍率）
        List<CardDataSO> datas = playData.cards;
        HandType type = PokerResolver.Evaluate(datas);
        int baseChips = PokerResolver.GetBaseChips(type);
        int mult = PokerResolver.GetMultiplier(type);
        HashSet<int> coreIndices = PokerResolver.GetCoreCardIndices(datas, type);
        int cardBonus = PokerResolver.CalcCardBonus(datas, coreIndices);
        int rawDamage = PokerResolver.CalcDamage(baseChips, cardBonus, mult);
        float atkMult = EnemyController.Instance.ConsumeNextAttackMultiplier();
        int preDamage = Mathf.RoundToInt(rawDamage * playData.damageMultiplier);   // 减半前
        int damage = Mathf.RoundToInt(preDamage * atkMult);                         // 减半后


        // 演出
        // yield return BattleView.Instance.PlayEnemyResolveSequence(playData, damage);
        yield return BattleView.Instance.PlayEnemyResolveSequence(playData, type, baseChips, mult, damage, coreIndices, atkMult,preDamage);

        // 游戏逻辑：敌人直接扣玩家筹码（无护盾），扣掉的进主池
        Debug.Log("[敌人攻击] 伤害:" + damage);
        GameManager.Instance.LoseChips(damage);

        // 敌人手牌更新 + 补牌 + 下回合意图
        EnemyController.Instance.CommitPlayedCards(playData);
        EnemyController.Instance.RefillHand();      //自动发布事件调用下面那个函数
        //BattleView.Instance.RefreshEnemyHand();      
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
    //----------卡牌技能效果-----------------
    
}
