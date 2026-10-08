using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 总管理。管筹码、钩子挂牌、胜负。
/// 玩家从手牌选 1~5 张挂上钩子 → 按牌型获得本关增益（每关限一次）→ 打光敌人筹码过关。
/// 敌人筹码归零 = 过关（非整局胜利），玩家筹码归零 = 整局失败。
/// </summary>
public class GameManager : Singleton<GameManager>
{
    private const int TROPHY_SIZE = 5;   // 战利品区容量

    [Header("开局筹码")]
    public int startingPlayerChips = 100;   // 玩家开局筹码数（在 Inspector 里填）
    public int startingSidePot = 50;        // 边池开局筹码数（在 Inspector 里填）

    [Header("开挂模式（勾上：无限筹码 / 地图全亮 / 可跳过此关）")]
    public bool cheatMode = false;

    // 玩家当前筹码（别的脚本只读，不要直接改）
    public int PlayerChips { get; private set; }
    
    // 敌人当前筹码
    public int EnemyChips { get; private set; }

    // ========== 双池系统 ==========

    // 主池：双方伤害+弃牌都进这里，关结束分账
    public int PotChips { get; private set; }

    // 边池：关结束 Pot 分账的一部分，下关开局当血量
    public int SidePotChips { get; private set; }

    [Header("双池参数")]
    public float returnRatio = 0.4f;      // Pot 回血比例
    public float sideRatio = 0.3f;        // Pot 进边池比例
    public int takeLimit = 200;           // 回血上限（超出留在 Pot）
    public int sidePotCap = 50;           // 边池上限
    public float discardPotRatio = 0.5f;  // 弃牌进 Pot 比例
    private bool deathSequenceRunning = false;  // 玩家打光敌人筹码后，死亡流程正在进行中，防止重复触发
    public int toGold;                    // 本关结算时主池换金币数

    // 战利品区当前张数
    public int TrophyCount { get { return trophy.Count; } }

    // 取战利品区第 index 张牌（0~4，空位返回 null，给 UI 显示用）
    public CardDataSO GetTrophyCard(int index)
    {
        if (index < 0 || index >= trophy.Count) return null;
        return trophy[index];
    }

    // 钩子上全部牌的拷贝（技能判定牌型用）
    public List<CardDataSO> GetTrophyCards()
    {
        return new List<CardDataSO>(trophy);
    }

    public bool IsGameOver { get; private set; }

    // 本关钩子是否已用过：挂牌一次后锁到过关，下一关 LoadLevel 重置
    public bool HookLocked { get; private set; }

    // 钩子上当前挂的牌：玩家从手牌里选 1~5 张挂上来，挂一次后本关锁定，过关清空
    private List<CardDataSO> trophy = new List<CardDataSO>();

    protected override void Awake()
    {
        base.Awake();               // 让 Singleton 正确设 _instance

        // 第一关用 Inspector 开局筹码并记进进度；之后跨关继承上次剩的筹码
        if (!GameProgress.chipsInitialized)
        {
            GameProgress.playerChips = startingPlayerChips;
            GameProgress.chipsInitialized = true;
        }
        PlayerChips = GameProgress.playerChips;
        SidePotChips = GameProgress.sidePotChips;

        GameProgress.cheatMode = cheatMode;  // 开挂开关同步到全局（跨场景）
    }

    private void Start()
    {
        IsGameOver = false;

        EventBus.Subscribe<ItemActivatedEvent>(OnItemActivated);   // 听关卡内点击道具
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<ItemActivatedEvent>(OnItemActivated);
    }

    // ========== 关卡 ==========

    // 进入一关：设敌人筹码、清空钩子、解锁挂牌（MapManager 调用）
    public void LoadLevel(LevelConfig cfg)
    {
        EnemyChips = cfg.enemyStartingChips;

        // 上关 SettlePot 累加的边池保留到本关，本关可弃牌回血
        // 第一关 SidePotChips 还是 0，下面赋初始值
        SidePotChips = GameProgress.sidePotChips;
        PlayerChips = GameProgress.playerChips;
        if (SidePotChips <= 0)
            SidePotChips = startingSidePot;
        PotChips = 0;       // 主池每关重置
        trophy.Clear();
        HookLocked = false;
        Coin.Instance.initialized=true;
        SendChipsChanged();
        EventBus.Publish(new TrophyChangedEvent { count = 0 });
        Debug.Log("[关卡] 进入 " + cfg.levelName + "，敌人筹码 " + EnemyChips + "，边池 " + SidePotChips);
    }

    // 整局胜利（MapManager 打完 52 关后调用）
    public void WinGame()
    {
        EndGame(true);
    }

    // ========== 钩子挂牌 ==========

    // 玩家把选中的手牌挂到钩子上：1~5 张，判定牌型 → 应用本关增益 → 锁定。
    // 返回牌型；未挂成（已锁定/张数非法）返回 null。
    public HandType? HangCards(List<CardDataSO> cards)
    {
        if (IsGameOver) return null;
        if (HookLocked)
        {
            Debug.Log("[钩子] 本关已经挂过牌了");
            return null;
        }
        if (cards == null || cards.Count == 0 || cards.Count > TROPHY_SIZE)
        {
            Debug.Log("[钩子] 只能挂 1~" + TROPHY_SIZE + " 张牌");
            return null;
        }

        trophy = new List<CardDataSO>(cards);
        HookLocked = true;

        HandType type = PokerHandEvaluator.Evaluate(trophy);
        ApplyHandSkill(type);

        EventBus.Publish(new TrophyChangedEvent { count = trophy.Count });
        Debug.Log("[钩子] 挂上 " + trophy.Count + " 张，牌型=" + type + "，本关增益已生效");
        return type;
    }

    // 按牌型给本关增益（效果以后再调，先留入口）
    void ApplyHandSkill(HandType type)
    {
        // if (type == HandType.HighCard)            AddChips(2);
        // else if (type == HandType.OnePair)        AddChips(5);
        // else if (type == HandType.TwoPair)        GameProgress.hides += 1;
        // else if (type == HandType.ThreeOfAKind)   BuffPlayerCards(1);
        // else if (type == HandType.Straight)       BuffEnemyCards(-1);
        // else if (type == HandType.Flush)          AddChips(12);
        // else if (type == HandType.FullHouse)      { AddChips(20); GameProgress.hides += 1; }
        // else if (type == HandType.FourOfAKind)    BuffPlayerCards(2);
        // else if (type == HandType.StraightFlush)  { AddChips(30); BuffPlayerCards(1); 
        // TODO: 后期在这里改各牌型的本关持续增益
    }

    // 本关我方所有牌 +delta 战力（槽位机制删除后场上无持续牌，暂不生效；
    // 道具/牌型增益后续挂到新玩法的伤害结算上）
    public void BuffPlayerCards(int delta)
    {
        Debug.Log("[增益] 我方 +" + delta + " 战力（槽位已删除，暂不生效）");
    }

    // 本关敌方所有牌 +delta 战力（同上，暂不生效）
    public void BuffEnemyCards(int delta)
    {
        Debug.Log("[增益] 敌方 +" + delta + " 战力（槽位已删除，暂不生效）");
    }

    // ========== 商店道具 ==========

    // 关卡内点击道具触发（TableItem 发的 ItemActivatedEvent）
    void OnItemActivated(ItemActivatedEvent e)
    {
        if (IsGameOver) return;
        if (e.item == null || e.item.effect == null) return;
        e.item.effect.Apply();   // 效果逻辑全在 ItemEffectSO 子类里
    }

    // ========== 筹码 ==========

    // 玩家加筹码
    public void AddChips(int amount)
    {
        if (IsGameOver) return;
        PlayerChips += amount;
        SendChipsChanged();
        CheckWin();
    }

    // 玩家扣筹码
    public void LoseChips(int amount)
    {
        if (IsGameOver) return;
        if (GameProgress.cheatMode) return;   // 开挂：玩家筹码不扣
        PlayerChips -= amount;
        PotChips += amount;                   // 玩家流出的筹码进主池
        SendChipsChanged();
        CheckWin();
    }

    // 敌人扣筹码
    public void EnemyLoseChips(int amount)
    {
        if (IsGameOver) return;
        EnemyChips -= amount;
        PotChips += amount;                   // 敌人流出的筹码进主池
        SendChipsChanged();
        CheckWin();
    }

    // 弃牌：暂时直接回血（牌型伤害 × 0.8）
    public void DiscardToPot(int cardDamage)
    {
        if (IsGameOver) return;
        int heal = Mathf.RoundToInt(cardDamage * 0.8f);
        // 边池不够时只能拿走边池里有的，不能扣成负数
        heal = Mathf.Min(heal, SidePotChips);
        if (heal <= 0) return;
        SidePotChips -= heal;
        PlayerChips += heal;
        SendChipsChanged();
        CheckWin();
    }
    // ========== 开挂模式 ==========

    // 强制跳过当前关（开挂模式用）
    public void SkipLevel()
    {
        EventBus.Publish(new LevelClearedEvent());
    }

    // 开挂时屏幕左上角画一个「跳过此关」按钮
    void OnGUI()
    {
        if (!GameProgress.cheatMode) return;
        if (GUI.Button(new Rect(12, 12, 120, 42), "跳过此关"))
        {
            SkipLevel();
        }
    }

    private void SendChipsChanged()
    {
        EventBus.Publish(new ChipsChangedEvent
        {
            playerChips = PlayerChips,
            enemyChips = EnemyChips,
            potChips = PotChips,
            sidePotChips = SidePotChips
        });
    }

    // 关结束分账：主池筹码全部清空，按 1/10 汇率转金币（10 筹码 = 1 金币，余数舍掉）
    // 注意：边池不再有进账，只剩开局值+跨关剩余；回血渠道后期由商店金币消费承接
    private void SettlePot()
    {
        if (PotChips <= 0) return;

        GameProgress.gold += toGold;

        Debug.Log("[关结束分账] 主池 " + PotChips + " → 金币+" + toGold + "，当前金币 " + GameProgress.gold);

        PotChips = 0;
        SendChipsChanged();   // 主池清零，刷新 PotArea 显示
    }

    // 胜负：敌人筹码归零 = 过关；玩家筹码归零 = 整局失败
    private void CheckWin()
    {
        if (EnemyChips <= 0)
        {
            EnemyChips = 0;
            if (deathSequenceRunning) return;
            deathSequenceRunning = true;
            BattleQueue.Instance.Until(() => !GameProgress.chipFly, 15f);
            BattleQueue.Instance.Run(DeathSequenceRoutine());   // 一个协程搞定后面所有
        }
        else if (PlayerChips <= 0)
        {
            PlayerChips = 0;
            PotChips = 0;
            SidePotChips = 0;
            EndGame(false);
        }
    }
    private IEnumerator DeathSequenceRoutine()
    {
        // 1. 算金币、结算（此时前面的队列项都完成了，toGold 是新值）
        toGold = PotChips / 10;
        SettlePot();
        GameProgress.playerChips = PlayerChips;
        GameProgress.sidePotChips = SidePotChips;
        Debug.Log("[死亡流程] toGold = " + toGold);

        // 2. 启动 PlayCollect，等它真正跑完（嵌套 StartCoroutine）
        if (TOPhat.Instance != null)
            yield return TOPhat.Instance.StartCoroutine(TOPhat.Instance.PlayCollect(toGold));
            //传入队列瞬间是原来的数，只有一帧，变的时候我还是传进来原来的数，我进来的时候就已经进队列了

        // 3. 额外保险：等所有筛子销毁
        float timeout = 0f;
        while (GameObject.FindGameObjectsWithTag("Dice").Length > 0 && timeout < 5f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        // 4. 发过关事件，进地图
        deathSequenceRunning = false;
        EventBus.Publish(new LevelClearedEvent());
    }
     

    // private IEnumerator EnemyDeathSequence()
    // {
    //     if (deathSequenceRunning) yield break;   // 防止重复触发
    //     deathSequenceRunning = true;

    //     // 1. 先分账：主池换金币（SettlePot 内部会 PotChips=0）
    //     int gold = PotChips / 10;
    //     SettlePot();
    //     GameProgress.playerChips = PlayerChips;
    //     GameProgress.sidePotChips = SidePotChips;

    //     // 2. 广播死亡事件，帽子开始接金币演出
    //     EventBus.Publish(new EnemyDefeatedEvent { goldReward = gold });

    //     // 3. 等帽子演出结束（帽子把 IsBusy 置 false）；5 秒超时兜底，防止帽子没挂卡死流程
    //     float timer = 0f;
    //     while (TOPhat.Instance != null && TOPhat.Instance.IsBusy && timer < 5f)
    //     {
    //         timer += Time.deltaTime;
    //         yield return null;
    //     }

    //     // 4. 演出看完，正式过关（→ 清场 → 铺地图）
    //     deathSequenceRunning = false;
    //     EventBus.Publish(new LevelClearedEvent());
    // }
    private void EndGame(bool playerWin)
    {
        IsGameOver = true;
        EventBus.Publish(new GameOverEvent { playerWin = playerWin });
        Debug.Log(playerWin ? "玩家胜利！" : "玩家失败！");
    }
}
