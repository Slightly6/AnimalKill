using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// 手牌排列。挂在 HandArea 上。
/// 牌在屏幕下方扇形展开，始终面向相机（像拿着扑克手牌）。
/// 牌越多越密集：超过最大宽度自动压缩间距。
/// </summary>
public class HandManager : MonoBehaviour
{
    [Header("排列设置")]
    public float cardSpacing = 1.5f;    // 牌与牌的水平间距（牌少时用这个）
    public float maxHandWidth = 12f;    // 手牌最大总宽度，牌多了自动挤进这个范围
    public float selectLift = 0.2f;     // 被点选的牌沿自己 Y 轴突出多少（小一点，别全露出来）
    public float maxAngle = 8f;         // 最边上的牌倾斜多少度
    public float yOffset = 0.2f;        // 越靠边越往下沉
    public float handDist = 8f;         // 手牌离相机的距离
    public float zStep = 0.03f;         // 牌离相机的 Z 级差：越靠上的牌离相机越近，点击命中和视觉顺序一致

    [Header("鼠标悬浮时抽出来")]
    public float hoverForward = 0.5f;   // 往相机方向突出多少
    public float hoverUp = 0.15f;        // 往上抬多少
    public float hoverLerpSpeed = 15f; // 平滑速度

    // 每张牌独立的 hover 偏移（切换 hover 牌时各自动画）
    private readonly Dictionary<Card, Vector3> hoverOffsets = new Dictionary<Card, Vector3>();

    [Header("落位平滑")]
    public float followSpeed = 17f;   // 目标格位变化（补牌/出牌/换位）时滑过去的快慢；0 = 瞬间吸附
    public float dragReorderDeadZone = 0.12f;   // 拖拽换位的迟滞（格位单位）：越大越不容易在中线附近来回抖

    [Header("拖牌时向玩家倾斜（0 = 不翻转）")]
    public float dragTiltAngle = 0f;    // 手牌区整体向玩家倾斜多少度（0 表示不翻转）

    private DeckManager deck;
    private Camera mainCam;   // 缓存主相机，避免每帧 Camera.main 查找

    void Start()
    {;
        deck = DeckManager.Instance;
        mainCam = Camera.main;
    }

    void Update()
    {
        Arrange();   // 每帧实时排列，拖拽中的卡会被跳过
    }

    public void Arrange()
    {
        int count = deck.HandCards.Count;
        if (count == 0) return;
        if (mainCam == null) mainCam = Camera.main;   // 兜底：切场景后重新找
        if (mainCam == null) return;

        Camera cam = mainCam;

        for (int i = 0; i < count; i++)
        {
            Card card = deck.HandCards[i];
            if (card == null) continue;

            // 正在飞进手牌：位置/朝向由 DeckManager 的飞入协程接管，这里别抢
            if (card.SkipArrange) continue;

            // 正在拖拽的卡不要抢
            if (CardDisplay.draggingCard != null && CardDisplay.draggingCard.Card == card)
                continue;

            // 扇面落位（和 GetSlotPosition 同一份算法）
            Vector3 pos = GetSlotPosition(i, count);

            // 被点选的牌：沿它自己的 Y 轴突出一点
            if (card.IsSelected)
                pos += card.transform.up * selectLift;

            // 鼠标悬浮的牌：往前突出（每张牌独立平滑）
            bool isHovered = (CardDisplay.hoveredCard != null && CardDisplay.hoveredCard.Card == card);
            Vector3 targetOffset = isHovered
                ? -cam.transform.forward * hoverForward + cam.transform.up * hoverUp
                : Vector3.zero;
            if (!hoverOffsets.TryGetValue(card, out Vector3 curOffset)) curOffset = Vector3.zero;
            curOffset = Vector3.Lerp(curOffset, targetOffset, hoverLerpSpeed * Time.deltaTime);
            hoverOffsets[card] = curOffset;
            pos += curOffset;

            // 平滑滑向格位：新牌飞进来/出牌/换位导致格位变化时，牌是滑过去而不是瞬间弹过去
            float follow = followSpeed > 0f ? Mathf.Clamp01(followSpeed * Time.deltaTime) : 1f;
            card.transform.position = Vector3.Lerp(card.transform.position, pos, follow);

            // 扇形倾斜：越靠边越斜，但整体面向相机
            // 拖牌时整体向玩家倾斜，平时不倾斜
            float tilt = 0f;
            if (CardDisplay.draggingCard != null)
                tilt = dragTiltAngle;
            Quaternion rot = cam.transform.rotation * Quaternion.Euler(tilt, 0, GetSlotAngle(i, count));
            card.transform.rotation = Quaternion.Slerp(card.transform.rotation, rot, follow);

            // 错开渲染顺序，防止手牌互相闪烁（选中不额外提层级，只靠 Y 轴突出）
            SortingGroup sg = card.sortingGroup;
            if (sg != null)
            {
                int order = 10 + i;
                sg.sortingOrder = order;
            }
        }
    }

    // 扇面上第 index 张牌的位置（抽牌飞进手牌也读这份，保证飞入终点和排好的位置完全一致）
    public Vector3 GetSlotPosition(int index, int count)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null || count <= 0) return Vector3.zero;

        Camera cam = mainCam;

        // 屏幕下方一点作为手牌中心（viewport 0~1，0.5,0.10 = 下方中间）
        Vector3 center = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.10f, handDist));

        float spacing = GetSpacingFor(count);
        float t = count == 1 ? 0 : (index / (float)(count - 1) - 0.5f) * 2f;   // -1 ~ 1
        float x = (index - (count - 1) / 2f) * spacing;
        float sink = Mathf.Abs(t) * yOffset;

        // 越靠上（index 大）的牌离相机近一点，让点击命中的牌和眼睛看到的"盖在上面"一致
        return center
             + cam.transform.right * x
             - cam.transform.up * sink
             - cam.transform.forward * (index * zStep);
    }

    // 扇面上第 index 张牌的倾斜角（俯仰为 0，面向相机）
    public Quaternion GetSlotRotation(int index, int count)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null || count <= 0) return Quaternion.identity;
        return mainCam.transform.rotation * Quaternion.Euler(0f, 0f, GetSlotAngle(index, count));
    }

    float GetSlotAngle(int index, int count)
    {
        float t = count <= 1 ? 0 : (index / (float)(count - 1) - 0.5f) * 2f;   // -1 ~ 1
        return -t * maxAngle;
    }

    float GetSpacingFor(int count)
    {
        // 牌越多越密集：总宽度超过 maxHandWidth 就压缩间距
        float spacing = cardSpacing;
        if (count > 1)
        {
            float totalWidth = (count - 1) * cardSpacing;
            if (totalWidth > maxHandWidth)
                spacing = maxHandWidth / (count - 1);
        }
        return spacing;
    }
    // 拖拽中实时换位：鼠标拖到哪，列表里就把这张牌挪到哪，
    // 其他牌立刻滑开让出空档（Arrange 每帧重排 + followSpeed 平滑），松手不用再等。
    // 迟滞：鼠标正压在两个格位的分界线上时别来回改，否则相邻两张牌会抖。
    public void OnDragCardChangedPosition(Card draggingCard, Vector3 screenPos)
    {
        int count = deck.HandCards.Count;
        if (count <= 1) return;

        int currentIndex = deck.HandCards.IndexOf(draggingCard);
        if (currentIndex < 0) return;

        float idxF = GetContinuousIndexAtScreenPoint(screenPos);          // 鼠标指向的连续索引
        int targetIndex = Mathf.Clamp(Mathf.RoundToInt(idxF), 0, count - 1);

        if (targetIndex == currentIndex) return;                          // 还在自己这一格
        if (Mathf.Abs(idxF - currentIndex) < 0.5f + dragReorderDeadZone)  // 没真正越过中线，先不动
            return;

        deck.MoveHandCard(draggingCard, targetIndex);   // 复用现成的换位（含拟态牌同步）
    }
    // 手牌中心（和 Arrange 里同一个点）
    public Vector3 GetHandCenter()
    {
        if (mainCam == null) mainCam = Camera.main;
        return mainCam.ViewportToWorldPoint(new Vector3(0.5f, 0.10f, handDist));
    }

    // 当前实际间距（牌多压缩后的值，和 Arrange 算法一致）
    public float GetSpacing()
    {
        int count = deck.HandCards.Count;
        float spacing = cardSpacing;
        if (count > 1 && (count - 1) * cardSpacing > maxHandWidth)
            spacing = maxHandWidth / (count - 1);
        return spacing;
    }

    // 鼠标屏幕点 → 手牌扇面上的世界点（拖拽跟手用）
    public Vector3 ScreenToHandPoint(Vector3 screenPos)
    {
        if (mainCam == null) mainCam = Camera.main;
        return mainCam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, handDist));
    }

    // 鼠标屏幕点 → 松手后该插入的手牌索引
    public int GetIndexAtScreenPoint(Vector3 screenPos)
    {
        int count = deck.HandCards.Count;
        if (count == 0) return 0;
        return Mathf.Clamp(Mathf.RoundToInt(GetContinuousIndexAtScreenPoint(screenPos)), 0, count - 1);
    }

    // 连续索引（不四舍五入）：拖动换位靠它算"离当前格位有多远"，做迟滞判定
    public float GetContinuousIndexAtScreenPoint(Vector3 screenPos)
    {
        int count = deck.HandCards.Count;
        if (count == 0) return 0;

        Vector3 p = ScreenToHandPoint(screenPos);
        Vector3 center = GetHandCenter();
        float localX = Vector3.Dot(p - center, mainCam.transform.right);   // 相对手牌中心的横向偏移
        return (count - 1) / 2f + localX / GetSpacing();
    }
}
