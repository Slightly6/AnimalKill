using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 总管理。管筹码、胜负。
/// 敌人筹码归零 = 过关（非整局胜利），玩家筹码归零 = 整局失败。
/// </summary>
public class GameManager : Singleton<GameManager>
{
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

    // 边池：【机制暂时关闭】数值恒为 0，不再开局发储备、不承接弃牌回血、篝火也不补它
    public int SidePotChips { get; private set; }

    [Header("双池参数")]
    public float returnRatio = 0.4f;      // Pot 回血比例
    public float sideRatio = 0.3f;        // Pot 进边池比例
    public int takeLimit = 200;           // 回血上限（超出留在 Pot）
    public int sidePotCap = 50;           // 边池上限
    public float discardPotRatio = 0.5f;  // 弃牌进 Pot 比例

    [Header("过关骰子奖励（按节点类型给固定值，主池不再换骰子）")]
    public int smallBlindDice = 3;   // 小盲注（Monster）
    public int bigBlindDice = 5;     // 大盲注（Elite）
    public int bossDice = 8;         // Boss

    private bool deathSequenceRunning = false;  // 玩家打光敌人筹码后，死亡流程正在进行中，防止重复触发
    public int toDice;                    // 本关过关奖励的骰子数（按节点类型算）

    public bool IsGameOver { get; private set; }

    protected override void Awake()
    {
        base.Awake();               // 让 Singleton 正确设 _instance

        // 第一关用 Inspector 开局筹码并记进进度；之后跨关继承上次剩的筹码
        if (!GameProgress.chipsInitialized)
        {
            GameProgress.playerChips = startingPlayerChips;
            GameProgress.playerChipsMax = startingPlayerChips;
            GameProgress.chipsInitialized = true;
        }
        // 同步上限：篝火升过的上限跨关保留
        startingPlayerChips = GameProgress.playerChipsMax;
        PlayerChips = GameProgress.playerChips;
        // 边池机制关闭：不再从存档恢复边池储备
        // SidePotChips = GameProgress.sidePotChips;

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

    // 进入一关：设敌人筹码（MapManager 调用）
    public void LoadLevel(LevelConfig cfg)
    {
        EnemyChips = cfg.enemyConfig != null ? cfg.enemyConfig.Chips : cfg.enemyStartingChips;

        // 边池机制关闭：进关不再发放 50 枚边池储备，也不从存档继承
        // SidePotChips = GameProgress.sidePotChips;
        PlayerChips = GameProgress.playerChips;
        // if (SidePotChips <= 0)
        //     SidePotChips = startingSidePot;
        PotChips = 0;       // 主池每关重置
        Coin.Instance.initialized=true;
        SendChipsChanged();
        Debug.Log("[关卡] 进入 " + cfg.levelName + "，敌人筹码 " + EnemyChips);
    }

    // 整局胜利（MapManager 打完 52 关后调用）
    public void WinGame()
    {
        EndGame(true);
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
        e.item.effect.Apply();   // 效果逻辑全在 AbilityEffectSO 子类里
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
        TrinketRuntime.NotifyPotGained(amount);   // 镶嵌物：存根/大账簿记账
        SendChipsChanged();
        CheckWin();
    }

    // 敌人扣筹码
    public void EnemyLoseChips(int amount)
    {
        if (IsGameOver) return;
        EnemyChips -= amount;
        PotChips += amount;                   // 敌人流出的筹码进主池
        TrinketRuntime.NotifyPotGained(amount);   // 镶嵌物：存根/大账簿记账
        SendChipsChanged();
        CheckWin();
    }

    // 镶嵌物：往主池加筹码（延期票/筹码签用）
    public void AddToPot(int amount)
    {
        if (IsGameOver || amount <= 0) return;
        PotChips += amount;
        TrinketRuntime.NotifyPotGained(amount);
        SendChipsChanged();
    }

    // 镶嵌物：从主池取筹码（当票/金库钥匙/黑蜡烛用），返回实际取到的数
    public int TakeFromPot(int amount)
    {
        if (IsGameOver || amount <= 0) return 0;
        int take = Mathf.Min(amount, PotChips);
        PotChips -= take;
        SendChipsChanged();
        return take;
    }

    // 偷取：敌人筹码直接转给玩家（不走主池，和"伤害→主池"区分开）
    // 按点数偷筹码的效果用它
    public void StealChips(int amount)
    {
        if (IsGameOver) return;
        int steal = Mathf.Min(amount, EnemyChips);   // 不能偷超过敌人现有的
        if (steal <= 0) return;
        EnemyChips -= steal;
        PlayerChips += steal;
        SendChipsChanged();
        CheckWin();
    }

    // 休息节点：只回复玩家筹码（边池机制关闭，不再补边池）；回满后再用 = 上限+5 并把这 5 点补满
    public void RestHeal(float ratio = 0.3f)
    {
        if (IsGameOver) return;
        int healChips = Mathf.RoundToInt(startingPlayerChips * ratio);
        // 边池机制关闭：篝火不再回复边池
        // int healSide = Mathf.RoundToInt(sidePotCap * ratio);
        PlayerChips = Mathf.Min(PlayerChips + healChips, startingPlayerChips);
        // SidePotChips = Mathf.Min(SidePotChips + healSide, sidePotCap);

        // 筹码加满了：上限+5，并把加的 5 也补满
        if (PlayerChips >= startingPlayerChips)
        {
            startingPlayerChips += 5;
            GameProgress.playerChipsMax = startingPlayerChips;
            PlayerChips = startingPlayerChips;
            Debug.Log($"[篝火] 筹码满！上限+5 → {startingPlayerChips}");
        }

        SendChipsChanged();
    }

    // 弃牌：【边池机制关闭】弃牌不再回血，保留空壳供 BattleManager 调用，以后重做再填
    public void DiscardToPot(int cardDamage)
    {
        // if (IsGameOver) return;
        // int heal = Mathf.RoundToInt(cardDamage * 0.8f);
        // // 边池不够时只能拿走边池里有的，不能扣成负数
        // heal = Mathf.Min(heal, SidePotChips);
        // if (heal <= 0) return;
        // SidePotChips -= heal;
        // PlayerChips += heal;
        // SendChipsChanged();
        // CheckWin();
        return;
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

    // 过关骰子奖励：按当前节点类型给固定值（小盲注 3 / 大盲注 5 / Boss 8，数值可在 Inspector 调）
    private int GetClearDiceReward()
    {
        switch (GameProgress.currentNodeType)
        {
            case MapNodeType.Elite: return bigBlindDice;
            case MapNodeType.Boss:  return bossDice;
            default:                return smallBlindDice;   // Monster = 小盲注
        }
    }

    // 关结束分账：主池清空（不再换骰子），过关骰子按节点类型另发
    private void SettlePot()
    {
        TrinketRuntime.FireLevelSettle();   // 镶嵌物：分红票等"关卡结算"效果在主池清零前触发
        GameProgress.dice += toDice;

        Debug.Log("[关结束分账] " + GameProgress.currentNodeType + " 过关 → 骰子+" + toDice + "，当前骰子 " + GameProgress.dice);

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
            Narrator.Hide();   // 敌人已死，收掉残留的意图气泡（"我想要弃牌"之类）
            BattleQueue.Instance.Until(() => !GameProgress.chipFly, 15f);
            BattleQueue.Instance.Run(DeathSequenceRoutine());   // 一个协程搞定后面所有
        }
        else if (PlayerChips <= 0)
        {
            PlayerChips = 0;
            PotChips = 0;
            // SidePotChips = 0;   // 边池机制关闭
            EndGame(false);
        }
    }
    private IEnumerator DeathSequenceRoutine()
    {
        // 1. 算过关骰子（按节点类型固定值）、结算
        toDice = GetClearDiceReward();
        SettlePot();
        GameProgress.playerChips = PlayerChips;
        // GameProgress.sidePotChips = SidePotChips;   // 边池机制关闭：不再跨关继承
        Debug.Log("[死亡流程] toDice = " + toDice);

        // 2. 启动 PlayCollect，等它真正跑完（嵌套 StartCoroutine）
        if (TOPhat.Instance != null)
            yield return TOPhat.Instance.StartCoroutine(TOPhat.Instance.PlayCollect(toDice));
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
     

    public void EnemyAddChips(int amount)
    {
        if (IsGameOver) return;
        EnemyChips += amount;
        SendChipsChanged();
        CheckWin();
    }
    private void EndGame(bool playerWin)
    {
        IsGameOver = true;
        EventBus.Publish(new GameOverEvent { playerWin = playerWin });
        Debug.Log(playerWin ? "玩家胜利！" : "玩家失败！");
    }
}
