using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 钩子显示：击杀收的牌像真卡一样挂在钩子上，还会荡（单摆）。
/// 挂在 Hook 物体上。5 个挂点 + Card 预制体拖进来。
/// 收一张 → 刷一张挂到下一个空挂点；满 5 张结算 → 直接消失。
/// </summary>
public class HookDisplay : MonoBehaviour
{
    [Header("挂点（Hook 下建 5 个空物体，摆好，按顺序拖进来）")]
    public Transform[] hangPoints = new Transform[5];

    [Header("卡牌预制体（拖 Card.prefab）")]
    public GameObject cardPrefab;

    [Header("挂上去的牌外观")]
    public float cardScale = 0.6f;   // 牌缩放（比手牌的 0.7 小一圈）
    public float hangDrop = 0.78f;   // 牌中心离挂点往下多远 = 1.3×cardScale（牌高2.6的一半），改 cardScale 要跟着改

    [Header("新牌挂上来时，老牌被撞一下")]
    public float bumpKick = 30f;     // 踢一脚的角速度

    List<GameObject> spawned = new List<GameObject>();   // 当前挂着的牌（记的是支点物体）

    void Start()
    {
        EventBus.Subscribe<TrophyChangedEvent>(OnTrophyChanged);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<TrophyChangedEvent>(OnTrophyChanged);
    }

    void OnTrophyChanged(TrophyChangedEvent e)
    {
        Rebuild();
    }

    // 按当前战利品重建钩子上的牌
    void Rebuild()
    {
        if (GameManager.Instance == null) return;

        int count = GameManager.Instance.TrophyCount;

        // 结算 / 重开关卡（0 张）：全清空
        if (count == 0)
        {
            ClearAll();
            return;
        }

        // 有新牌挂上来：先踢一脚旧牌（钩子被撞，一起晃）
        for (int i = 0; i < spawned.Count; i++)
        {
            HangingCard h = spawned[i].GetComponent<HangingCard>();
            if (h != null) h.Bump(bumpKick);
        }

        // 只把多出来的新牌挂上去（旧牌一直留着，不用重建）
        for (int i = spawned.Count; i < count; i++)
        {
            SpawnCard(GameManager.Instance.GetTrophyCard(i), i);
        }
    }

    // 在第 index 个挂点挂一张牌
    void SpawnCard(CardDataSO data, int index)
    {
        if (data == null || cardPrefab == null) return;
        if (index < 0 || index >= hangPoints.Length) return;
        Transform point = hangPoints[index];
        if (point == null) return;

        // 支点：空物体放在挂点，牌挂在它下面，绕着它荡
        GameObject pivot = new GameObject("Hang_" + data.GetRankText());
        pivot.transform.SetParent(point, false);
        pivot.transform.localPosition = Vector3.zero;
        pivot.transform.localRotation = Quaternion.identity;

        // 牌本体：正面朝外、缩小、挂在支点下方
        GameObject go = Instantiate(cardPrefab, pivot.transform);
        go.transform.localPosition = new Vector3(0f, -hangDrop, 0f);
        go.transform.localScale = Vector3.one * cardScale;

        Card card = go.GetComponent<Card>();
        if (card != null)
        {
            card.Init(data, false);      // 只借它的外观，阵营填敌方（本身就不能拖）
            card.SetFaceDown(false);     // 正面朝外
        }

        // 关掉拖拽（挂着的牌不能拿）
        CardDisplay display = go.GetComponent<CardDisplay>();
        if (display != null) display.enabled = false;

        // 套上摆动物理（在支点上，绕着支点荡）
        pivot.AddComponent<HangingCard>();

        spawned.Add(pivot);
    }

    void ClearAll()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null) Destroy(spawned[i]);
        }
        spawned.Clear();
    }
}
