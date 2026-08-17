using UnityEngine;
using UnityEngine.Rendering;

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
    public float handDist = 9f;         // 手牌离相机的距离
    public float zStep = 0.03f;         // 牌离相机的 Z 级差：越靠上的牌离相机越近，点击命中和视觉顺序一致

    [Header("拖牌时向玩家倾斜（0 = 不翻转）")]
    public float dragTiltAngle = 0f;    // 手牌区整体向玩家倾斜多少度（0 表示不翻转）

    [Header("起点")]
    public Transform handCenter;        // 手牌区中心点（新版用相机相对，可留空）

    private DeckManager deck;
    private Camera mainCam;   // 缓存主相机，避免每帧 Camera.main 查找

    void Start()
    {
        if (handCenter == null)
            handCenter = transform;
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

        // 屏幕下方一点作为手牌中心（viewport 0~1，0.5,0.10 = 下方中间）
        Vector3 center = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.10f, handDist));

        // 牌越多越密集：总宽度超过 maxHandWidth 就压缩间距
        float spacing = cardSpacing;
        if (count > 1)
        {
            float totalWidth = (count - 1) * cardSpacing;
            if (totalWidth > maxHandWidth)
                spacing = maxHandWidth / (count - 1);
        }

        for (int i = 0; i < count; i++)
        {
            Card card = deck.HandCards[i];
            if (card == null) continue;

            // 正在拖拽的卡不要抢
            if (CardDisplay.draggingCard != null && CardDisplay.draggingCard.Card == card)
                continue;

            // 沿相机右方向展开，越靠边越往下沉
            float t = count == 1 ? 0 : (i / (float)(count - 1) - 0.5f) * 2f;   // -1 ~ 1
            float x = (i - (count - 1) / 2f) * spacing;
            float sink = Mathf.Abs(t) * yOffset;

            Vector3 pos = center
                + cam.transform.right * x
                - cam.transform.up * sink;

            // 被点选的牌：沿它自己的 Y 轴突出一点
            if (card.IsSelected)
                pos += card.transform.up * selectLift;

            // 越靠上（i 大）的牌离相机近一点，让点击命中的牌和眼睛看到的"盖在上面"一致
            pos -= cam.transform.forward * (i * zStep);

            card.transform.position = pos;

            // 扇形倾斜：越靠边越斜，但整体面向相机
            float angle = -t * maxAngle;

            // 拖牌时整体向玩家倾斜，平时不倾斜
            float tilt = 0f;
            if (CardDisplay.draggingCard != null)
                tilt = dragTiltAngle;

            card.transform.rotation = cam.transform.rotation * Quaternion.Euler(tilt, 0, angle);

            // 错开渲染顺序，防止手牌互相闪烁（选中不额外提层级，只靠 Y 轴突出）
            SortingGroup sg = card.sortingGroup;
            if (sg != null)
            {
                int order = 10 + i;
                sg.sortingOrder = order;
            }
        }
    }
}
