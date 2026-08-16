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
