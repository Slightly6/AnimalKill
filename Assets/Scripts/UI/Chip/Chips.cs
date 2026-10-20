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

    public float interval=0.1f;          // 每枚离堆的间隔，逐枚加快
    public float intervalDecay = 0.85f;  // 每飞出一枚，下一枚间隔乘这个数（越小越快）
    public float minInterval = 0.03f;    // 间隔下限
    public float dropHeight = 1.5f;   // 掉落演出高度
    public float dropDuration = 0.25f; // 掉落到目标位的时间
    [Header("Fly to pot")]
    public float flyForce = 1.5f;
    public float flyUpForce = 0.5f;

    [Header("失去飞行（PopAndFlyAway 用）：飞向指定位置后归还对象池")]
    public float loseFlyHeight = 1.2f;      // 抛物线拱起高度
    public float loseFlyDuration = 0.45f;   // 单枚飞行用时

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

    // 每批开始时用的间隔（Inspector 配的 interval），批结束时复位，避免下一批从半路加速的位置开始
    private float baseInterval = -1f;

    private IEnumerator LoseCoinsRoutine()
    {
        Debug.Log("[Chips] 开始飞，coins=" + coins.Count + "，target=" + targetCount);
        if (baseInterval < 0f) baseInterval = interval;
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

            // 出牌结算：这枚一离堆就当场起飞（不再等整批一起走），走完整条抛物线进主池
            if (potArea != null)
            {
                flyingPotCount++;
                GameProgress.chipFly = true;
                StartCoroutine(FlyToPotRoutine(coin));
            }
            else
            {
                // 没有主池的那堆就是蓝色筹码堆（临时护盾）：到期就是收走。
                // 飞向离场点后归还对象池 —— 原来只 SetParent(null) 会把实体丢在场上，
                // 既漏还池，看起来也是硬消失。
                Transform dest = MapManager.Instance != null ? MapManager.Instance.discardFlyTarget : null;
                if (dest != null)
                {
                    flyingLoseCount++;
                    GameProgress.chipFly = true;
                    StartCoroutine(LoseFlyRoutine(coin, dest.position));
                }
                else if (pool != null)
                {
                    pool.Return(coin);
                }
                else
                {
                    coin.transform.SetParent(null, true);
                }
            }

            yield return new WaitForSeconds(interval);
            interval = Mathf.Max(minInterval, interval * intervalDecay);   // 逐枚加快：伤害越大，筹码出得越密
        }
        interval = baseInterval;
        loseRoutine = null;
        // chipFly 由单枚入池流程（FlyToPotRoutine）全部落袋后负责关掉；没主池时这里直接关（蓝码还有正在飞的，等飞完自己关）
        if (potArea == null && flyingLoseCount <= 0) GameProgress.chipFly = false;
    }

    // ========== 飞向主池（同一枚币走完整条抛物线） ==========

    private int flyingPotCount;   // 还在空中/正在入池的枚数，归零才放开 chipFly
    private int flyGeneration;    // 清场代次：中途清过场，这枚正在飞的就不再往下走（币的归属已经交给池子）

    [Header("飞天→主池（同一枚币走完整条抛物线，中途不还池重生）")]
    public float flyUpHeight = 5f;        // 抛物线顶点比筹码堆高出多少
    public float flyUpDuration = 0.85f;   // 从离堆到落进池子的总时长
    [Header("落池回弹")]
    public float bounceHeight = 0.12f;    // 落地后小跳多高
    public float bounceDuration = 0.12f;  // 小跳用时

    // 单枚闭环：这枚筹码一离堆就走完整条抛物线——先窜到顶点，再横穿桌面落进主池它自己的格子，
    // 落地轻弹一下锁位。全程同一枚实体，中途不还池、不重生，所以没有"飞到半空突然瞬移"的缝。
    // 曲线由 flyUpHeight（顶点高出多少）和 flyUpDuration（总时长）反解重力与初速，落点精确落在格子上。
    private IEnumerator FlyToPotRoutine(GameObject coin)
    {
        int gen = flyGeneration;

        coin.transform.SetParent(null, true);   // 离堆：堆清场时不会被一起收走
        potArea.ReceiveCoin(coin);              // 入池账 → 拿到它在摞里的格子；此后归属交给池子

        Vector3 landPos = potArea.GetCoinWorldPos(coin);
        Rigidbody rb = coin.GetComponent<Rigidbody>();

        if (rb == null)
        {
            coin.transform.position = landPos;   // 没刚体的兜底：直接摆到格子上
            EndFlyOne();
            yield break;
        }

        Vector3 startPos = rb.position;                       // 从堆里弹出来的当前位置
        Quaternion startRot = rb.rotation;
        Quaternion landRot = potArea.transform.rotation;

        // 反解重力与竖直初速：上升段 tUp 拱到 flyUpHeight 高，下落段 tDown 从顶点落到格子，两段和就是总时长
        float h = Mathf.Max(0.05f, flyUpHeight);
        float T = Mathf.Max(0.1f, flyUpDuration);
        float drop = Mathf.Max(0.05f, (startPos.y + h) - landPos.y);
        float g = 2f * Mathf.Pow(Mathf.Sqrt(h) + Mathf.Sqrt(drop), 2f) / (T * T);
        float vy0 = Mathf.Sqrt(2f * g * h);

        Vector3 vh = landPos - startPos;   // 水平分量按总时长均分，横向刚好落在格子上
        vh.y = 0f;
        vh /= T;

        rb.isKinematic = true;             // 手动控位：曲线就是标准抛物线，和物理同一形状但落点精确
        rb.constraints = RigidbodyConstraints.None;
        rb.detectCollisions = false;       // 空中不跟别的筹码/桌面碰，免得被撞歪打不进格子
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 tumbleAxis = Random.onUnitSphere;
        bool aborted = false;
        float ct = 0f;

        while (ct < T)
        {
            if (gen != flyGeneration || !rb.gameObject.activeInHierarchy)
            {
                aborted = true;   // 中途清场（关卡结束）：这枚已还池，别再写它
                break;
            }

            ct += Time.deltaTime;
            float t = Mathf.Min(ct, T);
            float p = t / T;

            Vector3 pos = startPos + vh * t;
            pos.y = startPos.y + vy0 * t - 0.5f * g * t * t;   // 抛物线：起手窜天，顶点后横穿桌面落向主池
            rb.position = pos;
            rb.rotation = Quaternion.Slerp(startRot, landRot, p)
                        * Quaternion.AngleAxis(360f * p, tumbleAxis);   // 空中翻一整圈，落地正好摆正
            yield return null;
        }

        if (!aborted)
        {
            // 落地回弹：小跳一下再压住，落池才有重量感（不再是砸下去纹丝不动）
            float bt = 0f;
            while (bt < 1f)
            {
                if (gen != flyGeneration || !rb.gameObject.activeInHierarchy)
                {
                    aborted = true;
                    break;
                }

                bt += Time.deltaTime / bounceDuration;
                rb.position = landPos + Vector3.up * (Mathf.Sin(Mathf.Clamp01(bt) * Mathf.PI) * bounceHeight);
                yield return null;
            }
        }

        if (!aborted)
        {
            rb.position = landPos;
            rb.rotation = landRot;
        }

        rb.detectCollisions = true;   // 无论正常落地还是中途作废都恢复：这枚回池后可能被别的流程复用
        EndFlyOne();
    }

    // 一枚收工：还在空中/正在入池的枚数减一，全落完了才放开 chipFly
    private void EndFlyOne()
    {
        flyingPotCount--;
        if (flyingPotCount <= 0)
        {
            flyingPotCount = 0;
            GameProgress.chipFly = false;
        }
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

    // ========== 失去飞行：从堆顶取币 → 飞向指定位置 → 归还对象池 ==========

    private int flyingLoseCount;   // 正在飞向目标位的筹码数，归零时关掉 chipFly

    // 从堆顶（沿用"离主池最近"的取币规则）取走最多 amount 枚，逐枚飞向 target 后归还对象池（不进主池）。
    // 返回实际取走的枚数，外部按同一次数扣数据，筹码堆和数值就不会脱节。
    public int PopAndFlyAway(int amount, Transform target)
    {
        if (amount <= 0) return 0;

        int popped = 0;
        for (int i = 0; i < amount; i++)
        {
            if (!TryFindClosestTopCoin(out int g, out int s, out GameObject coin))
                break;

            List<GameObject> stack = stackGroups[g][s];
            stack.RemoveAt(stack.Count - 1);
            coins.Remove(coin);

            targetCount = Mathf.Max(0, targetCount - 1);   // 堆自己的目标数同步减少，避免 SetCount 再播一遍减少动画

            if (target == null)
            {
                coin.transform.SetParent(null, true);
                pool.Return(coin);
            }
            else
            {
                flyingLoseCount++;
                GameProgress.chipFly = true;
                StartCoroutine(LoseFlyRoutine(coin, target.position));
            }
            popped++;
        }
        return popped;
    }

    // 单枚：抛物线拱起飞到目标位 → 归还对象池
    private IEnumerator LoseFlyRoutine(GameObject coin, Vector3 targetPos)
    {
        Rigidbody rb = coin.GetComponent<Rigidbody>();
        Vector3 startPos = rb != null ? rb.position : coin.transform.position;

        coin.transform.SetParent(null, true);
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / loseFlyDuration;
            float p = Mathf.Clamp01(t);
            Vector3 pos = Vector3.Lerp(startPos, targetPos, p);
            pos.y += Mathf.Sin(p * Mathf.PI) * loseFlyHeight;   // 拱一下，飞过去自然些
            if (rb != null) rb.position = pos;
            else coin.transform.position = pos;
            yield return null;
        }

        if (rb != null)
        {
            rb.position = targetPos;
            rb.detectCollisions = true;
        }
        else coin.transform.position = targetPos;

        pool.Return(coin);

        flyingLoseCount--;
        if (flyingLoseCount <= 0)
        {
            flyingLoseCount = 0;
            GameProgress.chipFly = false;
        }
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
        baseInterval = -1f;   // 下次扣筹码重新从 Inspector 配的间隔开始加速

        if (pool != null)
        {
            // 堆里没飞完的 + 飞来途中没入摞的，全部还池（在飞往主池途中的那些已经归 PotArea 管，由 ClearCoins 收）
            foreach (GameObject coin in coins)
                if (coin != null) pool.Return(coin);
            foreach (GameObject coin in pendingPlayerCoins)
                if (coin != null) pool.Return(coin);
        }

        flyingPotCount = 0;
        flyGeneration++;   // 作废还在空中的单枚流程
        GameProgress.chipFly = false;
        coins.Clear();
        pendingPlayerCoins.Clear();
        stackGroups.Clear();
        targetCount = 0;
        initialized = false;   // 关键：下一关 SetCount 才能重新生成
    }
}