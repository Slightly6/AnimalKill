using UnityEngine;

/// <summary>
/// 卡牌拖拽（世界空间 2D 版本，OnMouse 系列 + Collider2D）
/// </summary>
[RequireComponent(typeof(Card))]
[RequireComponent(typeof(BoxCollider2D))]
public class CardDisplay : MonoBehaviour
{
    private Card card;
    private Camera mainCam;
    private Vector3 dragOffset;

    // 拖前状态（弹回用）
    private Vector3 originalPos;
    private Quaternion originalRot;
    private Transform originalParent;
    private int originalSortOrder;

    // 拖拽中的 Z 深度
    private float dragZ;

    // 当前正在拖拽的卡（全局标记，让 HandManager 别抢）
    public static CardDisplay draggingCard;

    void Awake()
    {
        card = GetComponent<Card>();
        mainCam = Camera.main;

        // 确保有碰撞体
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.8f, 2.6f);  // 和卡牌大小匹配
    }

    void Start()
    {
        // 记住深度
        dragZ = transform.position.z;
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

        // 检查能不能出牌（BattleManager 不存在就跳过检查）
        if (BattleManager.Instance != null)
        {
            if (!BattleManager.Instance.IsPlayerTurn) return;
            if (BattleManager.Instance.CurrentPhase != TurnPhase.Play) return;
        }

        // 记住原位
        originalPos = transform.position;
        originalRot = transform.localRotation;
        originalParent = transform.parent;

        // 计算偏移
        Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = dragZ;
        dragOffset = transform.position - mouseWorld;

        // 提到前面
        dragZ = -2f;  // 拖拽时离相机更近
        Vector3 pos = transform.position;
        pos.z = dragZ;
        transform.position = pos;

        // 拖拽时摆正
        transform.localRotation = Quaternion.identity;

        draggingCard = this;
    }

    void OnMouseDrag()
    {
        if (!enabled) return;
        Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = dragZ;
        transform.position = mouseWorld;   // 卡牌中心直接贴鼠标
    }

    void OnMouseUp()
    {
        if (!enabled) return;
        EndDrag();
    }

    void EndDrag()
    {
        if (draggingCard != this) return;   // 防止重复调用
        draggingCard = null;

        // 恢复深度
        dragZ = originalPos.z;

        // 检测鼠标下有没有槽位
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
            // 弹回
            transform.position = originalPos;
            transform.localRotation = originalRot;
            transform.SetParent(originalParent);
        }
    }

    // ========== 射线检测槽位 ==========

    CardSlot GetSlotUnderMouse()
    {
        Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;

        Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorld);

        for (int i = 0; i < hits.Length; i++)
        {
            CardSlot slot = hits[i].GetComponent<CardSlot>();
            if (slot != null) return slot;
        }
        return null;
    }
}
