using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 卡牌交互（2.5D 俯视桌面版）。
/// 短按（点一下）＝选中，牌沿 Y 轴轻微浮起；长按拖拽＝出牌。
/// 用数学平面（y=0 的桌面）做射线，不依赖槽位碰撞体。
/// </summary>
[RequireComponent(typeof(Card))]
[RequireComponent(typeof(BoxCollider))]
public class CardDisplay : MonoBehaviour
{
    private Card card;
    public Card Card { get { return card; } }   // 给 HandManager 用，避免每帧 GetComponent
    private Camera mainCam;

    // 拖前状态（弹回用）
    private Vector3 originalPos;
    private Quaternion originalRot;
    private Transform originalParent;
    private int originalSortOrder;
    private Vector3 originalScale;    // 拖前缩放（弹回用）

    // 拖动时牌的缩放（0.9 = 稍微缩小，抵消切高位的透视放大）
    public float dragScale = 0.9f;

    // 拖拽判定：鼠标移动超过多少屏幕像素才算拖（区分"点击"和"长按拖"）
    public float dragPixels = 15f;
    private Vector3 pressMousePos;   // 按下时的鼠标屏幕坐标（判拖用）

    // 桌面平面（y=0），拖拽时射线打它
    private Plane tablePlane;

    // 当前正在长按拖拽的牌（全局标记，让 HandManager 别抢）
    public static CardDisplay draggingCard;
    // 当前点选（突出）的牌（全局唯一）
    public static CardDisplay selectedCard;

    private bool down;   // 这次按下是否有效（通过检查，可交互）

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

    // 能不能操作这张牌（玩家牌、没打出、玩家回合、出牌阶段）
    bool CanPlay()
    {
        if (!enabled) return false;
        if (card.IsPlayer == false) return false;
        if (card.IsPlayed) return false;

        if (BattleManager.Instance != null)
        {
            if (!BattleManager.Instance.IsPlayerTurn) return false;
            if (BattleManager.Instance.CurrentPhase != TurnPhase.Play) return false;
        }
        return true;
    }

    void OnMouseDown()
    {
        if (!CanPlay()) { down = false; return; }
        down = true;
        pressMousePos = Input.mousePosition;

        // 记录原位（拖拽弹回用）
        originalPos = transform.position;
        originalRot = transform.rotation;
        originalParent = transform.parent;
        originalScale = transform.localScale;
    }

    // 长按拖动：无论是否选中都能拖
    void OnMouseDrag()
    {
        if (!down) return;

        // 还没进入拖拽：先判断鼠标是不是真的移动了，避免"点击"也被当成拖
        if (draggingCard != this)
        {
            Vector2 now = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            Vector2 start = new Vector2(pressMousePos.x, pressMousePos.y);
            if (Vector2.Distance(now, start) < dragPixels) return;   // 移动不够，仍算点击
            BeginDrag();   // 真拖了：放平拿起（相机这时才切高视角）
        }

        Vector3 hit = RayToTable();
        transform.position = hit + Vector3.up * 0.01f;   // 浮在桌面上方一点
    }

    void BeginDrag()
    {
        // 开始拖牌时，把当前突出的那张（不管是哪张）取消突出，让它变回去
        if (selectedCard != null)
            selectedCard.Deselect();

        SortingGroup sg = card.sortingGroup;
        originalSortOrder = 0;
        if (sg != null)
        {
            originalSortOrder = sg.sortingOrder;
            sg.sortingOrder = 100;   // 拖拽时盖在最上面
        }

        // 牌放平到桌面（牌面朝上 +Y），拖动时缩小一点抵消透视放大
        transform.rotation = Quaternion.Euler(90, 180, 0);
        transform.localScale = Vector3.one * dragScale;

        draggingCard = this;
    }

    void OnMouseUp()
    {
        if (draggingCard == this)
        {
            EndDrag();          // 长按拖拽结束
        }
        else if (down)
        {
            ToggleSelect();     // 短按（没拖）：切换突出
        }
        down = false;
    }

    void ToggleSelect()
    {
        if (selectedCard == this)
        {
            Deselect();
        }
        else
        {
            if (selectedCard != null) selectedCard.Deselect();
            Select();
        }
    }

    void Select()
    {
        selectedCard = this;
        card.IsSelected = true;
    }

    void Deselect()
    {
        if (selectedCard == this) selectedCard = null;
        card.IsSelected = false;
    }

    void EndDrag()
    {
        if (draggingCard != this) return;
        draggingCard = null;

        // 检测鼠标下最近的玩家槽位
        CardSlot slot = GetSlotUnderMouse();

        // 打出去；没打成（没放到空槽）就弹回
        bool played = PlayToSlot(slot);
        if (!played)
        {
            // 弹回：位置、朝向、父物体、缩放、排序都恢复（选中态保持不变）
            transform.position = originalPos;
            transform.rotation = originalRot;
            transform.SetParent(originalParent);
            transform.localScale = originalScale;

            SortingGroup sg = card.sortingGroup;
            if (sg != null) sg.sortingOrder = originalSortOrder;
        }
    }

    // 把这张牌打到槽位上（拖放、点选后点槽都走这里）。打成功返回 true。
    public bool PlayToSlot(CardSlot slot)
    {
        if (slot == null || !slot.IsEmpty || !slot.isPlayerSide) return false;
        if (!CanPlay()) return false;

        if (DeckManager.Instance != null)
            DeckManager.Instance.RemoveFromHand(card);

        card.IsPlayed = true;      // 打出后就不能再选了
        card.IsSelected = false;   // 出牌后取消选中
        if (selectedCard == this) selectedCard = null;

        EventBus.Publish(new CardPlayedEvent
        {
            card = card,
            laneIndex = slot.laneIndex,
            isPlayerSide = true
        });

        if (card.Data != null)
            Debug.Log("[出牌] " + card.CardName + " 放到第" + (slot.laneIndex + 1) + "路");

        return true;
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
