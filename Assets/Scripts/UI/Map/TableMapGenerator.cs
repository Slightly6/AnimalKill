using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 杀戮尖塔式 3D 地图生成器：
/// 1) 每对相邻楼层先用"单调网格路径"DFS 建立覆盖所有节点的基础连接（数学保证不交叉、每个节点有进有出）
/// 2) 再随机遍历节点追加第 2、3 条出路（过出入度 + 交叉检测）
/// 3) 所有线只能连下一层 col-1/col/col+1；唯一例外是 Boss——任意列都允许长线汇入
/// 4) 随机列布局无法满足基础连接时，整张布局重抽，最多 layoutGenerationAttempts 次
/// </summary>
public class TableMapGenerator : Singleton<TableMapGenerator>
{
    [Serializable]
    public class NodeSpriteSetting
    {
        public MapNodeType type;       // 节点类型
        public Sprite sprite;          // 该类型在桌面上显示的图标
    }

    private struct ConnectionCell
    {
        public int fromIndex;
        public int toIndex;

        public ConnectionCell(int fromIndex, int toIndex)
        {
            this.fromIndex = fromIndex;
            this.toIndex = toIndex;
        }
    }

    private struct PathStep
    {
        public int fromIndex;
        public int toIndex;

        public PathStep(int fromIndex, int toIndex)
        {
            this.fromIndex = fromIndex;
            this.toIndex = toIndex;
        }
    }

    [Header("节点")]
    [SerializeField] private MapNode3D nodePrefab;                 // 节点预制体（带 BoxCollider + MapNode3D）
    [SerializeField] private Transform nodeRoot;                   // 生成节点的父物体（不拖自动建）
    [SerializeField] private List<NodeSpriteSetting> nodeSprites = new List<NodeSpriteSetting>();  // 各类型图标

    [Header("连线")]
    [SerializeField] private Transform lineRoot;                   // 生成连线的父物体（不拖自动建）
    [SerializeField] private Material lineMaterial;                // 连线材质
    [SerializeField] private Color lineColor = new Color(0.35f, 0.2f, 0.1f, 1f);  // 连线颜色
    [SerializeField, Min(0.001f)] private float lineWidth = 0.035f;  // 线宽
    [Tooltip("线头距节点中心的截短距离：线硬停在节点图标边缘（世界单位，0=不截短）")]
    [SerializeField, Min(0f)] private float lineEndpointInset = 0.35f;

    [Header("地图尺寸")]
    [SerializeField, Min(1f)] private float mapWidth = 8f;         // 纸面宽度（X）
    [SerializeField, Min(1f)] private float mapLength = 12f;       // 纸面长度（Z，行方向）
    [SerializeField, Min(0f)] private float horizontalPadding = 0.7f;  // 左右留白
    [SerializeField, Min(0f)] private float verticalPadding = 0.8f;    // 前后留白
    [SerializeField, Min(0f)] private float nodeHeight = 0.025f;   // 节点离地高度

    [Header("生成规则")]
    [SerializeField, Min(3)] private int floorCount = 15;          // 总行数（含第一行和 Boss 行）
    [SerializeField, Min(2)] private int columnCount = 7;          // 总列数（固定网格宽）
    [SerializeField, Min(1)] private int minNodesPerFloor = 3;     // 每行最少节点数
    [SerializeField, Min(1)] private int maxNodesPerFloor = 5;     // 每行最多节点数
    [SerializeField, Min(1)] private int layoutGenerationAttempts = 200;  // 布局重抽上限

    [Header("连线规则")]
    [SerializeField, Range(1, 3)] private int maxOutgoing = 3;     // 每节点最多出路（左/竖/右）
    [SerializeField, Min(1)] private int maxIncoming = 3;          // 每节点最多来路（Boss 不限）
    [Tooltip("追加第二条出路的概率")]
    [SerializeField, Range(0f, 1f)] private float secondEdgeChance = 0.35f;
    [Tooltip("已有第二条后，再追加第三条的概率")]
    [SerializeField, Range(0f, 1f)] private float thirdEdgeChance = 0.10f;
    [Tooltip("斜线权重：1=不偏向；越大斜线越多、直线越少（3≈斜线占75%）")]
    [SerializeField, Range(1f, 10f)] private float diagonalWeight = 3f;

    [Header("地图（运行时按行分组，不直接存档）")]
    public List<List<MapNodeData>> Map = new List<List<MapNodeData>>();  // Map[行][该行节点]

    [Header("随机种子")]
    [SerializeField] private bool randomizeSeed = true;            // true=每次新随机；false=用 seed 复现
    [SerializeField] private int seed = 12345;

    [Header("运行状态")]
    [SerializeField] private int currentNodeId = -1;               // 玩家当前所在节点（-1=还没选）

    public List<MapNodeData> Nodes { get; private set; } = new List<MapNodeData>();  // 扁平节点列表（存档用）

    private readonly Dictionary<int, MapNodeData> nodeLookup = new Dictionary<int, MapNodeData>();  // id→节点

    private System.Random random;

    // 改参数自动重新生成（仅运行时生效；非播放时请用右键菜单 Generate Map）
//     private void OnValidate()
//     {
//         // 夹取参数，防止非法值
//         floorCount = Mathf.Max(3, floorCount);
//         columnCount = Mathf.Max(2, columnCount);
//         minNodesPerFloor = Mathf.Clamp(minNodesPerFloor, 1, columnCount);
//         maxNodesPerFloor = Mathf.Clamp(Mathf.Max(minNodesPerFloor, maxNodesPerFloor), 1, columnCount);
//         layoutGenerationAttempts = Mathf.Max(1, layoutGenerationAttempts);
//         maxOutgoing = Mathf.Clamp(maxOutgoing, 1, 3);
//         maxIncoming = Mathf.Max(1, maxIncoming);
//         diagonalWeight = Mathf.Max(1f, diagonalWeight);
//         mapWidth = Mathf.Max(1f, mapWidth);
//         mapLength = Mathf.Max(1f, mapLength);
//         horizontalPadding = Mathf.Max(0f, horizontalPadding);
//         verticalPadding = Mathf.Max(0f, verticalPadding);
//         lineWidth = Mathf.Max(0.001f, lineWidth);

//         // 编辑器非播放模式：不建任何物体（OnValidate 里销毁/换父节点都被 Unity 禁止）
//         if (!Application.isPlaying)
//             return;

// #if UNITY_EDITOR
//         // 播放模式也要推迟：OnValidate 期间 SetParent 会触发 SendMessage，同样被禁
//         UnityEditor.EditorApplication.delayCall -= DelayedRegeneratePlaying;
//         UnityEditor.EditorApplication.delayCall += DelayedRegeneratePlaying;
// #else
//         RegeneratePlaying();
// #endif
//     }

// #if UNITY_EDITOR
//     private void DelayedRegeneratePlaying()
//     {
//         UnityEditor.EditorApplication.delayCall -= DelayedRegeneratePlaying;

//         // 延迟期间可能已经停止播放或物体被删
//         if (this == null || !Application.isPlaying) return;

//         RegeneratePlaying();
//     }
// #endif

//     private void RegeneratePlaying()
//     {
//         // 运行时改参数 = 调试行为，强制重新生成（无视已存地图）
//         GameProgress.mapGenerated = false;

//         ClearGeneratedObjects();
//         GenerateMap();
//     }

    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        ClearGeneratedObjects();

        bool hasExistingMap =
            GameProgress.mapGenerated &&
            GameProgress.mapSuit == GameProgress.currentSuit &&
            GameProgress.map != null &&
            GameProgress.map.Count > 0;

        if (hasExistingMap)
        {
            // 同一条命、同一章：读回已有地图
            Nodes = new List<MapNodeData>(GameProgress.map);
            RebuildLookup();
            currentNodeId = GameProgress.currentNodeId;
        }
        else
        {
            // randomizeSeed=true 每次新随机；false 用 Inspector 里的固定 seed（方便复现地图）
            seed = randomizeSeed
                ? UnityEngine.Random.Range(int.MinValue, int.MaxValue)
                : seed;

            random = new System.Random(seed);

            Nodes.Clear();
            nodeLookup.Clear();
            Map.Clear();
            currentNodeId = -1;

            // 布局重抽：列位置无法建立合法基础连接时整张重来
            if (!GenerateValidMapData())
            {
                Debug.LogError(
                    "无法生成满足列距离、出入度和不交叉要求的地图。"
                    + "请检查节点数量、列数和 maxIncoming/maxOutgoing 设置。",
                    this);
                return;
            }

            SaveMapProgress();
        }

        CreateNodeObjects();
        CreateConnectionLines();
        RefreshAvailability();
    }

    // 反复"生成节点 → 尝试基础连接"，直到成功或用尽次数；成功后再追加随机分叉
    private bool GenerateValidMapData()
    {
        int attempts = Mathf.Max(1, layoutGenerationAttempts);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            CreateNodeData();

            if (!CreateBaseConnections())
                continue;

            AddRandomBranches();
            return true;
        }

        return false;
    }

    void SaveMapProgress()
    {
        GameProgress.map = new List<MapNodeData>(Nodes);
        GameProgress.currentNodeId = currentNodeId;
        GameProgress.mapSuit = GameProgress.currentSuit;
        GameProgress.mapGenerated = true;
    }

    // 从 GameProgress.map 恢复数据后，重建 id→节点 字典 和 按行分组的 Map
    private void RebuildLookup()        //json不保证取出存档顺序
    {
        nodeLookup.Clear();
        Map.Clear();

        for (int i = 0; i < floorCount; i++)
            Map.Add(new List<MapNodeData>());

        foreach (MapNodeData node in Nodes)
        {
            if (node == null) continue;
            if (!nodeLookup.ContainsKey(node.id))
                nodeLookup.Add(node.id, node);

            if (node.floor >= 0 && node.floor < Map.Count)
                Map[node.floor].Add(node);
        }

        foreach (List<MapNodeData> row in Map)          //json不保证取出存档顺序，重新排一下
            row.Sort((a, b) => a.col.CompareTo(b.col));
    }

    private void CreateNodeData()
    {
        Nodes.Clear();
        nodeLookup.Clear();
        Map.Clear();

        for (int floor = 0; floor < floorCount; floor++)
            Map.Add(new List<MapNodeData>());

        int nextId = 0;
        int battleIndex = 0;   // 本章第几个战斗节点（决定 LevelDatabase 索引）

        for (int floor = 0; floor < floorCount; floor++)
        {
            int nodeCount = GetNodeCountForFloor(floor);
            List<int> columns = PickRandomColumns(nodeCount, floor);        //打乱删除多余，再排序
            columns.Sort();

            float normalizedZ = floorCount <= 1
                ? 0f
                : (float)floor / (floorCount - 1);

            float z = Mathf.Lerp(
                -mapLength * 0.5f + verticalPadding,
                mapLength * 0.5f - verticalPadding,
                normalizedZ
            );

            foreach (int col in columns)
            {
                float normalizedX = columnCount <= 1
                    ? 0.5f
                    : (float)col / (columnCount - 1);

                float x = Mathf.Lerp(
                    -mapWidth * 0.5f + horizontalPadding,
                    mapWidth * 0.5f - horizontalPadding,
                    normalizedX
                );

                MapNodeType nodeType = ChooseNodeType(floor);

                // 只有战斗类节点占 LevelDatabase 索引；商店/休息等为 -1
                int levelIndex = -1;
                if (nodeType == MapNodeType.Monster
                    || nodeType == MapNodeType.Elite
                    || nodeType == MapNodeType.Boss)
                {
                    levelIndex = battleIndex++;
                }

                MapNodeData node = new MapNodeData
                {
                    id = nextId++,
                    floor = floor,
                    col = col,
                    type = nodeType,
                    levelIndex = levelIndex,
                    localPosition = new Vector3(x, nodeHeight, z)
                };

                Nodes.Add(node);
                nodeLookup.Add(node.id, node);
                Map[floor].Add(node);
            }
        }
    }

    // 每行放几个节点：Boss 行固定 1 个；其他行在 min~max 之间随机
    // （Boss 允许任意列距离汇入，所以 Boss 前一行不再限制节点数量）
    private int GetNodeCountForFloor(int floor)
    {
        if (floor == floorCount - 1)
            return 1;

        int minCount = Mathf.Clamp(minNodesPerFloor, 1, columnCount);
        int maxCount = Mathf.Clamp(maxNodesPerFloor, minCount, columnCount);

        return RandomRangeInclusive(minCount, maxCount);
    }

    // 随机挑 count 个不同列；Boss 行固定正中间列
    private List<int> PickRandomColumns(int count, int floor)
    {
        var columns = new List<int>();

        if (floor == floorCount - 1)
        {
            columns.Add(columnCount / 2);
            return columns;
        }

        for (int col = 0; col < columnCount; col++)
            columns.Add(col);

        Shuffle(columns);

        if (columns.Count > count)
            columns.RemoveRange(count, columns.Count - count);

        return columns;
    }

    // 每对相邻楼层建立覆盖所有节点的基础连接
    private bool CreateBaseConnections()
    {
        ClearAllConnections();

        for (int floor = 0; floor < Map.Count - 1; floor++)
        {
            List<MapNodeData> sourceRow = Map[floor];
            List<MapNodeData> targetRow = Map[floor + 1];

            if (sourceRow.Count == 0 || targetRow.Count == 0)
                return false;

            if (!TryBuildNonCrossingBasePath(sourceRow, targetRow, out List<ConnectionCell> path))
                return false;

            foreach (ConnectionCell cell in path)
                AddConnection(sourceRow[cell.fromIndex], targetRow[cell.toIndex]);
        }

        return true;
    }

    private void ClearAllConnections()
    {
        foreach (MapNodeData node in Nodes)
        {
            if (node == null) continue;
            node.nextNodeIds.Clear();
            node.previousNodeIds.Clear();
        }
    }

    // 用单调网格路径覆盖两行的所有节点：
    // 网格 (i,j) = sourceRow[i]→targetRow[j]，从 (0,0) 走到 (m-1,n-1)，
    // 只前进不后退 → 连线左右顺序永远一致，不可能 X 交叉。
    private bool TryBuildNonCrossingBasePath(
        List<MapNodeData> sourceRow,
        List<MapNodeData> targetRow,
        out List<ConnectionCell> path)
    {
        path = new List<ConnectionCell>();

        if (sourceRow == null || targetRow == null) return false;
        if (sourceRow.Count == 0 || targetRow.Count == 0) return false;

        // 列数相差二直接终止换地图
        // 避免 DFS 为根本无解的布局白跑上万步
        if (!HasWindowCoverage(sourceRow, targetRow))
            return false;

        int[] outgoingCounts = new int[sourceRow.Count];
        int[] incomingCounts = new int[targetRow.Count];

        // 起点固定为 (0,0)：第一个节点必须先互连成路径的一端
        if (!CanUseBaseCell(sourceRow, targetRow, 0, 0, outgoingCounts, incomingCounts))
            return false;

        path.Add(new ConnectionCell(0, 0));
        outgoingCounts[0]++;
        incomingCounts[0]++;

        // 搜索上限按网格大小给（每行最多 5 个节点，1000 绰绰有余）
        int searchBudget = Mathf.Max(1000, (sourceRow.Count + 1) * (targetRow.Count + 1) * 16);

        // 整个 DFS 共用一个候选列表当栈用，递归时压入/弹回自己的候选，全程零 new
        var scratch = new List<PathStep>(8);

        bool found = SearchBasePath(
            sourceRow, targetRow,
            0, 0,
            outgoingCounts, incomingCounts,
            path, scratch, ref searchBudget);

        if (!found)
            path.Clear();

        return found;
    }

    // 必要条件预检：普通行每个节点在对方行 col±1 内至少要有一个邻居
    // （Boss 行收任意列，天然满足，跳过）
    private bool HasWindowCoverage(
        List<MapNodeData> sourceRow,
        List<MapNodeData> targetRow)
    {
        bool targetIsBoss = targetRow[0].floor == floorCount - 1;
        if (targetIsBoss) return true;

        foreach (MapNodeData from in sourceRow)
        {
            bool found = false;
            foreach (MapNodeData to in targetRow)
            {
                if (Mathf.Abs(to.col - from.col) <= 1) { found = true; break; }
            }
            if (!found) return false;
        }

        foreach (MapNodeData to in targetRow)
        {
            bool found = false;
            foreach (MapNodeData from in sourceRow)
            {
                if (Mathf.Abs(from.col - to.col) <= 1) { found = true; break; }
            }
            if (!found) return false;
        }

        return true;
    }

    // DFS：每步可 右移(i+1,汇合) / 上移(j+1,分叉) / 斜移(都+1)，随机顺序尝试
    // scratch 是所有递归层共用的候选栈：本层把候选压到末尾，逐层弹回，零 GC 分配
    private bool SearchBasePath(
        List<MapNodeData> sourceRow,
        List<MapNodeData> targetRow,
        int sourceIndex,
        int targetIndex,
        int[] outgoingCounts,
        int[] incomingCounts,
        List<ConnectionCell> path,
        List<PathStep> scratch,
        ref int searchBudget)
    {
        // 两个索引都到末尾 = 覆盖完成
        if (sourceIndex == sourceRow.Count - 1 &&
            targetIndex == targetRow.Count - 1)
        {
            return true;
        }

        if (searchBudget-- <= 0)
            return false;

        int baseCount = scratch.Count;   // 记住父层栈位置，本层只动 baseCount 之后的元素

        TryAddPathStep(sourceRow, targetRow, sourceIndex + 1, targetIndex, outgoingCounts, incomingCounts, scratch);
        TryAddPathStep(sourceRow, targetRow, sourceIndex, targetIndex + 1, outgoingCounts, incomingCounts, scratch);
        TryAddPathStep(sourceRow, targetRow, sourceIndex + 1, targetIndex + 1, outgoingCounts, incomingCounts, scratch);

        // 加权洗牌本层候选：斜线（两端列不同）权重 = diagonalWeight，直线权重 = 1
        // 注意：下面 while 从栈尾弹出尝试，所以这里把高权重的排到【末尾】= 先被尝试
        for (int i = scratch.Count - 1; i >= baseCount; i--)
        {
            float total = 0f;
            for (int j = baseCount; j <= i; j++)
                total += StepWeight(sourceRow, targetRow, scratch[j]);

            float roll = RandomValue() * total;
            int chosen = baseCount;
            for (int j = baseCount; j <= i; j++)
            {
                roll -= StepWeight(sourceRow, targetRow, scratch[j]);
                if (roll <= 0f) { chosen = j; break; }
            }
            (scratch[i], scratch[chosen]) = (scratch[chosen], scratch[i]);
        }

        // 从栈尾逐个弹出尝试；子递归会在 baseCount 之后继续压/弹，返回时栈恢复原状
        while (scratch.Count > baseCount)
        {
            int last = scratch.Count - 1;
            PathStep step = scratch[last];
            scratch.RemoveAt(last);

            outgoingCounts[step.fromIndex]++;
            incomingCounts[step.toIndex]++;
            path.Add(new ConnectionCell(step.fromIndex, step.toIndex));

            if (SearchBasePath(
                    sourceRow, targetRow,
                    step.fromIndex, step.toIndex,
                    outgoingCounts, incomingCounts,
                    path, scratch, ref searchBudget))
            {
                return true;
            }

            // 回溯
            path.RemoveAt(path.Count - 1);
            outgoingCounts[step.fromIndex]--;
            incomingCounts[step.toIndex]--;
        }

        return false;
    }

    private void TryAddPathStep(
        List<MapNodeData> sourceRow,
        List<MapNodeData> targetRow,
        int sourceIndex,
        int targetIndex,
        int[] outgoingCounts,
        int[] incomingCounts,
        List<PathStep> steps)
    {
        if (sourceIndex < 0 || sourceIndex >= sourceRow.Count) return;
        if (targetIndex < 0 || targetIndex >= targetRow.Count) return;

        if (!CanUseBaseCell(sourceRow, targetRow, sourceIndex, targetIndex, outgoingCounts, incomingCounts))
            return;

        steps.Add(new PathStep(sourceIndex, targetIndex));
    }

    // 这一步生成的线的权重：直线（同列）=1，斜线（列不同）=diagonalWeight
    private float StepWeight(
        List<MapNodeData> sourceRow,
        List<MapNodeData> targetRow,
        PathStep step)
    {
        return sourceRow[step.fromIndex].col == targetRow[step.toIndex].col ? 1f : diagonalWeight;
    }

    // 斜线优先的加权洗牌：直线目标权重1，斜线目标权重 diagonalWeight，排前的先被连
    private void WeightedShuffleTargets(List<MapNodeData> candidates, MapNodeData from)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            float total = 0f;
            for (int j = i; j < candidates.Count; j++)
                total += candidates[j].col == from.col ? 1f : diagonalWeight;

            float roll = RandomValue() * total;
            int chosen = i;
            for (int j = i; j < candidates.Count; j++)
            {
                roll -= candidates[j].col == from.col ? 1f : diagonalWeight;
                if (roll <= 0f) { chosen = j; break; }
            }
            (candidates[i], candidates[chosen]) = (candidates[chosen], candidates[i]);
        }
    }

    // DFS 用的合法性检查（用临时计数数组，不真正写连线）
    private bool CanUseBaseCell(
        List<MapNodeData> sourceRow,
        List<MapNodeData> targetRow,
        int sourceIndex,
        int targetIndex,
        int[] outgoingCounts,
        int[] incomingCounts)
    {
        MapNodeData from = sourceRow[sourceIndex];
        MapNodeData to = targetRow[targetIndex];

        bool isBoss = to.floor == floorCount - 1;

        // 普通层只许左邻/竖直/右邻；Boss 是唯一终点，允许任意列距离长线汇入
        if (!isBoss && Mathf.Abs(from.col - to.col) > 1)
            return false;

        if (outgoingCounts[sourceIndex] >= maxOutgoing)
            return false;

        if (!isBoss && incomingCounts[targetIndex] >= maxIncoming)
            return false;

        return true;
    }

    // 基础连接完成后，每行洗牌，给节点追加第 2、3 条出路
    private void AddRandomBranches()
    {
        for (int floor = 0; floor < Map.Count - 1; floor++)
        {
            List<MapNodeData> sourceRow = new List<MapNodeData>(Map[floor]);
            List<MapNodeData> targetRow = Map[floor + 1];

            Shuffle(sourceRow);

            foreach (MapNodeData from in sourceRow)
                AddBranchesForNode(from, targetRow);
        }
    }

    private void AddBranchesForNode(MapNodeData from, List<MapNodeData> targetRow)
    {
        int desiredCount = RollDesiredOutgoingCount();

        // 基础连接已达到随机目标就不再加
        if (from.nextNodeIds.Count >= desiredCount)
            return;

        var candidates = new List<MapNodeData>();

        foreach (MapNodeData target in targetRow)
        {
            bool toBoss = target.floor == floorCount - 1;

            // 普通目标只收 col±1；Boss 收任意列
            if (!toBoss && Mathf.Abs(target.col - from.col) > 1)
                continue;

            if (!CanAddConnection(from, target))
                continue;

            candidates.Add(target);
        }

        // 斜线优先的加权洗牌：直线目标权重1，斜线目标权重 diagonalWeight
        WeightedShuffleTargets(candidates, from);

        foreach (MapNodeData target in candidates)
        {
            if (from.nextNodeIds.Count >= desiredCount)
                break;

            // 加之前再次复检：前一条线可能改变了合法性
            if (CanAddConnection(from, target))
                AddConnection(from, target);
        }
    }

    // 随机这个节点"最终想到达几条出路"：基础1条，命中 second 变2，再命中 third 变3
    private int RollDesiredOutgoingCount()
    {
        int desiredCount = 1;

        if (maxOutgoing >= 2 && RandomValue() < secondEdgeChance)
        {
            desiredCount = 2;

            if (maxOutgoing >= 3 && RandomValue() < thirdEdgeChance)
                desiredCount = 3;
        }

        return desiredCount;
    }

    // 加线总闸：只许相邻层 + 列距离（Boss 豁免）+ 出入度 + 不许 X 交叉
    private bool CanAddConnection(MapNodeData from, MapNodeData to)
    {
        if (from == null || to == null) return false;
        if (to.floor != from.floor + 1) return false;

        bool isBoss = to.floor == floorCount - 1;

        // 普通层只许 ±1 列；Boss 允许任意列长线汇入
        if (!isBoss && Mathf.Abs(to.col - from.col) > 1)
            return false;

        if (from.nextNodeIds.Count >= maxOutgoing)
            return false;

        if (!isBoss && to.previousNodeIds.Count >= maxIncoming)
            return false;

        if (from.nextNodeIds.Contains(to.id))
            return false;

        if (from.floor < 0 || from.floor >= Map.Count)
            return false;

        foreach (MapNodeData otherFrom in Map[from.floor])
        {
            if (otherFrom == null) continue;

            foreach (int otherToId in otherFrom.nextNodeIds)
            {
                if (!nodeLookup.TryGetValue(otherToId, out MapNodeData otherTo)) continue;
                if (otherTo.floor != to.floor) continue;

                // 同一来源分叉、多个来源汇合同一目标 → 都不算交叉
                if (otherFrom.id == from.id) continue;
                if (otherTo.id == to.id) continue;

                // 左右顺序相反才是真正的 X 交叉
                bool crosses =
                    (otherFrom.col < from.col && otherTo.col > to.col) ||
                    (otherFrom.col > from.col && otherTo.col < to.col);

                if (crosses)
                    return false;
            }
        }

        return true;
    }

    // 双向写入，保证 next/previous 始终同步
    private void AddConnection(MapNodeData from, MapNodeData to)
    {
        if (from == null || to == null) return;
        if (from.nextNodeIds.Contains(to.id)) return;

        from.nextNodeIds.Add(to.id);

        if (!to.previousNodeIds.Contains(from.id))
            to.previousNodeIds.Add(from.id);
    }

    // Fisher-Yates 洗牌（用本地种子随机，保证存档可复现）
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // 取一个节点能通往的下一层节点（点击后高亮/生成下一关用）
    public List<MapNodeData> GetNextNodes(MapNodeData node)
    {
        var result = new List<MapNodeData>();
        if (node == null) return result;

        foreach (int id in node.nextNodeIds)
            if (nodeLookup.TryGetValue(id, out MapNodeData next))
                result.Add(next);

        return result;
    }

    private void CreateNodeObjects()// 生成节点物体
    {
        if (nodePrefab == null)
        {
            Debug.LogError("没有设置 Node Prefab。", this);
            return;
        }

        EnsureRoots();
        Quaternion rot = Quaternion.Euler(90f, 0f, 0f);   // SpriteRenderer 躺平
        Vector3 scale = new Vector3(0.3f, 0.3f, 0.3f);
        foreach (MapNodeData data in Nodes)
        {
            MapNode3D nodeView;
            
            if (nodePrefab != null)
            {
                nodeView = Instantiate(nodePrefab, nodeRoot);
            }
            else
            {
                // 没配预制体时运行时建一个最简节点（SpriteRenderer 躺平 + BoxCollider）
                nodeView = CreateRuntimeNode(nodeRoot);
            }

            nodeView.name = $"Node_{data.id}_{data.type}_Floor_{data.floor}";
            nodeView.transform.localPosition = data.localPosition;
            nodeView.transform.localRotation = rot;
            nodeView.transform.localScale = scale;

            nodeView.Setup(data, this, GetNodeSprite(data.type));
            data.view = nodeView;
        }
    }

    // 运行时占位节点：一个躺平的 SpriteRenderer + 碰撞盒，美术预制体就绪前先用它跑流程
    private MapNode3D CreateRuntimeNode(Transform parent)
    {
        GameObject go = new GameObject("RuntimeNode");
        go.transform.SetParent(parent, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 5;

        // X 旋转 90° 让竖面 Sprite 平躺在 XZ 纸面上
        // go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        // go.transform.localScale = Vector3.one * 0.6f;

        BoxCollider col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(1.2f, 1.2f, 0.1f);

        return go.AddComponent<MapNode3D>();
    }

    private Sprite placeholderSprite;

    private Sprite GetPlaceholderSprite()
    {
        if (placeholderSprite != null)
            return placeholderSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[size * size];
        float radius = size * 0.42f;
        float inner = size * 0.32f;
        float cx = size * 0.5f;
        float cy = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                Color32 c;
                if (dist <= radius)
                {
                    // 外圈白环，内部留空一点，看起来像筹码
                    bool ring = dist >= inner;
                    c = ring ? (Color32)Color.white : new Color32(255, 255, 255, 90);
                }
                else
                {
                    c = new Color32(0, 0, 0, 0);
                }

                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        placeholderSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        placeholderSprite.name = "PlaceholderNodeSprite";

        return placeholderSprite;
    }

    private void CreateConnectionLines()
    {
        EnsureRoots();

        foreach (MapNodeData from in Nodes)
        {
            foreach (int nextId in from.nextNodeIds)
            {
                if (!nodeLookup.TryGetValue(nextId, out MapNodeData to)) continue;
                CreateLine(from, to);
            }
        }
    }

    private void CreateLine(MapNodeData from, MapNodeData to)
    {
        // 用节点世界坐标，nodeRoot/lineRoot 位置不一致也不会错位
        if (from.view == null || to.view == null) return;

        GameObject lineObject = new GameObject($"Line_{from.id}_{to.id}");
        lineObject.transform.SetParent(lineRoot, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.startColor = lineColor;
        line.endColor = lineColor;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;

        if (lineMaterial != null)
        {
            line.sharedMaterial = lineMaterial;
        }
        else
        {
            // 没配材质时兜底用 Sprite 默认材质（能画线，粉色线说明需要补材质）
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
                line.sharedMaterial = new Material(spriteShader);
        }

        Vector3 start = from.view.transform.position;
        Vector3 end = to.view.transform.position;

        float drawnLength = 0f;   // 截短后实际画出的长度（传给虚线 shader 定疏密）

        {
            Vector3 flatDir = end - start;
            flatDir.y = 0f;
            float distance = flatDir.magnitude;

            if (distance > 0.0001f)
            {
                flatDir /= distance;
                // 超短线保护：两端合计最多吃掉 90% 线长，避免端点反向交叉
                float inset = Mathf.Min(lineEndpointInset, distance * 0.45f);
                start += flatDir * inset;
                end   -= flatDir * inset;
                drawnLength = distance - inset * 2f;
            }
            }
        start.y -= nodeHeight * 0.5f;   // 略低于节点，防止穿过图标
        end.y -= nodeHeight * 0.5f;

        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    // 点击节点入口：校验 → 标 visited → 存档 → 清节点卷起地图（后期在这里接 EnterNodeByType）
    public void TrySelectNode(int nodeId)
    {
        if (!nodeLookup.TryGetValue(nodeId, out MapNodeData selected))
            return;

        if (!selected.available)
        {
            Debug.Log($"节点 {nodeId} 当前不可进入。");
            return;
        }

        if (currentNodeId >= 0)
        {
            // 坏存档保护：当前节点 id 失效就重置回起点
            if (!nodeLookup.TryGetValue(currentNodeId, out MapNodeData current))
            {
                currentNodeId = -1;
                GameProgress.currentNodeId = -1;
            }
            else if (!current.nextNodeIds.Contains(nodeId))
            {
                return;
            }
        }

        selected.visited = true;
        currentNodeId = nodeId;
        GameProgress.currentNodeId = nodeId;

        RefreshAvailability();
        SaveMapProgress();

        Debug.Log($"进入节点：{selected.id}，类型：{selected.type}");

        StartCoroutine(EnterNodeRoutine(selected));
    }

    private System.Collections.IEnumerator EnterNodeRoutine(MapNodeData selected)
    {
        // RollUp 内部先清节点和线，再卷起纸张
        Debug.Log("[EnterNode] 1 开始 RollUp");
        RollingMap3D rolling = FindObjectOfType<RollingMap3D>();
        if (rolling != null)
            yield return rolling.RollUp();
        Debug.Log("[EnterNode] 2 RollUp 完成，准备进节点");
        GameProgress.mapOpen = false;
        if (CameraRig.Instance != null) CameraRig.Instance.JumpToIndex(0);   // 关图后相机回机位0（玩家第一视角，回战斗桌面）

        // 地图卷起后按节点类型进入对应关卡/玩法
        if (MapManager.Instance != null)
            MapManager.Instance.EnterNodeByType(selected.type, selected.levelIndex);
        Debug.Log("[EnterNode] 3 EnterNodeByType 完成");
    }

    // 刷新每个节点的可进入状态：初始开放第 0 行；之后只开放当前节点的下一层连线目标
    private void RefreshAvailability()
    {
        foreach (MapNodeData node in Nodes)
            node.available = false;

        if (currentNodeId >= 0 && !nodeLookup.ContainsKey(currentNodeId))
        {
            currentNodeId = -1;
            GameProgress.currentNodeId = -1;
        }

        if (currentNodeId < 0)
        {
            foreach (MapNodeData node in GetFloor(0))
                node.available = true;
        }
        else if (nodeLookup.TryGetValue(currentNodeId, out MapNodeData current))
        {
            foreach (int nextId in current.nextNodeIds)
            {
                if (nodeLookup.TryGetValue(nextId, out MapNodeData next))
                    next.available = true;
            }
        }

        foreach (MapNodeData node in Nodes)
        {
            if (node.view != null)
                node.view.Refresh(node.id == currentNodeId);
        }
    }

    // 按行分配节点类型：第0行固定战斗、Boss 前一行固定休息、最后一行 Boss，其余按概率
    private MapNodeType ChooseNodeType(int floor)
    {
        if (floor == floorCount - 1) return MapNodeType.Boss;
        if (floor == 0) return MapNodeType.Monster;
        if (floor == floorCount - 2) return MapNodeType.Rest;

        float value = RandomValue();

        if (value < 0.20f) return MapNodeType.Monster;
        if (value<0.66f) return MapNodeType.Rest;
        if (value < 0.99f) return MapNodeType.Shop;        // 12%
        return MapNodeType.Treasure;                        // 8%
    }

    private List<MapNodeData> GetFloor(int floor)
    {
        if (floor >= 0 && floor < Map.Count)
            return Map[floor];

        return new List<MapNodeData>();
    }

    private Sprite GetNodeSprite(MapNodeType type)
    {
        foreach (NodeSpriteSetting setting in nodeSprites)
        {
            if (setting.type == type && setting.sprite != null)
                return setting.sprite;
        }

        // 没配图片：全部用运行时生成的白色筹码圆片占位
        return GetPlaceholderSprite();
    }

    private void EnsureRoots()
    {
        if (nodeRoot == null)
            nodeRoot = CreateRoot("Generated Nodes");

        if (lineRoot == null)
            lineRoot = CreateRoot("Generated Lines");
    }

    private Transform CreateRoot(string rootName)
    {
        GameObject root = new GameObject(rootName);
        root.transform.SetParent(transform, false);
        return root.transform;
    }

    // 清掉所有已生成的节点和线（卷起地图/重新生成时调）
    public void ClearGeneratedObjects()
    {
        ClearRoot(nodeRoot);
        ClearRoot(lineRoot);
    }

    private void ClearRoot(Transform root)
    {
        if (root == null) return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private int RandomRangeInclusive(int min, int max)
    {
        if (max < min)
            max = min;

        return random.Next(min, max + 1);
    }

    private float RandomValue()
    {
        return (float)random.NextDouble();
    }
}
