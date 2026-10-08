using UnityEngine;
using TMPro;

/// <summary>
/// 战利品区（凑德州的 5 张牌）。挂在一个空物体上（Canvas 内）。
/// 5 个牌位，收一张显示一张（花色+点数，如 ♠A），空位留空。
/// </summary>
public class TrophyDisplay : MonoBehaviour
{
    public TextMeshProUGUI[] slotTexts = new TextMeshProUGUI[5];   // 5个牌位文字，在 Inspector 里拖

    void Start()
    {
        EventBus.Subscribe<TrophyChangedEvent>(OnTrophyChanged);
        Refresh();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<TrophyChangedEvent>(OnTrophyChanged);
    }

    void OnTrophyChanged(TrophyChangedEvent e) { Refresh(); }

    void Refresh()
    {
        if (GameManager.Instance == null) return;
        for (int i = 0; i < slotTexts.Length; i++)
        {
            if (slotTexts[i] == null) continue;
            CardDataSO card = GameManager.Instance.GetTrophyCard(i);
            if (card != null)
                slotTexts[i].text = card.GetSuitSymbol() + card.GetRankText();
            else
                slotTexts[i].text = "";   // 空位留空
        }
    }
}
