using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TOPhat : Singleton<TOPhat>
{
    public float radius = 0.5f;
    public float speed = 90f;
    public Vector3 CenterOffset = new Vector3(0, 0.5f, 0);
    public bool IsBusy { get; private set; }=false;
    public bool sai=false;
    public GameObject nodePrefab;      // Inspector 里拖 Prefab 进来

    [Header("关卡结束掉骰子（露出个头就销毁）")]
    public float fallCatchTime = 0.5f;    // 从帽上方落到帽口要多久，到点就销毁

    [Header("商店悬浮卡：从帽子里冒骰子")]
    public float hatRadius = 0.3f;        // 冒出的横向范围（略小于帽子内径）
    public float startHeight = 0f;        // 生成位置在帽子本地的高度（帽子底）
    public float emergeHeight = 0.8f;     // 冒出后停在的本地高度
    public float emergeTime = 0.28f;      // 单颗冒出/缩回时长
    public float emergeStagger = 0.05f;   // 依次冒出的间隔，营造"变出来"的节奏
    public float dieGap = 1.2f;           // 骰子间距系数：间距 = 骰子实际直径 × 它。1 = 刚好贴着，调大留缝

    [Header("商店落脚点（甩出来后滑到这里停着，买完再滑回去甩走）")]
    public Vector3 shopPosition = new Vector3(3.74f, 3.45f, -1.96f);
    public float shopMoveTime = 0.4f;     // 滑过去/滑回来的用时

    [Header("帽子计数显示")]
    public TextMeshPro countText;      // 帽子上的数字（子物体，不拖自动找）
    private int diceCollected;         // 本关已掉完的骰子数
    private int startDice;             // 本关收集前的骰子总数

    private readonly List<Transform> hoverDice = new List<Transform>();   // 现在浮在帽子外面的骰子
    private readonly List<int> hoverCells = new List<int>();              // 每颗骰子领的格号，和 hoverDice 一一对应
    private Coroutine hoverRoutine;    // "往上冒"的协程（换卡时要停掉重来）
    private float currentAngle = 180f; // 帽子当前角度（180 = 常驻位，90 = 甩出来的位置）

    // 格点发放：帽口圆面按 dieSpacing 切格子，一格一颗 —— 格距不小于骰子直径，任意两颗都没有重合部分
    private readonly List<Vector3> hatCells = new List<Vector3>();   // 全部候选格点（帽内的排在前，扩出去的在后）
    private int insideCount;         // 帽口圆内的格点数
    private int gridN;               // 已铺到的格数半径
    private List<Vector3> sessionSpots;                       // 本次悬浮会话的格点序列（帽内已洗牌）
    private int sessionTaken;                                 // 会话里已领出去几个
    private readonly List<int> freedSpots = new List<int>();  // 缩回去空出的格号，下次优先复用

    void Start()
    {
        // 平时常驻显示当前拥有的骰子总数
        if (countText == null) countText = GetComponentInChildren<TextMeshPro>();
        if (countText != null) countText.text = GameProgress.dice.ToString();
    }

    // 骰子总数变了（商店消费等）让帽子数字跟着刷新
    public void RefreshCount()
    {
        if (countText == null) countText = GetComponentInChildren<TextMeshPro>();
        if (countText != null) countText.text = GameProgress.dice.ToString();
    }

    // ===== 商店卡悬浮：从帽子里冒骰子 =====

    /// <summary>悬浮商店卡时调用：帽子里冒出 count 颗骰子（count = 卡价）</summary>
    public void ShowHoverDice(int count)
    {
        count = Mathf.Max(0, count);
        // 买不起就少冒几颗，顺便当面告诉玩家"你还差多少"
        if (!GameProgress.cheatMode) count = Mathf.Min(count, GameProgress.dice);

        // 已经在外面浮着的保持原位不动，只补差额 / 收走多出来的
        if (hoverRoutine != null) { StopCoroutine(hoverRoutine); hoverRoutine = null; }
        while (hoverDice.Count < count)
        {
            int cell = TakeCell();
            hoverDice.Add(SpawnHoverDie(CellSpot(cell)));
            hoverCells.Add(cell);
        }
        while (hoverDice.Count > count) RemoveLastHoverDie();

        if (hoverDice.Count > 0) hoverRoutine = StartCoroutine(RiseHoverDiceRoutine());
    }

    /// <summary>移开卡牌时调用：冒出的骰子全部缩回帽子，然后销毁</summary>
    public void HideHoverDice()
    {
        if (hoverRoutine != null) { StopCoroutine(hoverRoutine); hoverRoutine = null; }
        if (hoverDice.Count == 0) return;

        // 交给独立协程去收，跟 hoverDice 脱钩 —— 后面再悬浮新卡也不用等它
        List<Transform> dice = new List<Transform>(hoverDice);
        hoverDice.Clear();
        hoverCells.Clear();
        sessionSpots = null;    // 会话结束，下次悬浮重新洗牌换一批位置
        freedSpots.Clear();
        StartCoroutine(SinkAndDestroyAll(dice));
    }

    // 在指定格点生成一颗骰子（先待在帽底，等 RiseHoverDiceRoutine 把它送上来）
    Transform SpawnHoverDie(Vector3 spot)
    {
        GameObject die = Instantiate(nodePrefab, transform);   // 挂在帽子下，跟着帽子走
        die.transform.localPosition = new Vector3(spot.x, startHeight, spot.z);
        die.transform.localRotation = Random.rotation;
        die.name = "HoverDice";

        // 装饰用的骰子不参与物理：拆掉刚体、关掉碰撞，免得被帽子或别的东西撞飞
        Rigidbody rb = die.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);
        foreach (Collider c in die.GetComponentsInChildren<Collider>()) c.enabled = false;
        die.tag = "Untagged";   // 别让 GameManager 那句"等骰子落完"把它算进去

        return die.transform;
    }

    // ===== 格点发放：保证不重叠的关键 =====

    // 领一个格号：先复用缩回去空出来的，没有就按会话顺序领下一个（同一颗骰子占的格子绝不发第二次）
    int TakeCell()
    {
        EnsureSession();

        int cell;
        if (freedSpots.Count > 0)
        {
            cell = freedSpots[freedSpots.Count - 1];
            freedSpots.RemoveAt(freedSpots.Count - 1);
        }
        else
        {
            cell = sessionTaken;
            sessionTaken++;
        }
        return cell;
    }

    Vector3 CellSpot(int cell) { return sessionSpots[cell]; }

    // 保证会话格点序列存在且够长（不够就往外扩圈补）
    void EnsureSession()
    {
        if (sessionSpots == null)   // 新会话：帽内格点重新洗牌
        {
            EnsureCellsBuilt(1);
            ShuffleInside();
            sessionSpots = new List<Vector3>(hatCells);
            sessionTaken = 0;
            freedSpots.Clear();
        }
        if (sessionTaken >= sessionSpots.Count)   // 会话中格点用完了：继续往外扩
        {
            EnsureCellsBuilt(sessionTaken + 1);
            sessionSpots.AddRange(hatCells.GetRange(sessionSpots.Count, hatCells.Count - sessionSpots.Count));
        }
    }

    // 保证 hatCells 里至少有 count 个格点：第一次铺帽口圆内，不够就往外一圈圈扩正方形
    void EnsureCellsBuilt(int count)
    {
        float sp = CellSpacing();

        if (gridN <= 0 && hatCells.Count == 0)   // 第一次：帽口圆内整片铺格点
        {
            gridN = Mathf.Max(1, Mathf.RoundToInt(hatRadius / sp));
            for (int ix = -gridN; ix <= gridN; ix++)
                for (int iz = -gridN; iz <= gridN; iz++)
                {
                    float x = ix * sp, z = iz * sp;
                    if (x * x + z * z <= hatRadius * hatRadius + 0.0001f)
                        hatCells.Add(new Vector3(x, 0f, z));
                }
            insideCount = hatCells.Count;
        }

        while (hatCells.Count < count)   // 帽内装不下：往外扩一圈（圈里也洗乱）
        {
            gridN++;
            List<Vector3> ring = new List<Vector3>();
            for (int ix = -gridN; ix <= gridN; ix++)
                for (int iz = -gridN; iz <= gridN; iz++)
                {
                    if (Mathf.Max(Mathf.Abs(ix), Mathf.Abs(iz)) != gridN) continue;   // 只要最外圈
                    ring.Add(new Vector3(ix * sp, 0f, iz * sp));
                }
            for (int i = ring.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (ring[i], ring[j]) = (ring[j], ring[i]);
            }
            hatCells.AddRange(ring);
        }
    }

    // 骰子的实际直径：直接量 nodePrefab 包围盒最长边（自动跟上 prefab 的缩放，改多大都叠不了）
    float CellSpacing()
    {
        float d = 0f;
        if (nodePrefab != null)
        {
            Renderer r = nodePrefab.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                Vector3 s = r.bounds.size;
                d = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            }
        }
        return Mathf.Max(0.01f, d * dieGap);
    }

    // 只洗帽口圆内的那段（外圈格点保持"一圈一乱"，永远不会排到帽内格子前面）
    void ShuffleInside()
    {
        for (int i = insideCount - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (hatCells[i], hatCells[j]) = (hatCells[j], hatCells[i]);
        }
    }

    // 多出来的骰子：从名单里摘掉，让它自己缩回帽底再销毁；它占的格子空出来留给下次
    void RemoveLastHoverDie()
    {
        int last = hoverDice.Count - 1;
        Transform die = hoverDice[last];
        freedSpots.Add(hoverCells[last]);
        hoverDice.RemoveAt(last);
        hoverCells.RemoveAt(last);

        if (die == null) return;
        StartCoroutine(SinkAndDestroy(die));
    }

    // 把 hoverDice 里所有骰子往上升。已经在顶上的原地不动，新冒出来的按 emergeStagger 依次走
    IEnumerator RiseHoverDiceRoutine()
    {
        List<Vector3> froms = new List<Vector3>();
        List<float> delays = new List<float>();
        int risers = 0;

        for (int i = 0; i < hoverDice.Count; i++)
        {
            Transform d = hoverDice[i];
            Vector3 from = d != null ? d.localPosition : Vector3.zero;
            froms.Add(from);

            // 还没到顶的才算"要冒"，它们才有依次出场的间隔
            bool needsRise = d != null && d.localPosition.y < emergeHeight - 0.001f;
            delays.Add(needsRise ? risers * emergeStagger : 0f);
            if (needsRise) risers++;
        }

        float dur = Mathf.Max(0.01f, emergeTime);
        float total = dur + emergeStagger * Mathf.Max(0, risers - 1);
        float t = 0f;
        while (t < total)
        {
            t += Time.deltaTime;
            for (int i = 0; i < hoverDice.Count && i < froms.Count; i++)
            {
                Transform d = hoverDice[i];
                if (d == null) continue;
                Vector3 to = new Vector3(froms[i].x, emergeHeight, froms[i].z);
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - delays[i]) / dur));
                d.localPosition = Vector3.Lerp(froms[i], to, p);
            }
            yield return null;
        }
        for (int i = 0; i < hoverDice.Count && i < froms.Count; i++)
        {
            if (hoverDice[i] == null) continue;
            hoverDice[i].localPosition = new Vector3(froms[i].x, emergeHeight, froms[i].z);
        }

        hoverRoutine = null;
    }

    IEnumerator SinkAndDestroyAll(List<Transform> dice)
    {
        List<Vector3> froms = new List<Vector3>();
        for (int i = 0; i < dice.Count; i++)
            froms.Add(dice[i] != null ? dice[i].localPosition : Vector3.zero);

        float dur = Mathf.Max(0.01f, emergeTime);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            for (int i = 0; i < dice.Count; i++)
            {
                if (dice[i] == null) continue;
                Vector3 to = new Vector3(froms[i].x, startHeight, froms[i].z);
                dice[i].localPosition = Vector3.Lerp(froms[i], to, p);
            }
            yield return null;
        }
        for (int i = 0; i < dice.Count; i++)
            if (dice[i] != null) Destroy(dice[i].gameObject);
    }

    IEnumerator SinkAndDestroy(Transform die)
    {
        Vector3 from = die.localPosition;
        Vector3 to = new Vector3(from.x, startHeight, from.z);

        float dur = Mathf.Max(0.01f, emergeTime);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            if (die == null) yield break;
            die.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur)));
            yield return null;
        }
        if (die != null) Destroy(die.gameObject);
    }

    // ===== 关卡结束：掉骰子 =====

    // Update is called once per frame
    void Update()
    {
        if (sai)
        {
            StartCoroutine(PlayCollect(10));
            sai = false;
        }
    }
    public IEnumerator PlayCollect(int diceCount)
    {
        IsBusy = true;

        // 从收之前的总数开始，每进一颗 +1，掉完正好是最新总数
        // （SettlePot 在演出前已把 GameProgress.dice 加好，所以要减回去当起点）
        startDice = Mathf.Max(0, GameProgress.dice - diceCount);
        diceCollected = 0;
        if (countText == null) countText = GetComponentInChildren<TextMeshPro>();
        if (countText != null) countText.text = startDice.ToString();

        yield return SwingTo(90f);   // 甩出来接骰子

        while (diceCount > 0)
        {
            GameObject dice = Instantiate(nodePrefab, CenterOffset + new Vector3(0, 10, 0), Quaternion.identity);
            Rigidbody rb = dice.GetComponent<Rigidbody>();
            if (rb != null) rb.velocity = Vector3.down * 17f;
            StartCoroutine(FallAndDestroy(dice));   // 掉到帽口就销毁，不用触发器
            diceCount--;
            yield return new WaitForSeconds(0.1f);
        }
        yield return new WaitForSeconds(1f);

        yield return SwingTo(180f);   // 甩回常驻位

        IsBusy = false;
    }

    // 把帽子沿弧线甩到 target 角度：180 = 常驻位，90 = 甩出来的位置（接骰子 / 商店冒骰子共用）
    IEnumerator SwingTo(float target)
    {
        while (currentAngle != target)
        {
            currentAngle = Mathf.MoveTowards(currentAngle, target, speed * Time.deltaTime);
            SetCircularPosition(currentAngle);
            yield return null;
        }
    }

    // 商店进场：先甩出来，再滑到商店落脚点停住，悬浮卡冒骰子全从它这儿出
    public IEnumerator PlayEnter()
    {
        IsBusy = true;
        yield return SwingTo(90f);
        yield return MoveTo(shopPosition);
        IsBusy = false;
    }

    // 离开商店：先滑回甩出来的位置，再甩回常驻位（没进过商店就全程原地，等于空转）
    public IEnumerator PlayExit()
    {
        IsBusy = true;
        yield return MoveTo(ArcPos(currentAngle));
        yield return SwingTo(180f);
        IsBusy = false;
    }

    // 帽子平滑滑到目标点（进出商店落脚点用）
    IEnumerator MoveTo(Vector3 target)
    {
        Vector3 from = transform.position;
        float dur = Mathf.Max(0.01f, shopMoveTime);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            transform.position = Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }
        transform.position = target;
    }

    // 角度对应的弧线位置（180 = 常驻位，90 = 甩出来的位置）
    Vector3 ArcPos(float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        float y = Mathf.Cos(rad) * radius;//-1-0
        float z = Mathf.Sin(rad) * radius;//0-1

        return CenterOffset + new Vector3(0, z, y);
    }
    void SetCircularPosition(float angle)
    {
        transform.position = ArcPos(angle);
    }

    // 掉下来的骰子：落到帽口（计时到点）就销毁 —— 它只在过关时露个脸，不留存货
    // 不用触发器，所以帽子身上不需要任何 Collider（有挡路的反而会把骰子弹开）
    IEnumerator FallAndDestroy(GameObject die)
    {
        yield return new WaitForSeconds(fallCatchTime);
        if (die == null) yield break;

        diceCollected++;
        Destroy(die);
        if (countText != null) countText.text = (startDice + diceCollected).ToString();
    }
}
