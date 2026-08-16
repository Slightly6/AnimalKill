using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 手牌排列。挂在 HandArea 上。
/// 牌在屏幕下方扇形展开，始终面向相机（像拿着扑克手牌）。
/// </summary>
public class HandManager : MonoBehaviour
{
    [Header("排列设置")]
    public float cardSpacing = 1.5f;    // 牌与牌的水平间距
    public float maxAngle = 8f;         // 最边上的牌倾斜多少度
    public float yOffset = 0.2f;        // 越靠边越往下沉
    public float handDist = 9f;         // 手牌离相机的距离

    [Header("拖牌时向玩家倾斜")]
    public float dragTiltAngle = 15f;   // 拖牌时手牌区整体向玩家倾斜多少度（正=向玩家，负=反向）

    [Header("起点")]
    public Transform handCenter;        // 手牌区中心点（新版用相机相对，可留空）

    private DeckManager deck;

    void Start()
    {
        if (handCenter == null)
            handCenter = transform;
        deck = DeckManager.Instance;
    }

    void Update()
    {
        Arrange();   // 每帧实时排列，拖拽中的卡会被跳过
    }

    public void Arrange()
    {
        int count = deck.HandCards.Count;
        if (count == 0) return;
        if (Camera.main == null) return;

        Camera cam = Camera.main;

        // 屏幕下方一点作为手牌中心（viewport 0~1，0.5,0.10 = 下方中间）
        Vector3 center = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.10f, handDist));

        for (int i = 0; i < count; i++)
        {
            Card card = deck.HandCards[i];
            if (card == null) continue;

            // 正在拖拽的卡不要抢
            if (CardDisplay.draggingCard != null && CardDisplay.draggingCard.GetComponent<Card>() == card)
                continue;

            // 沿相机右方向展开，越靠边越往下沉
            float t = count == 1 ? 0 : (i / (float)(count - 1) - 0.5f) * 2f;   // -1 ~ 1
            float x = (i - (count - 1) / 2f) * cardSpacing;
            float sink = Mathf.Abs(t) * yOffset;

            Vector3 pos = center
                + cam.transform.right * x
                - cam.transform.up * sink;

            card.transform.position = pos;

            // 扇形倾斜：越靠边越斜，但整体面向相机
            float angle = -t * maxAngle;

            // 拖牌时整体向玩家倾斜，平时不倾斜
            float tilt = 0f;
            if (CardDisplay.draggingCard != null)
                tilt = dragTiltAngle;

            card.transform.rotation = cam.transform.rotation * Quaternion.Euler(tilt, 0, angle);

            // 错开渲染顺序，防止手牌互相闪烁
            SortingGroup sg = card.GetComponent<SortingGroup>();
            if (sg != null) sg.sortingOrder = 10 + i;
        }
    }
}
