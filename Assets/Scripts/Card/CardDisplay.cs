using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;


[RequireComponent(typeof(Card))]
[RequireComponent(typeof(BoxCollider))]


public class CardDisplay : MonoBehaviour
{
    private Card card;
    public Card Card { get { return card; } }   // 给 HandManager 用，避免每帧 GetComponent
    private Camera mainCam;

    // 拖前状态（换位松手后恢复）
    private int originalSortOrder;
    private Vector3 originalScale;

    // 拖动时牌的缩放（1.08 = 略微放大，让玩家知道拿起了哪张）
    public float dragScale = 0.8f;

    // 拖拽判定：鼠标移动超过多少屏幕像素才算拖（区分"点击"和"长按拖"）
    public float dragPixels = 15f;
    private Vector3 pressMousePos;   // 按下时的鼠标屏幕坐标（判拖用）
    private bool down;               // 这次按下是否有效（通过检查，可交互）

    // 当前正在长按拖拽的牌（全局标记，让 HandManager 别抢）
    public static CardDisplay draggingCard;
    // 鼠标悬浮中的牌（全局，唯一），HandManager 排列时让它往前突出
    public static CardDisplay hoveredCard;
    // 多选：当前选中要打出的牌（铃铛结算 / 弃牌都用它）
    public static List<CardDisplay> selectedCards = new List<CardDisplay>();

    public const int MaxSelect = 5;   // 一次最多选几张（牌型最多5张）

    private HandManager handManager;   // 手牌排列（拖拽换位用）

    void Awake()
    {
        card = GetComponent<Card>();
        mainCam = Camera.main;
        handManager = FindObjectOfType<HandManager>();

        // 确保有 3D 碰撞体（OnMouseDown 靠它）
        BoxCollider col = GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.8f, 2.6f, 0.05f);
    }
    void Update()
    {
        // 保底：鼠标松开了但 OnMouseUp 没触发（移出屏幕/失焦），手动结束拖拽
        if (draggingCard == this && Input.GetMouseButtonUp(0))
        {
            EndDrag();
        }
    }

    // 能不能操作这张牌（玩家牌、玩家回合、出牌阶段）
    bool CanPlay()
    {
        if (!enabled) return false;
        if (card.IsPlayer == false) return false;

        if (BattleManager.Instance != null)
        {
            if (!BattleManager.Instance.IsPlayerTurn) return false;
            if (BattleManager.Instance.CurrentPhase != TurnPhase.Play) return false;
        }
        return true;
    }

    public void OnClick()
    {
        if (!CanPlay()) { down = false; return; }
        down = true;
        pressMousePos = Input.mousePosition;
        originalScale = transform.localScale;
    }

    // 长按拖动：只在手牌扇面里换位，不会打上桌
    void OnMouseDrag()
    {
        // 拖牌途中打开了卷轴地图：立即结束拖拽，让 HandManager 把牌排回去
        if (GameProgress.InputLocked)
        {
            if (draggingCard == this || down)
            {
                Narrator.Say(SpeakTopic.ActionDuringMap);
                EndDrag();
            }
            down = false;
            return;
        }

        if (!down) return;

        // 还没进入拖拽：先判断鼠标是不是真的移动了，避免"点击"也被当成拖
        if (draggingCard != this)
        {
            Vector2 now = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            Vector2 start = new Vector2(pressMousePos.x, pressMousePos.y);
            if (Vector2.Distance(now, start) < dragPixels) return;   // 移动不够，仍算点击
            BeginDrag();
        }

        // 立着跟手走（沿手牌平面），旋转保持手牌扇面角度不变
        if (handManager != null)
        {
            transform.position = handManager.ScreenToHandPoint(Input.mousePosition);

            // 边拖边换位：手上的牌提前挪出空档，玩家能实时看到松手会插到哪
            // （只改列表顺序、不发事件；HandManager 的 followSpeed 会让其他牌滑过去）
            handManager.OnDragCardChangedPosition(card, Input.mousePosition);
        }
    }

    void BeginDrag()
    {
        SortingGroup sg = card.sortingGroup;
        originalSortOrder = 0;
        if (sg != null)
        {
            originalSortOrder = sg.sortingOrder;
            sg.sortingOrder = 100;   // 拖拽时盖在最上面
        }
        transform.localScale = originalScale * dragScale;   // 略微放大

        draggingCard = this;
    }

    void OnMouseUp()
    {
        if (draggingCard == this)
        {
            EndDrag();          // 长按拖拽结束 = 手牌内换位
        }
        else if (down)
        {
            ToggleSelect();     // 短按（没拖）：选中/取消选中
        }
        down = false;
    }

    // 鼠标悬浮：标记全局 hoveredCard，HandManager 排列时让这张牌突出
    void OnMouseEnter()
    {
        if (draggingCard != null) return;   // 拖拽中不切 hover
        hoveredCard = this;
    }

    void OnMouseExit()
    {
        if (hoveredCard == this) hoveredCard = null;
    }

    // 点选：高亮 + 桌面对应位置出现虚影；最多选 5 张
    void ToggleSelect()
    {
        if (selectedCards.Contains(this))
        {
            selectedCards.Remove(this);
            card.IsSelected = false;
        }
        else
        {
            if (selectedCards.Count >= MaxSelect)
            {
                Debug.Log("[选牌] 一次最多选 " + MaxSelect + " 张");
                return;
            }
            selectedCards.Add(this);
            card.IsSelected = true;
        }

        if (BattleView.Instance != null) BattleView.Instance.RefreshGhosts();
        UpdateCoinLiftPreview();
    }

    /// <summary>选牌时根据当前牌型消耗点数，让最顶上对应数量金币浮起预览</summary>
    void UpdateCoinLiftPreview()
    {
        if (Coin.Instance == null) return;
        if (selectedCards.Count == 0)
        {
            Coin.Instance.LiftTop(0);
            return;
        }

        var normalPicked = new List<CardDataSO>();
        int itemCostSum = 0;
        for (int i = 0; i < selectedCards.Count; i++)
        {
            var c = selectedCards[i];
            if (c == null || c.Card == null || c.Card.Data == null) continue;
            if (c.Card.Data.cardType == CardType.Item)
                itemCostSum += Mathf.Max(0, c.Card.Data.itemCost);
            else
                normalPicked.Add(c.Card.Data);
        }
        if (normalPicked.Count == 0 && itemCostSum == 0) { Coin.Instance.LiftTop(0); return; }

        int cost = itemCostSum;
        if (normalPicked.Count > 0)
        {
            HandType type = PokerResolver.Evaluate(normalPicked);
            cost += PokerResolver.GetPointCost(type);
        }
        Coin.Instance.LiftTop(cost);
    }

    void Select()
    {
        if (!selectedCards.Contains(this))
        {
            selectedCards.Add(this);
            card.IsSelected = true;
            if (BattleView.Instance != null) BattleView.Instance.RefreshGhosts();
        }
    }

    void Deselect()
    {
        if (selectedCards.Remove(this))
        {
            card.IsSelected = false;
            if (BattleView.Instance != null) BattleView.Instance.RefreshGhosts();
        }
    }

    // 松手 = 换到鼠标对应的手牌位置（拖拽期间被 HandManager 跳过，松手后它会自动排好）
    void EndDrag()
    {
        if (draggingCard != this) return;
        draggingCard = null;

        transform.localScale = originalScale;
        SortingGroup sg = card.sortingGroup;
        if (sg != null) sg.sortingOrder = originalSortOrder;

        if (GameProgress.InputLocked || handManager == null || DeckManager.Instance == null)
            return;   // 锁住了：不换位，HandManager 下帧把牌排回原位

        int targetIndex = handManager.GetIndexAtScreenPoint(Input.mousePosition);
        DeckManager.Instance.MoveHandCard(card, targetIndex);
    }
}
