using UnityEngine;

/// <summary>
/// 手牌排列。挂在 HandArea 上。
/// 牌在屏幕下方扇形展开，像拿着扑克手牌。
/// </summary>
public class HandManager : MonoBehaviour
{
    [Header("排列设置")]
    public float cardSpacing = 1.5f;    // 牌与牌的水平间距
    public float maxAngle = 8f;         // 最边上的牌倾斜多少度
    public float yOffset = 0.2f;        // 越靠边越往下沉

    [Header("起点")]
    public Transform handCenter;        // 手牌区中心点，不填就用自身 Transform

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
        float totalWidth = (count - 1) * cardSpacing;
        float startX = handCenter.position.x - totalWidth / 2f;
        for (int i = 0; i < count; i++)
        {
            Card card = deck.HandCards[i];
            if (card == null) continue;

            // 正在拖拽的卡不要抢
            if (CardDisplay.draggingCard != null && CardDisplay.draggingCard.GetComponent<Card>() == card)
                continue;

            float x = startX + i * cardSpacing;
            float y = handCenter.position.y;
            float distFromCenter = Mathf.Abs(i - (count - 1) / 2f);
            y -= distFromCenter * yOffset;

            card.transform.position = new Vector3(x, y, handCenter.position.z + i * 0.01f);

            float t = count == 1 ? 0 : (i / (float)(count - 1) - 0.5f) * 2f;
            float angle = -t * maxAngle;
            card.transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
}
