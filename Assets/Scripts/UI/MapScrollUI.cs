using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 卷轴地图 UI（整条长卷轴版）。
///
/// 层级：
/// MapScrollRoot（铺满全屏：深色背景 + CanvasGroup + RectMask2D；本身不动，负责裁剪/显隐/挡点击）
/// └── ScrollPanel（宽固定、高=地图全长=上杆+纸+下杆；锚在 Root 底部中心，掉落/滚动/飞走都移动它）
///     ├── TopRod    （上杆，地图最上端 = Boss 端）
///     ├── MapPaper  （牛皮纸：贴图 + ScrollParchment 材质；节点和连线全挂在纸上）
///     └── BottomRod （下杆，地图最下端 = 第一关 A 端）
///
/// 坐标约定（ScrollPanel 锚在 Root 底部中心，pivot 也在底部中心，anchoredPosition.y）：
///   y = 0                → BottomRod 正好贴屏幕底（掉落后的静止位，看到第一关）
///   y = -maxMove         → TopRod 正好贴屏幕顶（滚到 Boss）
///   y = 屏幕高 + 余量     → 整条在屏幕上方外（隐藏/掉落起点/关闭终点）
///
/// 行为：
///   按 Tab 打开：整条从屏幕上方外掉落，BottomRod 砸到屏幕底部 EaseOutBounce 弹一下后静止。
///   查看中：滚轮直接上下移动整条 ScrollPanel——纸/纹路/节点/线/两根杆一体移动。
///   再按 Tab：整条向上飞出屏幕（打开的逆过程）。
/// </summary>
public class MapScrollUI : MonoBehaviour
{
    [Header("=== 结构引用（Setup 自动赋值）===")]
    public RectTransform scrollPanel;        // 整条长卷轴（移动它）
    public LayoutElement paperLayout;        // MapPaper 上的 LayoutElement（按节点排数改高度）
    public RectTransform paper;              // 牛皮纸物体（节点/连线的父节点）

    [Header("=== 节点 ===")]
    public GameObject nodePrefab;            // 带 Image+Button+UIMapNode；不拖则代码建默认色块
    public Sprite nodeSprite;               // 默认节点底图
    public Sprite battleSprite, bossSprite, shopSprite, upgradeSprite, extraSprite, chestSprite;

    [Header("=== 连线 ===")]
    public Color lineColor = new Color(0.95f, 0.82f, 0.35f, 1f);   // 亮金线
    public float lineThickness = 5f;

    [Header("=== 布局（UI 像素）===")]
    public float panelWidth = 600f;          // 卷轴宽度（杆宽；纸每侧再内缩 paperInset）
    public float paperInset = 24f;
    public float rowGap = 90f;               // 节点排间距，同时决定纸张高度
    public float nodeGapX = 130f;
    public float nodeSize = 40f;
    public float topPadding = 60f;           // 纸顶（Boss）留白
    public float bottomPadding = 90f;        // 纸底（第一关）留白
    public float bottomGap = 0f;             // 落定后 BottomRod 离屏幕底的间隙（0=正好贴底）

    [Header("=== 掉出/收起动画 ===")]
    public float dropDuration = 0.9f;        // 从上掉落 + 落地弹一下
    public float closeDuration = 0.55f;      // 向上飞出
    public float offScreenMargin = 120f;     // 隐藏位置比屏幕顶再多出多少

    [Header("=== 滚轮（归一化，不受设备/分辨率影响）===")]
    [Range(0.05f, 1f)]
    public float wheelStep = 10f;           // 滚轮一齿翻多少：0.4=一屏的40%（约2~3齿一屏）
    [Range(1f, 30f)]
    public float wheelSmooth = 14f;          // 滚动插值速度，越大越跟手、越小越绵
    public float rubberPixels = 260f;        // 边界橡皮筋：每"齿越界量"最多拉出多少像素
    public float rubberMax = 110f;           // 橡皮筋最多拉出多少像素

    [Header("=== 按键 ===")]
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("=== 地图生成（一般不用动）===")]
    public int rankCount = 13;        // 每章点数（A~K，默认 13）

    private bool isOpen = false;
    private bool isAnimating = false;
    private int currentMaxRow = 0;    // 本次地图最大排数，算纸高和节点 y 用
    private float paperHeight = 0f;  // 按节点排数算出的纸高
    private float visibleH = 1080f;  // Root（屏幕）高度，掉落/滚动范围基准
    private float maxMove = 0f;      // 卷轴可上移距离 = panelH - visibleH
    private float hiddenY = 0f;      // 整条在屏幕上方外时的 y
    private Sprite whitePixel;
    private CanvasGroup cg;
    private Canvas rootCanvas;
    private int lastScreenW, lastScreenH;

    // 滚轮状态：norm 0=第一关(底)，1=Boss(顶)；panel.y = -norm*maxMove
    private float wheelTargetNorm = 0f;
    private float wheelCurrentNorm = 0f;
    private float overScroll = 0f;   // 边界越界量（单位：齿*step），做橡皮筋视觉
    private float currentY = 0f;     // panel 当前 y（含橡皮筋偏移），动画外每帧平滑写入

    void Start()
    {
        // Root 自己保持 active（否则 Update 不会被调用），显隐交给 CanvasGroup。
        cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        rootCanvas = GetComponentInParent<Canvas>();

        var rootRt = (RectTransform)transform;
        visibleH = rootRt.rect.height > 1f ? rootRt.rect.height : 1080f;

        if (scrollPanel != null)
        {
            // 宽度保险（Setup 已设，重跑/旧数据时兜底）
            scrollPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth);
            if (paperLayout != null) paperLayout.preferredHeight = 0f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollPanel);

            hiddenY = visibleH + offScreenMargin;
            scrollPanel.anchoredPosition = new Vector2(0f, hiddenY);
        }

        lastScreenW = Screen.width;
        lastScreenH = Screen.height;
        HideRoot();
    }

    void Update()
    {
        // 窗口大小/分辨率变化：关闭状态下重算可视高、可移距离和隐藏位置
        // （打开状态不动，避免看一半变形；关闭再开就是新尺寸）
        if (!isOpen && !isAnimating &&
            (Screen.width != lastScreenW || Screen.height != lastScreenH))
        {
            var rootRt0 = (RectTransform)transform;
            visibleH = rootRt0.rect.height > 1f ? rootRt0.rect.height : 1080f;
            hiddenY = visibleH + offScreenMargin;
            if (scrollPanel != null)
                scrollPanel.anchoredPosition = new Vector2(0f, hiddenY);
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;
        }

        if (Input.GetKeyDown(toggleKey) && !isAnimating)
        {
            if (isOpen)
            {
                // 开局卷轴自动掉落、玩家还没选任何节点时（mapRow==0），
                // 必须点第一关节点进入战斗，不允许 Tab 关掉露出未初始化的空桌
                if (GameProgress.mapRow <= 0)
                {
                    Narrator.Say(SpeakTopic.MapMustPickFirst);
                    return;
                }
                StartCoroutine(CloseRoutine());
            }
            else StartCoroutine(OpenRoutine());
        }

        HandleCustomWheel();
    }

    // 自定义滚轮：归一化定步长，任何鼠标/触摸板/分辨率下一齿手感一致。
    // 直接移动 ScrollPanel：wheel>0（向上滚）→ norm 增大 → panel.y 减小 → 看到地图上方(Boss)。
    void HandleCustomWheel()
    {
        if (!isOpen || isAnimating || scrollPanel == null) return;

        float dt = Time.unscaledDeltaTime;
        float wheel = Input.mouseScrollDelta.y;
        float lerpK = Mathf.Clamp01(wheelSmooth * dt);

        if (Mathf.Abs(wheel) >= 0.01f)
        {
            // 鼠标必须悬在卷轴条上才滚（Root 是全屏遮罩，不能用它判定）
            Camera uiCam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(scrollPanel, Input.mousePosition, uiCam))
                return;

            float input = (wheel / 120f) * wheelStep;
            float next = wheelTargetNorm + input;

            if (next < 0f)
            {
                // 已在第一关还往下滚 → 累积橡皮筋
                if (wheelTargetNorm <= 0.0001f) overScroll += input;
                wheelTargetNorm = 0f;
            }
            else if (next > 1f)
            {
                // 已到 Boss 还往上滚 → 累积橡皮筋（input 为正，橡皮筋向上拉）
                if (wheelTargetNorm >= 0.9999f) overScroll += input;
                wheelTargetNorm = 1f;
            }
            else
            {
                wheelTargetNorm = next;
            }
            overScroll = Mathf.Clamp(overScroll, -rubberMax / rubberPixels, rubberMax / rubberPixels);
        }

        // 橡皮筋回弹（无输入也执行）
        overScroll = Mathf.Lerp(overScroll, 0f, lerpK);
        if (Mathf.Abs(overScroll) < 0.0002f) overScroll = 0f;

        float baseY = -wheelTargetNorm * maxMove;                 // 0（底部/第一关）→ -maxMove（顶部/Boss）
        // 橡皮筋方向与滚动方向相反（越界拉过头后弹回）：
        // 底部继续下滚(overScroll<0) → panel 往上拽(y+)；顶部继续上滚(overScroll>0) → panel 往下拽(y-)
        float elasticY = -overScroll * rubberPixels;
        float targetY = baseY + elasticY;

        currentY = Mathf.Lerp(currentY, targetY, lerpK);
        if (Mathf.Abs(currentY - targetY) < 0.05f) currentY = targetY;
        scrollPanel.anchoredPosition = new Vector2(0f, currentY);
    }

    // 外部调用入口（MapManager 过关/商店宝箱结束后调）：效果等同按 Tab 打开
    public void OpenMap()
    {
        if (!isOpen && !isAnimating) StartCoroutine(OpenRoutine());
    }

    // 外部调用入口（UIMapNode 选中节点后调）：效果等同按 Tab 关闭
    public void CloseMap()
    {
        if (isOpen && !isAnimating) StartCoroutine(CloseRoutine());
    }

    void HideRoot()
    {
        if (cg == null) return;
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    void ShowRoot()
    {
        if (cg == null) return;
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    // ========== 打开：生成数据/节点 → 纸撑成地图全长 → 整条从上方掉落（下杆砸屏幕底弹一下） ==========
    System.Collections.IEnumerator OpenRoutine()
    {
        isOpen = true;
        isAnimating = true;
        GameProgress.mapOpen = true;   // 立即锁：掉落动画期间也不让点战斗里的东西

        // 屏幕高/隐藏位按当前尺寸重算（首帧布局可能刚就绪）
        var rootRt = (RectTransform)transform;
        visibleH = rootRt.rect.height > 1f ? rootRt.rect.height : 1080f;
        hiddenY = visibleH + offScreenMargin;

        EnsureMapGenerated();
        BuildMap();

        // 纸张高度跟随节点排数：节点排到哪纸就有多长；
        // ScrollPanel 的 ContentSizeFitter 自动撑成 上杆30 + 纸 + 下杆30
        paperHeight = topPadding + currentMaxRow * rowGap + bottomPadding;
        paperLayout.preferredHeight = paperHeight;
        paperLayout.preferredWidth = panelWidth - paperInset * 2f;
        scrollPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth);

        ShowRoot();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollPanel);
        yield return null;                       // 等一帧让 ContentSizeFitter 把 panel 高度撑出来

        float panelH = scrollPanel.rect.height;
        maxMove = Mathf.Max(0f, panelH - visibleH);   // 整条能上移多少让 TopRod 碰到屏幕顶

        // 滚轮状态归零（第一关/底部），整条先放到屏幕顶外
        wheelTargetNorm = wheelCurrentNorm = 0f;
        overScroll = 0f;
        currentY = hiddenY;
        scrollPanel.anchoredPosition = new Vector2(0f, hiddenY);

        // 掉落：BottomRod 从屏幕顶外砸到屏幕底部（y=bottomGap），EaseOutBounce 落地弹两下
        // 用 unscaledDeltaTime：命中停顿（timeScale≈0.05）或暂停时卷轴动画照常播放
        float t = 0f;
        while (t < dropDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dropDuration);
            float e = EaseOutBounce(p);
            currentY = Mathf.LerpUnclamped(hiddenY, bottomGap, e);
            scrollPanel.anchoredPosition = new Vector2(0f, currentY);
            yield return null;
        }
        currentY = bottomGap;
        scrollPanel.anchoredPosition = new Vector2(0f, currentY);
        isAnimating = false;
    }

    // ========== 关闭：整条向上飞出屏幕（打开的逆过程，加速飞走） ==========
    System.Collections.IEnumerator CloseRoutine()
    {
        isAnimating = true;

        float t = 0f;
        while (t < closeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / closeDuration);
            float e = EaseInCubic(p);
            currentY = Mathf.LerpUnclamped(currentY, hiddenY, e);
            scrollPanel.anchoredPosition = new Vector2(0f, currentY);
            yield return null;
        }
        currentY = hiddenY;
        scrollPanel.anchoredPosition = new Vector2(0f, hiddenY);

        isOpen = false;
        isAnimating = false;
        wheelTargetNorm = wheelCurrentNorm = 0f;
        overScroll = 0f;
        GameProgress.mapOpen = false;   // 飞出动画结束才解锁，防止飞走途中误点
        HideRoot();
    }

    // ========== 地图数据生成（原 MapGenerator 逻辑）==========
    // 重复调安全：mapGenerated 标志位挡住二次生成，连线每次重算。

    void EnsureMapGenerated()
    {
        if (!GameProgress.mapGenerated || GameProgress.mapSuit != GameProgress.currentSuit)
        {
            GenerateMapData();
            GameProgress.mapGenerated = true;
            GameProgress.mapSuit = GameProgress.currentSuit;
        }
        ComputeConnections();

        currentMaxRow = 0;
        for (int i = 0; i < GameProgress.map.Count; i++)
            if (GameProgress.map[i].row > currentMaxRow) currentMaxRow = GameProgress.map[i].row;
    }

    // 生成当前章：13 必过关（A~K，K 是 Boss），必过关之间随机插分岔区
    void GenerateMapData()
    {
        GameProgress.map.Clear();
        GameProgress.mapRow = 0;

        int row = 0;
        int suit = GameProgress.currentSuit;   // 0=♠ 1=♥ 2=♦ 3=♣

        for (int rank = 0; rank < rankCount; rank++)
        {
            MapNodeData must = new MapNodeData();
            if (rank == rankCount - 1) must.type = NodeType.Boss;
            else must.type = NodeType.Battle;
            must.levelIndex = suit * rankCount + rank;
            must.row = row;
            must.col = 0;
            GameProgress.map.Add(must);
            row++;

            if (rank < rankCount - 1)
            {
                int startIndex = GameProgress.map.Count;
                int depth = Random.Range(1, 3);
                for (int d = 0; d < depth; d++)
                {
                    int count = Random.Range(2, 4);
                    for (int c = 0; c < count; c++)
                    {
                        MapNodeData node = new MapNodeData();
                        node.type = RandomBranchType();
                        node.levelIndex = -1;
                        node.row = row;
                        node.col = c;
                        GameProgress.map.Add(node);
                    }
                    row++;
                }

                if (!HasReward(startIndex))
                    GameProgress.map[startIndex].type = NodeType.Upgrade;
            }
        }

        Debug.Log("[卷轴地图] 第 " + (suit + 1) + " 章地图生成完成，共 " + row + " 排");
    }

    NodeType RandomBranchType()
    {
        float r = Random.value;
        if (r < 0.4f) return NodeType.Extra;
        if (r < 0.7f) return NodeType.Shop;
        return NodeType.Upgrade;
    }

    bool HasReward(int startIndex)
    {
        for (int i = startIndex; i < GameProgress.map.Count; i++)
            if (GameProgress.map[i].type == NodeType.Upgrade) return true;
        return false;
    }

    // 每个节点的 nextCols = 下一排能走到的列（分岔再汇聚，不全连）
    void ComputeConnections()
    {
        for (int i = 0; i < GameProgress.map.Count; i++)
        {
            if (GameProgress.map[i].nextCols == null) GameProgress.map[i].nextCols = new List<int>();
            else GameProgress.map[i].nextCols.Clear();
        }

        int maxRow = 0;
        for (int i = 0; i < GameProgress.map.Count; i++)
            if (GameProgress.map[i].row > maxRow) maxRow = GameProgress.map[i].row;

        for (int r = 0; r < maxRow; r++)
        {
            int countA = CountInRow(GameProgress.map, r);
            int countB = CountInRow(GameProgress.map, r + 1);

            if (countA == 1 || countB == 1)
            {
                // 必经点 ↔ 分岔层：全连（发散/汇聚）
                for (int i = 0; i < GameProgress.map.Count; i++)
                {
                    MapNodeData a = GameProgress.map[i];
                    if (a.row != r) continue;
                    for (int j = 0; j < GameProgress.map.Count; j++)
                    {
                        MapNodeData b = GameProgress.map[j];
                        if (b.row != r + 1) continue;
                        a.nextCols.Add(b.col);
                    }
                }
            }
            else
            {
                // 分岔层之间：就近稀疏连，保证不断链
                List<MapNodeData> rowA = new List<MapNodeData>();
                List<MapNodeData> rowB = new List<MapNodeData>();
                for (int i = 0; i < GameProgress.map.Count; i++)
                {
                    if (GameProgress.map[i].row == r) rowA.Add(GameProgress.map[i]);
                    if (GameProgress.map[i].row == r + 1) rowB.Add(GameProgress.map[i]);
                }

                bool[] aUsed = new bool[rowA.Count];
                for (int bi = 0; bi < rowB.Count; bi++)
                {
                    int bestA = 0, bestDist = 9999;
                    for (int ai = 0; ai < rowA.Count; ai++)
                    {
                        int d = Mathf.Abs(rowA[ai].col - rowB[bi].col);
                        if (d < bestDist) { bestDist = d; bestA = ai; }
                    }
                    rowA[bestA].nextCols.Add(rowB[bi].col);
                    aUsed[bestA] = true;
                }

                for (int ai = 0; ai < rowA.Count; ai++)
                {
                    if (aUsed[ai]) continue;
                    int bestB = 0, bestDist = 9999;
                    for (int bi = 0; bi < rowB.Count; bi++)
                    {
                        int d = Mathf.Abs(rowA[ai].col - rowB[bi].col);
                        if (d < bestDist) { bestDist = d; bestB = bi; }
                    }
                    rowA[ai].nextCols.Add(rowB[bestB].col);
                }
            }
        }
    }

    // ========== 根据 GameProgress.map 在纸上摆节点和连线 ==========
    void BuildMap()
    {
        for (int i = paper.childCount - 1; i >= 0; i--)
            Destroy(paper.GetChild(i).gameObject);

        var map = GameProgress.map;
        if (map == null || map.Count == 0)
        {
            Debug.LogError("[卷轴地图] GameProgress.map 为空");
            return;
        }

        if (whitePixel == null) whitePixel = CreateWhitePixel();

        BuildLines(map);

        for (int i = 0; i < map.Count; i++)
        {
            MapNodeData d = map[i];
            int rowCount = CountInRow(map, d.row);
            Vector2 pos = NodeToUIPos(d.row, d.col, rowCount);

            GameObject go = nodePrefab != null
                ? Instantiate(nodePrefab, paper)
                : BuildDefaultNode();

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, nodeSize);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nodeSize);

            UIMapNode uiNode = go.GetComponent<UIMapNode>();
            if (uiNode == null) uiNode = go.AddComponent<UIMapNode>();
            uiNode.battleSprite = battleSprite;
            uiNode.bossSprite = bossSprite;
            uiNode.shopSprite = shopSprite;
            uiNode.upgradeSprite = upgradeSprite;
            uiNode.extraSprite = extraSprite;
            uiNode.chestSprite = chestSprite;
            uiNode.Setup(d);
        }
    }

    GameObject BuildDefaultNode()
    {
        var go = new GameObject("Node", typeof(RectTransform), typeof(Image), typeof(Button));
        Image img = go.GetComponent<Image>();
        img.sprite = nodeSprite != null ? nodeSprite : null;
        if (nodeSprite == null) img.color = new Color(0.95f, 0.82f, 0.35f, 1f);  // 亮金，深色背景上显眼
        go.transform.SetParent(paper, false);
        return go;
    }

    void BuildLines(List<MapNodeData> map)
    {
        for (int i = 0; i < map.Count; i++)
        {
            MapNodeData a = map[i];
            if (a.nextCols == null) continue;

            int countA = CountInRow(map, a.row);
            for (int k = 0; k < a.nextCols.Count; k++)
            {
                MapNodeData b = FindNode(map, a.row + 1, a.nextCols[k]);
                if (b == null) continue;
                int countB = CountInRow(map, b.row);
                CreateLine(
                    NodeToUIPos(a.row, a.col, countA),
                    NodeToUIPos(b.row, b.col, countB));
            }
        }
    }

    void CreateLine(Vector2 a, Vector2 b)
    {
        var go = new GameObject("Line", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(paper, false);
        Image img = go.GetComponent<Image>();
        img.sprite = whitePixel;
        img.color = lineColor;
        img.raycastTarget = false;   // 线不挡节点点击

        RectTransform rt = go.GetComponent<RectTransform>();
        Vector2 delta = b - a;
        float dist = delta.magnitude;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, dist);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, lineThickness);
        rt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        rt.SetAsFirstSibling();
    }

    // row=0（A 关）在纸最下，row=maxRow（Boss）在纸最上。
    // 节点锚在纸的顶部（anchor 0.5,1），y 越负越往下。
    Vector2 NodeToUIPos(int row, int col, int rowCount)
    {
        float y = -(topPadding + (currentMaxRow - row) * rowGap);
        float x = (col - (rowCount - 1) / 2f) * nodeGapX;
        return new Vector2(x, y);
    }

    int CountInRow(List<MapNodeData> map, int row)
    {
        int n = 0;
        for (int i = 0; i < map.Count; i++)
            if (map[i].row == row) n++;
        return n;
    }

    MapNodeData FindNode(List<MapNodeData> map, int row, int col)
    {
        for (int i = 0; i < map.Count; i++)
            if (map[i].row == row && map[i].col == col) return map[i];
        return null;
    }

    // 运行时造 1x1 白像素，连线不用手动拖 sprite
    Sprite CreateWhitePixel()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    float EaseInCubic(float t) => t * t * t;

    // 落地弹地：砸到终点 → 回弹两下 → 落定
    float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f, d1 = 2.75f;
        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
        if (t < 2.5f / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        return n1 * (t -= 2.625f / d1) * t + 0.984375f;
    }
}
