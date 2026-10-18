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
    [Header("勾上 = 这堆显示蓝色筹码（临时护盾），忽略 isPlayerStack；potArea 要留空，蓝码清零/被扣不进主池")]
    public bool showBlueChips = false;

    [Header("Stack layout")]
    public int coinsPerStack = 5;          // 每个小摞最多几枚
    public float coinHeight = 0.3f;
    public float patternSpacing = 0.55f;  // 五个位置相对图案中心的距离
    public int groupsPerRow = 3;            //一行放几组
    public float groupSpacing = 1.8f;           //组横向间距
    public float groupRowSpacing = 1.8f;        //组纵向间距

    public float interval=0.1f;
    public float dropHeight = 1.5f;   // 掉落演出高度
    public float dropDuration = 0.25f; // 掉落到目标位的时间
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
        SetCount(showBlueChips ? e.blueChips : (isPlayerStack ? e.playerChips : e.enemyChips));
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

        // 数量增加（篝火回血/上限+5/道具/偷取）：边池机制关闭后没有筹码飞过来了，
        // 新筹码直接从本堆上方排队掉进来（AddCoins 的掉落动画）
        if (newCount > oldTargetCount)
        {
            int add = Mathf.Max(0, newCount - coins.Count);
            if (add > 0) AddCoins(add);
            return;
        }

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
        StartCoroutine(AddCoinsRoutine(amount));
    }

    // 所有筹码从统一出生位置排队掉落：上一枚离开起点后下一枚才出发
    private IEnumerator AddCoinsRoutine(int amount)
    {
        GameProgress.chipFly = true;   // 篝火等它掉完才收场（开局掉落期间也算飞行动画）
        Vector3 spawnPos = stackRoot.position + Vector3.up * dropHeight;

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
            BindCoinClick(coin);

            // 目标位
            Vector3 localPos = GetLocalPosition(groupIndex, slotIndex, level);
            Vector3 targetPos = stackRoot.TransformPoint(localPos);
            Quaternion targetRot = stackRoot.rotation;

            Rigidbody rb = coin.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = true;
                rb.position = spawnPos;
                rb.rotation = targetRot;
                StartCoroutine(DropAnim(rb, targetPos, targetRot));
            }

            // 等上一枚离开出生位置再放下一枚
            yield return new WaitForSeconds(dropDuration * 0.1f);
        }
        GameProgress.chipFly = false;
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
            rb.detectCollisions = true;
            rb.position = worldPosition + Vector3.up * dropHeight;
            rb.rotation = worldRotation;
            StartCoroutine(DropAnim(rb, worldPosition, worldRotation));
        }
        else
        {
            coin.transform.position = worldPosition + Vector3.up * dropHeight;
            coin.transform.rotation = worldRotation;
            StartCoroutine(DropAnimNoRb(coin.transform, worldPosition, worldRotation));
        }
    }

    // 从上方插值滑到目标位，落地后锁死——不依赖物理引擎，位置永远精确
    IEnumerator DropAnim(Rigidbody rb, Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = rb.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dropDuration;
            float p = Mathf.Clamp01(t);
            // 竖直方向用 ease-in（越掉越快），模拟重力加速
            float eased = p * p;
            rb.position = Vector3.Lerp(startPos, targetPos, eased);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRot, p);
            yield return null;
        }
        rb.position = targetPos;
        rb.rotation = targetRot;
        rb.isKinematic = true;   // 锁死，纹丝不动
    }

    // 无刚体版本（理论上筹码都有 rb，留个兜底）
    IEnumerator DropAnimNoRb(Transform tr, Vector3 targetPos, Quaternion targetRot)
    {
        Vector3 startPos = tr.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dropDuration;
            float p = Mathf.Clamp01(t);
            tr.position = Vector3.Lerp(startPos, targetPos, p * p);
            tr.rotation = Quaternion.Slerp(tr.rotation, targetRot, p);
            yield return null;
        }
        tr.position = targetPos;
        tr.rotation = targetRot;
    }

    private IEnumerator LoseCoinsRoutine()
    {
        Debug.Log("[Chips] 开始飞，coins=" + coins.Count + "，target=" + targetCount);
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

            // 出牌结算：筹码先飞上天，再从主池正上方掉下来进主池（入池在 FlyUpRoutine 里做）
            if (potArea != null)
            {
                flyCoins.Add(coin);
                if (flyRoutine == null)
                    flyRoutine = StartCoroutine(FlyUpRoutine());
            }
            else
            {
                coin.transform.SetParent(null, true);
            }

            yield return new WaitForSeconds(interval);
            interval*=0.85f;
            if (interval < 0.01f) interval = 0.01f;
        }
        interval=0.1f;
        loseRoutine = null;
        // chipFly 由 FlyUpRoutine 全部落袋后负责关掉；没主池时这里直接关
        if (potArea == null) GameProgress.chipFly = false;
    }

    // ========== 飞上天 → 从主池上方掉落 ==========

    private readonly List<GameObject> flyCoins = new List<GameObject>();
    private Coroutine flyRoutine;

    [Header("飞天→主池掉落")]
    public float flyUpHeight = 5f;        // 往天上飞多高
    public float flyUpDuration = 0.3f;    // 飞天用时
    public float potDropHeight = 6f;      // 从主池正上方多高处出现往下掉
    public float potDropInterval = 0.15f; // 每枚间隔

    // 等本批筹码都飞完 → 逐枚从主池上方掉下来
    // 外层循环：飞完一批后再看 flyCoins——飞行期间下一手可能又塞了筹码进来，不能撂下不管
    private IEnumerator FlyUpRoutine()
    {
        while (true)
        {
            // 等 LoseCoinsRoutine 把筹码都塞进 flyCoins
            while (loseRoutine != null)
                yield return null;

            if (flyCoins.Count == 0)
                break;   // 真没了才收工

            List<GameObject> batch = new List<GameObject>(flyCoins);
            flyCoins.Clear();

            // 1. 全部同时竖直飞上天（物理向上冲），到点回收进池
            foreach (GameObject coin in batch)
            {
                if (coin == null) continue;
                Rigidbody rb = coin.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.constraints = RigidbodyConstraints.None;
                    rb.velocity = Vector3.up * (flyUpHeight / flyUpDuration);
                    rb.angularVelocity = Random.insideUnitSphere * 2f;
                }
            }

            yield return new WaitForSeconds(flyUpDuration);

            // 回收飞天上的筹码，直接还池（天上那批只是演出）
            foreach (GameObject coin in batch)
                if (coin != null) pool.Return(coin);

            // 2. 逐枚取新筹码，从各自落点的正上方直接掉到位（不再先掉主池中心再瞬移）
            for (int i = 0; i < batch.Count; i++)
            {
                GameObject coin = pool.Get();
                if (coin == null) continue;

                potArea.ReceiveCoin(coin);

                Rigidbody rb = coin.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 landPos = potArea.GetCoinWorldPos(coin);   // 它在摞里的准确位置
                    rb.isKinematic = true;
                    rb.position = landPos + Vector3.up * potDropHeight;
                    rb.rotation = potArea.transform.rotation;
                    StartCoroutine(PotDropAnim(rb, landPos));
                }

                yield return new WaitForSeconds(potDropInterval);
            }
        }
        flyRoutine = null;
        GameProgress.chipFly = false;
    }

    // 主池上方出现 → 竖直掉到主池 → 落地后排好
    private IEnumerator PotDropAnim(Rigidbody rb, Vector3 landPos)
    {
        Vector3 startPos = rb.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dropDuration;
            float p = Mathf.Clamp01(t);
            rb.position = Vector3.Lerp(startPos, landPos, p * p);   // ease-in 模拟重力加速
            yield return null;
        }
        potArea.Rearrange();   // 落地即收进摞，不用点击
    }

    // 本关结束清场：飞天上没回收的统一还池
    public void ClearScatter()
    {
        foreach (GameObject coin in flyCoins)
            if (coin != null) pool.Return(coin);
        flyCoins.Clear();
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
    // 账本清零：列表清空，实体也统一还池（没人调 pool.ReturnAll，指望不上）
    public void ClearLedger()
    {
        if (loseRoutine != null) { StopCoroutine(loseRoutine); loseRoutine = null; }
        if (flyRoutine != null) { StopCoroutine(flyRoutine); flyRoutine = null; }

        if (pool != null)
        {
            // 搁浅在 flyCoins 里的幽灵筹码 + 堆里没飞完的 + 飞来途中没入摞的，全部还池
            foreach (GameObject coin in flyCoins)
                if (coin != null) pool.Return(coin);
            foreach (GameObject coin in coins)
                if (coin != null) pool.Return(coin);
            foreach (GameObject coin in pendingPlayerCoins)
                if (coin != null) pool.Return(coin);
        }

        flyCoins.Clear();
        coins.Clear();
        pendingPlayerCoins.Clear();
        stackGroups.Clear();
        targetCount = 0;
        initialized = false;   // 关键：下一关 SetCount 才能重新生成
    }
}