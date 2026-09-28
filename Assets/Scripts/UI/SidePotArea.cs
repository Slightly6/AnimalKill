using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SidePotArea : MonoBehaviour
{
    private const int StacksPerPattern = 5;

    public CoinPool pool;
    public Transform stackRoot;
    public Chips PlayerChipsArea;
    public bool isPlayerStack = true;

    [Header("Stack layout")]
    public int coinsPerStack = 5;          // 每个小摞最多几枚
    public float coinHeight = 0.3f;
    public float patternSpacing = 0.55f;  // 五个位置相对图案中心的距离
    public int groupsPerRow = 1;    //一行放几组
    public float groupSpacing = 1.8f;           //组横向间距
    public float groupRowSpacing = 1.8f;        //组纵向间距

    [Header("Fly to pot")]
    public float flyForce = 1.5f;
    public float flyUpForce = 0.5f;

    private readonly List<GameObject> coins = new List<GameObject>();

    // [图案组][五个位置][该竖直小摞里的筹码]
    private readonly List<List<List<GameObject>>> stackGroups =
        new List<List<List<GameObject>>>();

    private int targetCount;
    private Coroutine loseRoutine;

    private void Awake()
    {
        if (stackRoot == null)
            stackRoot = transform;

        coinsPerStack = Mathf.Max(1, coinsPerStack);
        groupsPerRow = Mathf.Max(1, groupsPerRow);
        targetCount = coins.Count;
    }

    private void OnEnable()
    {
        EventBus.Unsubscribe<ChipsChangedEvent>(OnChipsChanged);
        EventBus.Subscribe<ChipsChangedEvent>(OnChipsChanged);

        if (targetCount < coins.Count && loseRoutine == null)
            loseRoutine = StartCoroutine(LoseCoinsRoutine());
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ChipsChangedEvent>(OnChipsChanged);

        if (loseRoutine != null)
        {
            StopCoroutine(loseRoutine);
            loseRoutine = null;
        }
    }

    private void OnChipsChanged(ChipsChangedEvent e)
    {
        // 边池只听边池数量：回血时 SidePotChips 变小 → LoseCoinsRoutine 飞向玩家堆
        SetCount(e.sidePotChips);
    }

    public void SetCount(int newCount)
    {
        targetCount = Mathf.Max(0, newCount);

        if (targetCount > coins.Count)
        {
            AddCoins(targetCount - coins.Count);
        }
        else if (targetCount < coins.Count && loseRoutine == null)
        {
            loseRoutine = StartCoroutine(LoseCoinsRoutine());
        }
    }

    private void AddCoins(int amount)
    {
        if (pool == null || stackRoot == null)
            return;

        coinsPerStack = Mathf.Max(1, coinsPerStack);

        for (int i = 0; i < amount; i++)
        {
            GameObject coin = pool.Get();
            if (coin == null)
                continue;

            FindLeastFilledStack(out int groupIndex, out int slotIndex);

            List<GameObject> stack = stackGroups[groupIndex][slotIndex];
            int level = stack.Count;

            stack.Add(coin);
            coins.Add(coin);

            coin.transform.SetParent(stackRoot, true);
            PlaceCoin(coin, groupIndex, slotIndex, level);
        }
    }

    // 找当前筹码最少且未满的小摞；所有小摞都满后再开一组。
    private void FindLeastFilledStack(out int bestGroup, out int bestSlot)
    {
        bestGroup = -1;
        bestSlot = -1;
        int fewestCoins = int.MaxValue;

        for (int g = 0; g < stackGroups.Count; g++)
        {
            for (int s = 0; s < StacksPerPattern; s++)
            {
                int count = stackGroups[g][s].Count;

                if (count < coinsPerStack && count < fewestCoins)
                {
                    fewestCoins = count;
                    bestGroup = g;
                    bestSlot = s;
                }
            }
        }

        if (bestGroup >= 0)
            return;

        List<List<GameObject>> newGroup = new List<List<GameObject>>();

        for (int i = 0; i < StacksPerPattern; i++)
            newGroup.Add(new List<GameObject>());

        stackGroups.Add(newGroup);

        bestGroup = stackGroups.Count - 1;
        bestSlot = 0;
    }

    private Vector3 GetLocalPosition(int groupIndex, int slotIndex, int level)
    {
        float s = patternSpacing;
        Vector3 slotOffset;

        switch (slotIndex)
        {
            case 0: slotOffset = new Vector3(-s, 0f, -s); break;
            case 1: slotOffset = new Vector3( s, 0f, -s); break;
            case 2: slotOffset = new Vector3( 0f, 0f,  0f); break;
            case 3: slotOffset = new Vector3(-s, 0f,  s); break;
            case 4: slotOffset = new Vector3( s, 0f,  s); break;
            default: slotOffset = Vector3.zero; break;
        }

        int rowWidth = Mathf.Max(1, groupsPerRow);
        int groupColumn = groupIndex % rowWidth;
        int groupRow = groupIndex / rowWidth;

        Vector3 groupOffset = new Vector3(
            groupColumn * groupSpacing,
            0f,
            groupRow * groupRowSpacing
        );

        return groupOffset
             + slotOffset
             + Vector3.up * (level * coinHeight);
    }

    private void PlaceCoin(GameObject coin, int groupIndex, int slotIndex, int level)
    {
        if (coin == null || stackRoot == null)
            return;

        Vector3 localPosition = GetLocalPosition(groupIndex, slotIndex, level);
        Vector3 worldPosition = stackRoot.TransformPoint(localPosition);
        Quaternion worldRotation = stackRoot.rotation;

        Rigidbody rb = coin.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = worldPosition;
            rb.rotation = worldRotation;
        }
        else
        {
            coin.transform.position = worldPosition;
            coin.transform.rotation = worldRotation;
        }
    }

    private IEnumerator LoseCoinsRoutine()
    {
        while (coins.Count > targetCount)
        {
            if (!TryFindClosestTopCoin(
                    out int groupIndex,
                    out int slotIndex,
                    out GameObject coin))
            {
                targetCount = coins.Count;
                break;
            }

            // 只移除该小摞最上面的一枚，其他筹码不重新排列。
            List<GameObject> stack = stackGroups[groupIndex][slotIndex];
            stack.RemoveAt(stack.Count - 1);
            coins.Remove(coin);

            if (PlayerChipsArea != null)
            {
                PlayerChipsArea.ReceiveCoin(coin);
            }
            else
            {
                coin.transform.SetParent(null, true);
            }

            Rigidbody rb = coin.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                Vector3 direction = (PlayerChipsArea != null
                    ? PlayerChipsArea.transform.position
                    : transform.position) - rb.position;

                direction.y = 0f;

                if (direction.sqrMagnitude > 0.001f)
                    direction.Normalize();
                else
                    direction = isPlayerStack ? Vector3.back : Vector3.forward;

                rb.AddForce(
                    direction * flyForce + Vector3.up * flyUpForce,
                    ForceMode.Impulse
                );

                rb.AddTorque(
                    Random.insideUnitSphere * 0.5f,
                    ForceMode.Impulse
                );
            }

            yield return new WaitForSeconds(0.2f);
        }

        loseRoutine = null;
    }

    // 在每个小摞的顶部筹码中，选择离主池最近的一枚。
    private bool TryFindClosestTopCoin(
        out int bestGroup,
        out int bestSlot,
        out GameObject bestCoin)
    {
        bestGroup = -1;
        bestSlot = -1;
        bestCoin = null;

        Vector3 potPosition = PlayerChipsArea != null
            ? PlayerChipsArea.transform.position
            : transform.position;

        float closestDistance = float.MaxValue;

        for (int g = 0; g < stackGroups.Count; g++)
        {
            for (int s = 0; s < StacksPerPattern; s++)
            {
                List<GameObject> stack = stackGroups[g][s];

                if (stack.Count == 0)
                    continue;

                GameObject candidate = stack[stack.Count - 1];
                if (candidate == null)
                    continue;

                Rigidbody rb = candidate.GetComponent<Rigidbody>();
                Vector3 candidatePosition = rb != null
                    ? rb.position
                    : candidate.transform.position;

                float distance = (candidatePosition - potPosition).sqrMagnitude;

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestGroup = g;
                    bestSlot = s;
                    bestCoin = candidate;
                }
            }
        }

        return bestCoin != null;
    }
}