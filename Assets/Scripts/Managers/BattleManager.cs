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
    private int pointBonusNextTurn = 0;  // 下回合额外点数挂账（AddCoin 手牌效果）

    // 手牌效果：下回合多 1 点（AddCoin 效果调）
    public void AddPointsNextTurn(int n)
    {
        pointBonusNextTurn += n;
    }

    // 道具卡效果：当前回合立即加点数
    public void AddPoints(int n)
    {
        PointsLeft += n;
        Debug.Log($"[道具] 点数 +{n}，当前 {PointsLeft}");
    }

    // 道具卡效果：立即抽 n 张（异步，不阻塞结算）
    public void DrawCardsNow(int count)
    {
        StartCoroutine(DeckManager.Instance.DrawCards(count));
    }

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
            return;
        }

        var dataList = cards.ConvertAll(c => c.Data);

        var normalDatas = new List<CardDataSO>();
        var itemCards = new List<Card>();
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Data != null && cards[i].Data.cardType == CardType.Item)
                itemCards.Add(cards[i]);
            else
                normalDatas.Add(cards[i].Data);
        }

        HandType hand = PokerResolver.Evaluate(normalDatas);
        int handCost = PokerResolver.GetPointCost(hand);
        if (normalDatas.Count == 0) handCost = 0;

        int itemCostSum = 0;
        foreach (var ic in itemCards)
            if (ic.Data != null) itemCostSum += Mathf.Max(0, ic.Data.itemCost);

        int totalCost = handCost + itemCostSum;

        if (PointsLeft < totalCost)
        {
            Debug.Log($"点数不足：需要 {totalCost}（牌型{handCost} + 道具{itemCostSum}），剩余 {PointsLeft}");
            return;
        }

        PointsLeft -= totalCost;
        isPlayfold = fold;
        StartCoroutine(ResolvePlayerHand());
    }
    // 主循环
    private IEnumerator GameLoop()
    {
        while (!levelEnded && !GameManager.Instance.IsGameOver)
        {
            SetPhase(TurnPhase.Draw);
            GameProgress.transitioning = false;
            PointsLeft = pointsPerTurn + pointBonusNextTurn;   // 每回合点数补满（含手牌挂账的额外点数）
            Debug.Log($"[回合开始] 点数补满 {pointsPerTurn} + 挂账{pointBonusNextTurn} = {PointsLeft}");
            pointBonusNextTurn = 0;

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
            

        // 1.5 触发道具卡效果（出牌/弃牌都触发） + 普通牌打出技能
        var itemCardsPlay = new List<Card>();
        for (int i = 0; i < cards.Count; i++)
            if (cards[i] != null && cards[i].Data != null && cards[i].Data.cardType == CardType.Item)
                itemCardsPlay.Add(cards[i]);

        foreach (var ic in itemCardsPlay)
        {
            if (ic.Data.itemEffect != null)
            {
                Debug.Log($"[道具卡] 触发 {ic.Data.itemEffect.GetType().Name}");
                ic.Data.itemEffect.Apply();
            }
        }

        if (!isPlayfold)
        {
            foreach (Card c in cards)
            {
                if (c != null && (c.Data == null || c.Data.cardType != CardType.Item))
                    c.TriggerAbility(AbilityTrigger.OnPlay, null);
            }
        }

        // 2. 纯计算（PokerResolver）：只对普通牌判牌型
        var normalCardsList = new List<Card>();
        foreach (var c in cards)
            if (c != null && c.Data != null && c.Data.cardType != CardType.Item)
                normalCardsList.Add(c);

        List<CardDataSO> normalDatas = normalCardsList.ConvertAll(c => c.Data);
        HandType type = normalDatas.Count > 0 ? PokerResolver.Evaluate(normalDatas) : HandType.HighCard;
        int damage = PokerResolver.GetHandDamage(type);

        var rawCoreIndices = normalDatas.Count > 0 ? PokerResolver.GetCoreCardIndices(normalDatas, type) : new HashSet<int>();
        HashSet<int> coreIndices = new HashSet<int>();
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && cards[i].Data != null && cards[i].Data.cardType != CardType.Item)
            {
                int normalIdx = normalCardsList.IndexOf(cards[i]);
                if (rawCoreIndices.Contains(normalIdx)) coreIndices.Add(i);
            }
        }

        // 3. 纯演出（BattleView）：飞牌+牌型板子+飞撞+销毁牌
        yield return BattleView.Instance.PlayResolveSequence(cards, type, damage, coreIndices, isPlayfold);

        // 4. 游戏逻辑：isPlayfold=false 出牌扣敌人筹码；isPlayfold=true 弃牌进主池
        if (!isPlayfold)
        {
            GameManager.Instance.EnemyLoseChips(Mathf.Min(damage, GameManager.Instance.EnemyChips));
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

        // Call/Raise：算伤害（牌型固定伤害 × 敌人技能倍率）
        List<CardDataSO> datas = playData.cards;
        HandType type = PokerResolver.Evaluate(datas);
        HashSet<int> coreIndices = PokerResolver.GetCoreCardIndices(datas, type);
        float atkMult = EnemyController.Instance.ConsumeNextAttackMultiplier();
        int preDamage = PokerResolver.GetHandDamage(type);                        // 减半前
        int damage = Mathf.Max(1, Mathf.RoundToInt(preDamage * atkMult));         // 减半后

        // 演出
        yield return BattleView.Instance.PlayEnemyResolveSequence(playData, type, damage, coreIndices, atkMult, preDamage);

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
