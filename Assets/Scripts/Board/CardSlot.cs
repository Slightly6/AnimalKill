using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 棋盘槽位。没有碰撞体（拖拽用数学平面检测），只负责放卡。
/// 槽位本身躺平（Euler -90），卡作子物体自动躺平。
/// </summary>
public class CardSlot : MonoBehaviour
{
    [Header("属性")]
    public int laneIndex;
    public bool isPlayerSide;
    public int tableSortOrder;   // 排的渲染顺序（远1 / 中2 / 近3）

    public Card CurrentCard { get; private set; }
    public bool IsEmpty { get { return CurrentCard == null; } }

    void Awake()
    {
        // 加一个触发器碰撞体，用来接 OnMouseDown（点选牌后点槽出牌）。
        // 尺寸和卡牌在槽位上的足迹一致（CardDisplay 里卡的碰撞体也是这个尺寸）。
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.8f, 2.6f, 0.05f);
    }

    // 点槽出牌：有选中的牌时，把那张牌打到这个槽位；没选牌就什么都不做
    void OnMouseDown()
    {
        if (CardDisplay.selectedCard == null) return;   // 没选中牌，没反应

        if (!isPlayerSide) return;   // 只响应玩家自己的槽位
        if (!IsEmpty) return;        // 槽位已有牌，没反应

        CardDisplay.selectedCard.PlayToSlot(this);
    }

    public void PlaceCard(Card card)
    {
        CurrentCard = card;
        card.transform.SetParent(transform);
        card.transform.localPosition = Vector3.zero;
        card.transform.localRotation = Quaternion.identity;   // 继承槽位朝向，躺平
        card.transform.localScale = Vector3.one;

        // 按排错开渲染顺序，近排盖远排
        SortingGroup sg = card.GetComponent<SortingGroup>();
        if (sg != null) sg.sortingOrder = tableSortOrder;
    }

    public void RemoveCard()
    {
        CurrentCard = null;
    }
}
