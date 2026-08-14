using UnityEngine;
using TMPro;

/// <summary>
/// 筹码 + 战利品显示。挂在一个 TextMeshPro 上。
/// 订阅筹码/战利品事件，实时刷新文字。
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class ScoreDisplay : MonoBehaviour
{
    private TextMeshPro text;

    void Awake()
    {
        text = GetComponent<TextMeshPro>();
    }

    void Start()
    {
        EventBus.Subscribe<ChipsChangedEvent>(OnChipsChanged);
        EventBus.Subscribe<TrophyChangedEvent>(OnTrophyChanged);
        Refresh();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<ChipsChangedEvent>(OnChipsChanged);
        EventBus.Unsubscribe<TrophyChangedEvent>(OnTrophyChanged);
    }

    void OnChipsChanged(ChipsChangedEvent e) { Refresh(); }
    void OnTrophyChanged(TrophyChangedEvent e) { Refresh(); }

    void Refresh()
    {
        if (GameManager.Instance == null) return;
        text.text = "你 " + GameManager.Instance.PlayerChips
            + "    敌 " + GameManager.Instance.EnemyChips
            + "    战利品 " + GameManager.Instance.TrophyCount + "/5";
    }
}
