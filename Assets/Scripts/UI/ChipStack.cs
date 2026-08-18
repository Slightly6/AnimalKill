using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 筹码堆视觉。挂在一个空物体上（如 ChipStackHolder）。
/// 开局按 GameManager 里的筹码数，把 3D 圆柱体筹码摞在桌上；每个面值（颜色）单独一摞，
/// 玩家、敌人各摆一组。数值变动时重摆；打脸转移时播「筹码飞过去」的装饰动画。
///
/// 真实数量永远以 GameManager 为准，这里只是照着摆样子。
/// </summary>
public class ChipStack : MonoBehaviour
{
    [Header("面值（顺序随意，内部会自动从大到小取）")]
    public int[] denominations = { 1 };

    [Header("对应面值的筹码预制体（和面值一一对应：第几个面值就放第几个预制体）")]
    public GameObject[] chipPrefabs;

    [Header("两堆的位置（桌面上的空物体）")]
    public Transform playerStackAnchor;   // 玩家堆底
    public Transform enemyStackAnchor;    // 敌人堆底

    [Header("每个筹码厚度（和 prefab 的扁圆柱高度一致）")]
    public float chipHeight = 0.06f;

    [Header("一列最多摞多少个筹码（摞满开新的一列）")]
    public int chipsPerColumn = 10;

    [Header("最多开几列（列都满了就只涨数字、不加筹码）")]
    public int maxColumns = 5;

    [Header("列与列的水平间距")]
    public float columnSpacing = 0.65f;

    [Header("不规则散落幅度（每列随机往旁边错开一点）")]
    public float scatter = 0.12f;

    [Header("飞过去动画")]
    public float flyDuration = 0.5f;   // 单个筹码飞多久
    public float flyArcHeight = 0.6f;  // 飞行弧线最高点

    // 两堆当前摆好的筹码（从底到顶）
    private List<GameObject> playerChips = new List<GameObject>();
    private List<GameObject> enemyChips = new List<GameObject>();

    private int lastPlayerChips = -1;
    private int lastEnemyChips = -1;

    // 每列的不规则偏移（开局随机一次，之后摆列都稳定，不会每次重摆就乱跳）
    private Vector3[] columnOffsets;

    void Start()
    {
        // 开局随机每列的散落偏移（只随机一次，之后摆列都稳定）
        columnOffsets = new Vector3[maxColumns];
        for (int i = 0; i < maxColumns; i++)
        {
            float x = i * columnSpacing + Random.Range(-scatter, scatter);
            float z = Random.Range(-scatter, scatter);
            columnOffsets[i] = new Vector3(x, 0, z);
        }

        EventBus.Subscribe<ChipsChangedEvent>(OnChipsChanged);
        EventBus.Subscribe<ChipTransferEvent>(OnChipTransfer);

        RebuildStack(true);
        RebuildStack(false);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<ChipsChangedEvent>(OnChipsChanged);
        EventBus.Unsubscribe<ChipTransferEvent>(OnChipTransfer);
    }

    // 数值变了 → 重摆两边的筹码堆
    void OnChipsChanged(ChipsChangedEvent e)
    {
        RebuildStack(true);
        RebuildStack(false);
    }

    // 打脸转移 → 播飞过去动画（装饰，真实数量已由 ChipsChangedEvent 摆好）
    void OnChipTransfer(ChipTransferEvent e)
    {
        StartCoroutine(FlyChips(e.amount, e.toPlayer));
    }

    // ========== 摆柱子 ==========

    // 返回从小到大排序好的面值副本（Inspector 里顺序随便填，内部统一排序）
    int[] SortedDenominations()
    {
        int[] sorted = new int[denominations.Length];
        for (int i = 0; i < denominations.Length; i++)
            sorted[i] = denominations[i];
        System.Array.Sort(sorted);
        return sorted;
    }

    // 按面值贪心：把金额拆成最少个数的面值组合（从大面值开始取）
    List<int> GreedyBreak(int amount)
    {
        List<int> result = new List<int>();
        int[] sorted = SortedDenominations();
        for (int i = sorted.Length - 1; i >= 0; i--)
        {
            while (amount >= sorted[i])
            {
                result.Add(sorted[i]);
                amount -= sorted[i];
            }
        }
        return result;
    }

    // 找某个面值对应的 prefab
    GameObject ChipPrefabFor(int denom)
    {
        for (int i = 0; i < denominations.Length; i++)
        {
            if (denominations[i] == denom && i < chipPrefabs.Length)
                return chipPrefabs[i];
        }
        return null;
    }

    // 重摆一边的柱子（isPlayer=true 玩家，false 敌人）
    void RebuildStack(bool isPlayer)
    {
        if (GameManager.Instance == null) return;

        int amount = isPlayer ? GameManager.Instance.PlayerChips : GameManager.Instance.EnemyChips;
        int last = isPlayer ? lastPlayerChips : lastEnemyChips;
        if (amount == last) return;   // 没变，别动

        if (isPlayer) lastPlayerChips = amount;
        else lastEnemyChips = amount;

        List<GameObject> list = isPlayer ? playerChips : enemyChips;
        Transform anchor = isPlayer ? playerStackAnchor : enemyStackAnchor;

        // 清掉旧的
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null) Destroy(list[i]);
        }
        list.Clear();

        if (anchor == null) return;

        if (chipsPerColumn <= 0 || maxColumns <= 0) return;   // 配置非法，摆不了

        // 把金额拆成一个个筹码（大面值在底下）
        List<int> parts = GreedyBreak(amount);

        // 最多摆 chipsPerColumn × maxColumns 个筹码，超过就只涨数字、不再加筹码
        int maxVisible = chipsPerColumn * maxColumns;
        int visible = Mathf.Min(parts.Count, maxVisible);

        // 一列摞满就开下一列；列之间按开局随机的不规则偏移散落摆放
        int placed = 0;
        for (int c = 0; c < maxColumns && placed < visible; c++)
        {
            int inThisColumn = Mathf.Min(chipsPerColumn, visible - placed);
            for (int j = 0; j < inThisColumn; j++)
            {
                GameObject prefab = ChipPrefabFor(parts[placed]);
                if (prefab != null)
                {
                    GameObject chip = Instantiate(prefab, anchor);
                    Vector3 offset = columnOffsets[c];
                    chip.transform.localPosition = new Vector3(offset.x, chipHeight * 0.5f + j * chipHeight, offset.z);
                    list.Add(chip);
                }
                placed++;
            }
        }
    }

    // ========== 飞动画 ==========

    // 一边的柱子现在有多高（底到顶）
    float StackTop(bool isPlayer)
    {
        List<GameObject> list = isPlayer ? playerChips : enemyChips;
        int heightInChips = Mathf.Min(list.Count, chipsPerColumn);   // 最高那列有几层
        return heightInChips * chipHeight;
    }

    IEnumerator FlyChips(int amount, bool toPlayer)
    {
        List<int> parts = GreedyBreak(amount);
        bool sourceIsPlayer = !toPlayer;   // 飞向玩家 = 从敌人那边来
        bool targetIsPlayer = toPlayer;

        Transform sourceAnchor = sourceIsPlayer ? playerStackAnchor : enemyStackAnchor;
        Transform targetAnchor = targetIsPlayer ? playerStackAnchor : enemyStackAnchor;
        if (sourceAnchor == null || targetAnchor == null) yield break;

        // 按面值拆成多个筹码，一个个飞过去，稍错开
        for (int i = 0; i < parts.Count; i++)
        {
            GameObject prefab = ChipPrefabFor(parts[i]);
            if (prefab == null) continue;

            GameObject chip = Instantiate(prefab);
            Vector3 start = sourceAnchor.position + Vector3.up * (StackTop(sourceIsPlayer) + 0.2f);
            Vector3 end = targetAnchor.position + Vector3.up * (StackTop(targetIsPlayer) + 0.2f);

            float t = 0f;
            while (t < flyDuration)
            {
                t += Time.deltaTime;
                float p = t / flyDuration;
                Vector3 pos = Vector3.Lerp(start, end, p);
                pos.y += Mathf.Sin(p * Mathf.PI) * flyArcHeight;   // 往上抛个弧线
                chip.transform.position = pos;
                yield return null;
            }

            Destroy(chip);   // 装饰筹码飞完就销毁，真实筹码已经摆好了
            yield return new WaitForSeconds(0.05f);
        }
    }
}
