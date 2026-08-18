using UnityEngine;
using TMPro;

/// <summary>
/// 单个筹码显示。挂在一个物体上（Canvas 内），只刷新一个筹码数字。
/// 玩家筹码和敌人筹码各挂一个实例，分开摆到不同位置。
/// isPlayer = true → 显示玩家筹码；false → 显示敌人筹码。
/// </summary>
public class ScoreDisplay : MonoBehaviour
{
    public bool isPlayer = true;          // 勾上=玩家筹码，不勾=敌人筹码
    public TextMeshPro text;          // 要刷新的那个数字

    void Start()
    {
        EventBus.Subscribe<ChipsChangedEvent>(OnChipsChanged);
        Refresh();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<ChipsChangedEvent>(OnChipsChanged);
    }

    void OnChipsChanged(ChipsChangedEvent e) { Refresh(); }

    void Refresh()
    {
        if (GameManager.Instance == null || text == null) return;
        int chips = isPlayer ? GameManager.Instance.PlayerChips : GameManager.Instance.EnemyChips;
        text.text = chips.ToString();
    }
}
