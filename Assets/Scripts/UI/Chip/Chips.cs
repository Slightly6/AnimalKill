using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chips : MonoBehaviour
{
    private const int StacksPerPattern = 5;

    public ChipPool pool;
    public Transform stackRoot;
    public PotArea potArea;
    public bool isPlayerStack = true;

    [Header("Stack layout")]
    public int coinsPerStack = 5;          // 每个小摞最多几枚
    public float coinHeight = 0.3f;
    public float patternSpacing = 0.55f;  // 五个位置相对图案中心的距离
    public int groupsPerRow = 3;            //一行放几组
    public float groupSpacing = 1.8f;           //组横向间距
    public float groupRowSpacing = 1.8f;        //组纵向间距

    public float interval=0.2f;
    [Header("Fly to pot")]
    public float flyForce = 1.5f;
    public float flyUpForce = 0.5f;

    private readonly List<GameObject> coins = new List<GameObject>();
    private readonly List<GameObject> pendingPlayerCoins = new List<GameObject>();
    // [图案组][五个位置][该竖直小摞里的筹码]
    private readonly List<List<List<GameObject>>> stackGroups =
        new List<List<List<GameObject>>>();

    private int targetCount;
    private Coroutine loseRoutine;
    private bool initialized;   // 只有第一次SetCount允许从池里生成初始筹码

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
        SetCount(isPlayerStack ? e.playerChips : e.enemyChips);
    }

    public void SetCount(int newCount)
    {
        newCount = Mathf.Max(0, newCount);

        // 只有第一次允许从池里生成初始筹码
        if (!initialized)
        {
            initialized = true;
            targetCount = newCount;

            int existingCount = coins.Count + pendingPlayerCoins.Count;
            int amountToAdd = newCount - existingCount;
            if (amountToAdd > 0)
                AddCoins(amountToAdd);

            return;
        }

        int oldTargetCount = targetCount;
        targetCount = newCount;

        // 之后数量增加：不从池子生成，新筹码由边池飞过来（ReceiveCoin）
        if (newCount >= oldTargetCount)
            return;

        // 数量减少：从正式摞里掉
        if (targetCount < coins.Count && loseRoutine == null)
        {
            GameProgress.chipFly=true;
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

            BindCoinClick(coin);
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

            if (potArea != null)
            {
                potArea.ReceiveCoin(coin);
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

                Vector3 direction = (potArea != null
                    ? potArea.transform.position
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
            
            yield return new WaitForSeconds(interval);
            interval*=0.85f;
            if (interval < 0.05f) interval = 0.05f;
        }
        GameProgress.chipFly=false;
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

        Vector3 potPosition = potArea != null
            ? potArea.transform.position
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
    public void ReceiveCoin(GameObject coin)
    {
        if (coin == null)
            return;

        if (!pendingPlayerCoins.Contains(coin))
            pendingPlayerCoins.Add(coin);

        coin.transform.SetParent(transform, true);

        Rigidbody rb = coin.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 飞来的筹码也能点击收编
        BindCoinClick(coin);
    }

    // 给筹码（含子物体上的 Collider）统一绑定点击组件，点击时回传筹码根物体
    private void BindCoinClick(GameObject coin)
    {
        if (coin == null)
            return;

            PotCoinClick click = coin.GetComponent<PotCoinClick>();
            if (click == null)
                click = coin.AddComponent<PotCoinClick>();
            click.Initialize(this, coin);
            return;
        

    }
    public void OnCoinClicked(GameObject clickedCoin)
    {
        if (clickedCoin == null || coins.Contains(clickedCoin) == false)
        return;   // 不是本堆的筹码就忽略

        // 有散落的先收编
        if (pendingPlayerCoins.Count > 0)
        ArrangePendingCoins();
    }

// 把所有散落等待中的筹码收编进摞，自动排好
    public void ArrangePendingCoins()
    {
        for (int i = 0; i < pendingPlayerCoins.Count; i++)
        {
            GameObject coin = pendingPlayerCoins[i];

            FindLeastFilledStack(out int g, out int s);
            List<GameObject> stack = stackGroups[g][s];

            stack.Add(coin);
            coins.Add(coin);

            coin.transform.SetParent(stackRoot, false);
            PlaceCoin(coin, g, s, stack.Count - 1);
        }

        pendingPlayerCoins.Clear();
    }
    // 账本清零（实体已由 CoinPool.ReturnAll 统一还池）
    public void ClearLedger()
    {
        if (loseRoutine != null) { StopCoroutine(loseRoutine); loseRoutine = null; }
        coins.Clear();
        pendingPlayerCoins.Clear();
        stackGroups.Clear();
        targetCount = 0;
        initialized = false;   // 关键：下一关 SetCount 才能重新生成
    }
}