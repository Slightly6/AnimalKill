using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 总管理。管筹码、战利品、胜负。
/// 打脸 → 收牌凑德州牌型 → 玩家加牌型筹码 → 打光敌人筹码过关。
/// 敌人筹码归零 = 过关（非整局胜利），玩家筹码归零 = 整局失败。
/// </summary>
public class GameManager : Singleton<GameManager>
{
    private const int TROPHY_SIZE = 5;   // 战利品区容量

    [Header("开局筹码")]
    public int startingPlayerChips = 100;   // 玩家开局筹码数（在 Inspector 里填）

    [Header("开挂模式（勾上：无限筹码 / 地图全亮 / 可跳过此关）")]
    public bool cheatMode = false;

    // 玩家当前筹码（别的脚本只读，不要直接改）
    public int PlayerChips { get; private set; }

    // 敌人当前筹码
    public int EnemyChips { get; private set; }

    // 战利品区当前张数
    public int TrophyCount { get { return trophy.Count; } }

    // 取战利品区第 index 张牌（0~4，空位返回 null，给 UI 显示用）
    public CardDataSO GetTrophyCard(int index)
    {
        if (index < 0 || index >= trophy.Count) return null;
        return trophy[index];
    }

    public bool IsGameOver { get; private set; }

    // 战利品区：打脸收的牌，凑满 5 张结算
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

        GameProgress.cheatMode = cheatMode;  // 开挂开关同步到全局（跨场景）
    }

    private void Start()
    {
        IsGameOver = false;

        EventBus.Subscribe<CardDiedEvent>(OnCardDied);
        EventBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<CardDiedEvent>(OnCardDied);
        EventBus.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
    }

    // 卡死了
    private void OnCardDied(CardDiedEvent e)
    {
        if (IsGameOver) return;
        // 后续在这里做觉醒或掉落
    }

    // 玩家出牌：扣筹码（按牌点数，1~13，A=1）
    private void OnCardPlayed(CardPlayedEvent e)
    {
        if (IsGameOver) return;
        if (!e.isPlayerSide) return;

        int cost = (int)e.card.Data.rank;
        LoseChips(cost);
        Debug.Log("[出牌] " + e.card.CardName + " 消耗 " + cost + " 筹码");
    }

    // ========== 关卡 ==========

    // 进入一关：设敌人筹码、清空战利品区（MapManager 调用）
    public void LoadLevel(LevelConfig cfg)
    {
        EnemyChips = cfg.enemyStartingChips;
        trophy.Clear();

        SendChipsChanged();
        EventBus.Publish(new TrophyChangedEvent { count = 0 });
        Debug.Log("[关卡] 进入 " + cfg.levelName + "，敌人筹码 " + EnemyChips);
    }

    // 整局胜利（MapManager 打完 52 关后调用）
    public void WinGame()
    {
        EndGame(true);
    }

    // ========== 战利品 ==========

    // 打脸收牌：把出手牌的花色+点数存进战利品区，满 5 张结算
    public void AddTrophy(CardDataSO data)
    {
        if (IsGameOver) return;
        trophy.Add(data);

        EventBus.Publish(new TrophyChangedEvent { count = trophy.Count });
        Debug.Log("[战利品] 收牌 " + data.GetSuitSymbol() + data.GetRankText()
            + "（" + trophy.Count + "/" + TROPHY_SIZE + "）");

        if (trophy.Count >= TROPHY_SIZE)
        {
            SettleTrophy();
        }
    }

    // 凑满 5 张，判定牌型。暂时不加筹码（以后改成：按牌型给临时技能）
    private void SettleTrophy()
    {
        HandType type = PokerHandEvaluator.Evaluate(trophy);
        trophy.Clear();

        Debug.Log("[结算] 牌型=" + type + "（暂时不加筹码）");
    }

    // ========== 筹码 ==========

    // 玩家加筹码（凑德州赢的）
    public void AddChips(int amount)
    {
        if (IsGameOver) return;
        PlayerChips += amount;
        SendChipsChanged();
        CheckWin();
    }

    // 玩家扣筹码（出牌成本 + 敌人打脸）
    public void LoseChips(int amount)
    {
        if (IsGameOver) return;
        if (GameProgress.cheatMode) return;   // 开挂：玩家筹码不扣
        PlayerChips -= amount;
        SendChipsChanged();
        CheckWin();
    }

    // 敌人加筹码（敌人打脸赢的）
    public void EnemyAddChips(int amount)
    {
        if (IsGameOver) return;
        EnemyChips += amount;
        SendChipsChanged();
        CheckWin();
    }

    // 敌人扣筹码（被玩家打脸赢走）
    public void EnemyLoseChips(int amount)
    {
        if (IsGameOver) return;
        EnemyChips -= amount;
        SendChipsChanged();
        CheckWin();
    }

    // 筹码转移（打脸）：一次从一方转到另一方。toPlayer=true 敌→我，false 我→敌
    public void TransferChips(int amount, bool toPlayer)
    {
        if (IsGameOver) return;

        if (toPlayer)
        {
            EnemyChips -= amount;
            PlayerChips += amount;
        }
        else
        {
            if (!GameProgress.cheatMode) PlayerChips -= amount;   // 开挂：玩家筹码不扣
            EnemyChips += amount;
        }

        SendChipsChanged();   // 刷新数字 + 让筹码堆校正数量
        EventBus.Publish(new ChipTransferEvent { amount = amount, toPlayer = toPlayer });   // 播飞过去动画
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
            enemyChips = EnemyChips
        });
    }

    // 胜负：敌人筹码归零 = 过关；玩家筹码归零 = 整局失败
    private void CheckWin()
    {
        if (EnemyChips <= 0)
        {
            EnemyChips = 0;
            GameProgress.playerChips = PlayerChips;// 跨关继承玩家筹码
            EventBus.Publish(new LevelClearedEvent());   // 过关（是否胜利由 MapManager 判）
        }
        else if (PlayerChips <= 0)
        {
            PlayerChips = 0;
            EndGame(false);
        }
    }

    private void EndGame(bool playerWin)
    {
        IsGameOver = true;
        EventBus.Publish(new GameOverEvent { playerWin = playerWin });
        Debug.Log(playerWin ? "玩家胜利！" : "玩家失败！");
    }
}
