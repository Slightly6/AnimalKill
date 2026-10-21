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
    private int drawBonusNextTurn = 0;  // 下回合额外抽牌挂账（道具卡效果）
    private int pendingDamageBonus = 0; // 本次出牌额外伤害（道具卡效果，用完清零）
    public int PendingDamageBonus => pendingDamageBonus;   // BattleView 逐张滚分用：读当前累积的镶嵌加成

    // 效果挂账（按 timing 分流）
    private List<AbilityEffectSO> delayedEffects = new(); // 延时：下回合开始时执行
    private List<AbilityEffectSO> waveEffects = new();   // 波次：下一波出牌结算后执行

    public void QueueDelayed(AbilityEffectSO effect)
    {
        delayedEffects.Add(effect);
        Debug.Log($"[延时挂账] {effect.GetType().Name}");
    }

    public void QueueWave(AbilityEffectSO effect)
    {
        waveEffects.Add(effect);
        Debug.Log($"[波次挂账] {effect.GetType().Name}");
    }

    // 下回合开始时执行所有延时效果
    private void FlushDelayedEffects()
    {
        if (delayedEffects.Count == 0) return;
        Debug.Log($"[延时触发] 执行 {delayedEffects.Count} 个延时效果");
        foreach (var e in delayedEffects)
        {
            Debug.Log($"  → {e.GetType().Name}");
            e.Apply();
        }
        delayedEffects.Clear();
    }

    // 下一波出牌结算后执行所有波次效果
    private void FlushWaveEffects()
    {
        if (waveEffects.Count == 0) return;
        Debug.Log($"[波次触发] 执行 {waveEffects.Count} 个波次效果");
        foreach (var e in waveEffects)
        {
            Debug.Log($"  → {e.GetType().Name}");
            e.Apply();
        }
        waveEffects.Clear();
    }

    // 手牌效果：下回合多 1 点（AddCoin 效果调）
    public void AddPointsNextTurn(int n)
    {
        pointBonusNextTurn += n;
    }

    // 道具卡效果：下回合多抽 n 张
    public void AddDrawNextTurn(int n)
    {
        drawBonusNextTurn += n;
        Debug.Log($"[道具] 下回合多抽 {n} 张");
    }

    // ---- N 回合后多抽牌的挂账（卡牌强化"点数个回合后"用）----
    private class PendingDraw
    {
        public int turnsLeft;   // 还剩几回合到账
        public int amount;      // 到账时多抽几张
    }
    private List<PendingDraw> pendingDraws = new();

    // 挂账：turns 个回合后多抽 amount 张
    public void AddDrawAfterTurns(int turns, int amount)
    {
        pendingDraws.Add(new PendingDraw { turnsLeft = turns, amount = amount });
        Debug.Log($"[强化挂账] {turns} 回合后多抽 {amount} 张");
    }

    // 每回合开始倒计时：到 0 的当回合加抽（必须跑在 need += drawBonusNextTurn 之前）
    private void TickPendingDraws()
    {
        for (int i = pendingDraws.Count - 1; i >= 0; i--)
        {
            pendingDraws[i].turnsLeft--;
            if (pendingDraws[i].turnsLeft <= 0)
            {
                Debug.Log($"[强化到账] 多抽 {pendingDraws[i].amount} 张");
                drawBonusNextTurn += pendingDraws[i].amount;
                pendingDraws.RemoveAt(i);
            }
        }
    }

    // 道具卡效果：本次出牌多 n 点伤害
    public void AddDamageBonus(int n)
    {
        pendingDamageBonus += n;
        Debug.Log($"[道具] 本次伤害 +{n}");
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
        pendingDraws.Clear();   // 跨关清账：上关没到账的"N 回合后多抽"不带进下一关
        TrinketRuntime.ResetForBattle();   // 镶嵌物：本场战斗状态全部清零
        BattleView.Instance.ClearGhosts();
        BattleView.Instance.ClearSelection();
        if (BattleView.Instance.scoreBoard != null)
            BattleView.Instance.scoreBoard.ResetBoard();   // 每关开始计分板归零
        if (cfg.enemyConfig != null)
        {
            Debug.Log("[Battle] PrepareIntent 前");
            EnemyController.Instance.Initialize(cfg.enemyConfig);
            if (BlindTokenBoard.Instance != null) BlindTokenBoard.Instance.Show(cfg.enemyConfig);   // 敌人这关抽到的机制摆上桌
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

        // 拟态牌：打出时随机变成手里一张（点数/花色/技能全按新身份结算）
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null && cards[i].IsMimic)
                cards[i].MimicRandomly(DeckManager.Instance.HandCards);
        }

        // 道具牌只能单张单独打出/弃掉，不能和其他牌混
        bool hasItem = false;
        foreach (var c in cards)
            if (c != null && c.Data != null && c.Data.cardType == CardType.Item) { hasItem = true; break; }
        if (hasItem && cards.Count > 1)
        {
            Debug.Log("[道具] 道具牌只能单独使用");
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
        if (!fold) TrinketRuntime.BeginHand(cards, totalCost, hand);   // 镶嵌物上下文：第几手/耗点/牌型/筹码签挂账
        StartCoroutine(ResolvePlayerHand());
    }
    // 主循环
    private IEnumerator GameLoop()
    {
        while (!levelEnded && !GameManager.Instance.IsGameOver)
        {
            SetPhase(TurnPhase.Draw);
            GameManager.Instance.BeginPlayerTurn();   // 蓝码：归档受伤旗标 + 清零（跑在敌方伤害结算之后）
            GameProgress.transitioning = false;
            TrinketRuntime.ResetForTurn();   // 镶嵌物：回合状态清零 + 当票还款 + 钩子回手 + 吸铁石吸牌
            // 延时效果在补牌恢复点数之前执行
            FlushDelayedEffects();
            TickPendingDraws();   // N 回合后抽牌的挂账倒计时（到 0 当回合生效）
            PointsLeft = Mathf.Max(0, pointsPerTurn + pointBonusNextTurn);   // 每回合点数补满（含手牌挂账；高利贷可挂负数，保底 0）
            Debug.Log($"[回合开始] 点数补满 {pointsPerTurn} + 挂账{pointBonusNextTurn} = {PointsLeft}");
            pointBonusNextTurn = 0;

            int need = GameProgress.targetHandSize - DeckManager.Instance.HandCards.Count;
            need += drawBonusNextTurn;
            drawBonusNextTurn = 0;
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
            TrinketRuntime.FireTurnEndHeld();        // 镶嵌物：攥在手里没打出的效果（延期票/秒针/保险箱）
            SetPhase(TurnPhase.EnemyBattle);         // 先切阶段再清手：牌飞回途中 CanPlay 已不通过，点不了
            yield return DeckManager.Instance.DiscardAllHandFly();   // 回合结束，手牌数据全弃，实体飞回弃牌堆销毁
            yield return StartCoroutine(ResolveEnemyTurn());
        }

        if (levelEnded && !GameManager.Instance.IsGameOver)
        {
            while (!BattleQueue.Instance.IsEmpty)
                yield return null;
           

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

        // 1. 清选择 + 移手牌列表（批量窗口：一次出牌动作不管几张只算 1 次手牌变动）
        BattleView.Instance.ClearSelection();
        TrinketRuntime.BeginHandChangeBatch();
        foreach (Card c in cards)
        {
            if (c != null) DeckManager.Instance.RemoveFromHand(c);
        }
        TrinketRuntime.EndHandChangeBatch();
            

        // 1.5 纯计算（PokerResolver）：先判牌型+计分牌（无副作用），效果触发要用
        var normalCardsList = new List<Card>();
        foreach (var c in cards)
            if (c != null && c.Data != null && c.Data.cardType != CardType.Item)
                normalCardsList.Add(c);

        List<CardDataSO> normalDatas = normalCardsList.ConvertAll(c => c.Data);
        HandType type = normalDatas.Count > 0 ? PokerResolver.Evaluate(normalDatas) : HandType.HighCard;

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

        // 灌铅骰：本手有任何一张镶了它，所有牌都算计分牌（散牌位的镶嵌也会触发）
        bool loadedDie = false;
        foreach (var c in cards)
        {
            if (c == null || c.Data == null || c.Data.effects == null) continue;
            foreach (var e in c.Data.effects)
                if (e is LoadedDieTrinket && !TrinketRuntime.IsDisabled(c.Data, e)) { loadedDie = true; break; }
            if (loadedDie) break;
        }
        if (loadedDie)
        {
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null && cards[i].Data != null && cards[i].Data.cardType != CardType.Item)
                    coreIndices.Add(i);
            Debug.Log("[灌铅骰] 本手所有牌都计分");
        }

        // 全垒打：宿主是计分牌才立旗——本手其他牌的随机伤害翻倍（霰弹枪用）
        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null || c.Data == null || c.Data.effects == null || !coreIndices.Contains(i)) continue;
            foreach (var e in c.Data.effects)
                if (e is HomeRunTrinket && !TrinketRuntime.IsDisabled(c.Data, e))
                {
                    TrinketRuntime.SetRandomDouble(c.Data);
                    Debug.Log("[全垒打] 本手其他牌的随机伤害翻倍");
                    break;
                }
        }

        // 小丑战斗钩子：演出开始前结算，加成进 pendingDamageBonus 跟着本手一起滚分
        if (!isPlayfold && JokerManager.Instance != null)
            JokerManager.Instance.NotifyHandPlayed();

        // 1.6 散牌位/道具卡的效果先发掉（不在计分位，不走演出）；计分牌的效果挪进演出循环，
        // 每弹一张发一张（BattleView.ShowHandBoard 里调 ApplyEffects），加血/加盾/挂账逐张入账
        if (!isPlayfold)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                Card c = cards[i];
                if (c != null && c.Data != null && c.Data.effects != null && c.Data.effects.Count > 0 && !coreIndices.Contains(i))
                    c.ApplyEffects(false);   // 散牌位：只有没勾 scoreOnly 的效果会发
            }

            // 道具牌使用：打出=使用，触发 ItemEffectSO.Use()（之前效果从未被调用，这就是道具效果拖了不生效的根因）
            for (int i = 0; i < cards.Count; i++)
            {
                Card c = cards[i];
                if (c == null || c.Data == null || c.Data.cardType != CardType.Item || c.Data.itemEffects == null) continue;
                foreach (var e in c.Data.itemEffects)
                    if (e != null) { e.Use(); Debug.Log("[道具] 使用 " + c.Data.name + " → " + e.effectName); }
            }
        }
        else   // 弃牌：触发"弃置时"效果（OnDiscard），打出效果不走
        {
            foreach (Card c in cards)
            {
                if (c == null || c.Data == null || c.Data.effects == null) continue;
                foreach (var e in c.Data.effects)
                    if (e != null) e.OnDiscard((int)c.Data.rank);
            }
        }

        // 2. 底分 = 牌型固定伤害；镶嵌加成在演出里逐张累积进 pendingDamageBonus（先清零，演完再读）
        int baseDamage = PokerResolver.GetHandDamage(type);
        pendingDamageBonus = 0;

        // 3. 纯演出（BattleView）：飞牌 + 计分板亮底分 + 逐张弹牌发效果滚分 + 销毁牌
        yield return BattleView.Instance.PlayResolveSequence(cards, type, baseDamage, coreIndices, isPlayfold);

        // 4. 总伤害 = 底分 + 演出里累积的镶嵌加成，结算完一块打过去：出牌扣敌人筹码；弃牌进主池
        int damage = Mathf.Max(0, baseDamage + pendingDamageBonus);   // 保底 0：蛇眼等效果可能挂负加成
        if (TrinketRuntime.ConsumeDoubleThisHand()) damage *= 2;   // 筹码签/大邪典：总得分翻倍
        pendingDamageBonus = 0;
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

        // 6. 被动机制：监听玩家出牌结果（Trap 设套回血、Showdown 记牌型）
        EnemyController ec2 = EnemyController.Instance;
        if (ec2 != null && ec2.Config != null && ec2.Config.passives != null)
        {
            foreach (var p in ec2.Config.passives)
                p.OnPlayerHandResolved(ec2, type);
        }

        // 7. 波次效果：本波结算后触发
        FlushWaveEffects();

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

        // Check/Fold：不出牌不攻击（Check 加筹码，Fold 标记回血）
        if (playData.intent == EnemyIntentType.Check || playData.intent == EnemyIntentType.Fold)
        {
            Debug.Log("[敌人] " + playData.intent + "，跳过攻击");
            Narrator.Hide();   // 敌人行动了，意图气泡收掉
            EnemyController.Instance.ExecuteSelectedBehavior();  // 触发意图独有效果
            yield return new WaitForSeconds(0.4f);
            EnemyController.Instance.PrepareIntent();
            resolving = false;
            GameProgress.transitioning = false;
            yield break;
        }

        resolving = true;
        GameProgress.transitioning = true;   // 锁输入（InputLocked = mapOpen || transitioning）

        // Call/Raise：算伤害（牌型固定伤害 × 特殊效果倍率；Raise 的额外伤害走 Apply）
        List<CardDataSO> datas = playData.cards;
        HandType type = PokerResolver.Evaluate(datas);
        HashSet<int> coreIndices = PokerResolver.GetCoreCardIndices(datas, type);
        float atkMult = EnemyController.Instance.ConsumeNextAttackMultiplier();
        int preDamage = PokerResolver.GetHandDamage(type);                         
        int damage = Mathf.Max(1, Mathf.RoundToInt(preDamage * atkMult));     

        // 被动机制 + 当前意图的伤害修正钩子
        EnemyController ec = EnemyController.Instance;
        if (ec.SelectedBehavior != null)
            damage = ec.SelectedBehavior.ModifyDamage(ec, damage);
        if (ec.Config != null && ec.Config.passives != null)
        {
            foreach (var p in ec.Config.passives)
                damage = p.ModifyDamage(ec, damage);
        }
        damage = Mathf.Max(1, damage);

        // 扑克脸：本次攻击被压成固定值（优先级最高，盖过所有修正）
        int flat = EnemyController.Instance.ConsumeNextAttackFlat();
        if (flat >= 0) damage = flat;

        // 演出
        Narrator.Hide();   // 敌人出牌了，意图气泡收掉
        yield return BattleView.Instance.PlayEnemyResolveSequence(playData, type, damage, coreIndices, atkMult, preDamage);

        // 游戏逻辑：敌方伤害先扣蓝码（临时护盾），打穿了才扣红码，红码流出进主池
        Debug.Log("[敌人攻击] 伤害:" + damage);
        GameManager.Instance.TakeEnemyDamage(damage);
        ec.MarkAttackDone();   // 记一发攻击：抢先（Rush）等"第一次攻击"类被动读它

        // 触发意图独有效果（Call/Raise 默认空，留作演出钩子）
        EnemyController.Instance.ExecuteSelectedBehavior();

        // 被动机制：敌人攻击后触发（Steal 偷牌等）
        if (ec.Config != null && ec.Config.passives != null)
        {
            foreach (var p in ec.Config.passives)
                p.OnEnemyAttack(ec);
        }

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
