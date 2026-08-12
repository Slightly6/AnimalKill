using UnityEngine;

/// <summary>
/// 总管理。管双方血量、扣血、胜负。
/// 监听 CardDied 事件。
/// </summary>
public class GameManager : Singleton<GameManager>
{
    [Header("初始血量")]
    public int startingHP = 20;

    // 当前血量（别的脚本只读，不要直接改）
    public int PlayerHP { get; private set; }
    public int EnemyHP { get; private set; }
    public bool IsGameOver { get; private set; }

    private void Start()
    {
        PlayerHP = startingHP;
        EnemyHP = startingHP;
        IsGameOver = false;

        EventBus.Subscribe<CardDiedEvent>(OnCardDied);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<CardDiedEvent>(OnCardDied);
    }

    // 卡死了
    private void OnCardDied(CardDiedEvent e)
    {
        if (IsGameOver) return;
        // 后续在这里做死亡扣血或加筹码
    }

    // 扣玩家血
    public void DamagePlayer(int amount)
    {
        if (IsGameOver) return;
        PlayerHP = Mathf.Max(0, PlayerHP - amount);
        SendHPChanged();

        if (PlayerHP <= 0)
        {
            EndGame(false);
        }
    }

    // 扣敌人血
    public void DamageEnemy(int amount)
    {
        if (IsGameOver) return;
        EnemyHP = Mathf.Max(0, EnemyHP - amount);
        SendHPChanged();

        if (EnemyHP <= 0)
        {
            EndGame(true);
        }
    }

    private void SendHPChanged()
    {
        EventBus.Publish(new HealthChangedEvent
        {
            playerHP = PlayerHP,
            enemyHP = EnemyHP
        });
    }

    private void EndGame(bool playerWin)
    {
        IsGameOver = true;
        EventBus.Publish(new GameOverEvent { playerWin = playerWin });
        Debug.Log(playerWin ? "玩家胜利！" : "玩家失败！");
    }
}
