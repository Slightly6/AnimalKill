using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 卡牌拖拽（2.5D 俯视桌面版）。
/// 用数学平面（y=0 的桌面）做射线，不依赖槽位碰撞体。
/// </summary>
[RequireComponent(typeof(Card))]
[RequireComponent(typeof(BoxCollider))]
public class CardDisplay : MonoBehaviour
{
    private Card card;
    private Camera mainCam;

    // 拖前状态（弹回用）
    private Vector3 originalPos;
    private Quaternion originalRot;
    private Transform originalParent;
    private int originalSortOrder;

    // 桌面平面（y=0），拖拽时射线打它
    private Plane tablePlane;

    // 当前正在拖拽的卡（全局标记，让 HandManager 别抢）
    public static CardDisplay draggingCard;

    void Awake()
    {
        card = GetComponent<Card>();
        mainCam = Camera.main;

        // 确保有 3D 碰撞体（OnMouseDown 靠它）
        BoxCollider col = GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.8f, 2.6f, 0.05f);

        tablePlane = new Plane(Vector3.up, Vector3.zero);
    }

    void Update()
    {
        // 保底：鼠标松开了但 OnMouseUp 没触发（移出屏幕/失焦），手动结束拖拽
        if (draggingCard == this && Input.GetMouseButtonUp(0))
        {
            EndDrag();
        }
    }

    // ========== 鼠标拖拽 ==========

    void OnMouseDown()
    {
        if (!enabled) return;

        // 只有玩家自己的卡能拖
        if (card.IsPlayer == false) return;

        // 检查能不能出牌
        if (BattleManager.Instance != null)
        {
            if (!BattleManager.Instance.IsPlayerTurn) return;
            if (BattleManager.Instance.CurrentPhase != TurnPhase.Play) return;
        }

        // 记住原位
        originalPos = transform.position;
        originalRot = transform.rotation;
        originalParent = transform.parent;

        SortingGroup sg = GetComponent<SortingGroup>();
        originalSortOrder = sg != null ? sg.sortingOrder : 0;
        if (sg != null) sg.sortingOrder = 100;   // 拖拽时盖在最上面

        // 牌放平到桌面
        transform.rotation = Quaternion.Euler(-90, 0, 0);

        // 镜头切高位，俯瞰全桌
        if (CameraRig.Instance != null)
            CameraRig.Instance.SetHigh(true);

        draggingCard = this;
    }

    void OnMouseDrag()
    {
        if (!enabled) return;

        Vector3 hit = RayToTable();
        transform.position = hit + Vector3.up * 0.5f;   // 浮在桌面上方一点
    }

    void OnMouseUp()
    {
        if (!enabled) return;
        EndDrag();
    }

    void EndDrag()
    {
        if (draggingCard != this) return;
        draggingCard = null;

        // 镜头切回低位
        if (CameraRig.Instance != null)
            CameraRig.Instance.SetHigh(false);

        // 检测鼠标下最近的玩家槽位
        CardSlot slot = GetSlotUnderMouse();

        if (slot != null && slot.IsEmpty && slot.isPlayerSide)
        {
            // 放到槽位上
            if (DeckManager.Instance != null)
                DeckManager.Instance.RemoveFromHand(card);

            EventBus.Publish(new CardPlayedEvent
            {
                card = card,
                laneIndex = slot.laneIndex,
                isPlayerSide = true
            });

            if (card.Data != null)
                Debug.Log("[出牌] " + card.CardName + " 拖到第" + (slot.laneIndex + 1) + "路");
        }
        else
        {
            // 弹回：位置、朝向、父物体、排序都恢复
            transform.position = originalPos;
            transform.rotation = originalRot;
            transform.SetParent(originalParent);

            SortingGroup sg = GetComponent<SortingGroup>();
            if (sg != null) sg.sortingOrder = originalSortOrder;
        }
    }

    // ========== 射线 ==========

    // 鼠标射线打到桌面（y=0 平面）上的点
    Vector3 RayToTable()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        float dist = 0;
        if (tablePlane.Raycast(ray, out dist))
            return ray.GetPoint(dist);
        return transform.position;   // 没打到就原地
    }

    // 找鼠标下的玩家槽位（用数学平面 + 最近距离，不用碰撞体）
    CardSlot GetSlotUnderMouse()
    {
        Vector3 hit = RayToTable();
        if (BoardManager.Instance == null) return null;
        return BoardManager.Instance.FindNearestPlayerSlot(hit);
    }
}
