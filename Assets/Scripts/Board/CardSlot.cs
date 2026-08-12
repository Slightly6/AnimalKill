using UnityEngine;

/// <summary>
/// 棋盘槽位。有碰撞体，能被射线检测到，直接放卡。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class CardSlot : MonoBehaviour
{
    [Header("属性")]
    public int laneIndex;
    public bool isPlayerSide;

    public Card CurrentCard { get; private set; }
    public bool IsEmpty { get { return CurrentCard == null; } }

    void Start()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    public void PlaceCard(Card card)
    {
        CurrentCard = card;
        card.transform.SetParent(transform);
        card.transform.localPosition = Vector3.zero;
        card.transform.localRotation = Quaternion.identity;
        card.transform.localScale = Vector3.one;   // 大小和槽位一致
    }

    public void RemoveCard()
    {
        CurrentCard = null;
    }
}
