using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图生成器（挂在 Map 场景空物体上）。
/// 4 花色 × 13 必过关（A~K，K 是 Boss），自下而上。
/// 必过关之间随机插分岔区（每层 2~3 叉、1~3 层深）。
/// </summary>
public class MapGenerator : MonoBehaviour
{
    [Header("节点预制体（SpriteRenderer + BoxCollider2D + MapNode）")]
    public GameObject nodePrefab;

    [Header("各类型节点的图（不拖就用 prefab 自带的图）")]
    public Sprite battleSprite;   // 战斗（必过关）
    public Sprite bossSprite;     // Boss
    public Sprite shopSprite;     // 商店
    public Sprite upgradeSprite;  // 强化
    public Sprite extraSprite;    // 小关

    [Header("关卡数据库（52关）")]
    public LevelDatabase database;

    [Header("生成参数")]
    public int rankCount = 13;        // 每章点数（A~K）

    [Header("布局（世界坐标，自下而上）")]
    public float startY = -4f;        // 最下面 A 的 y
    public float rowGap = 0.7f;       // 每横排往上走多少（调大让竖线分散）
    public float nodeGapX = 1.5f;     // 同一排节点横向间距

    [Header("连线（可选，没材质先留空）")]
    public Material lineMaterial;

    void Start()
    {
        // 当前章还没生成过（或换了新章）→ 重新生成
        if (!GameProgress.mapGenerated || GameProgress.mapSuit != GameProgress.currentSuit)
        {
            GenerateMapData();
            GameProgress.mapGenerated = true;
            GameProgress.mapSuit = GameProgress.currentSuit;
        }
        BuildNodes();
        DrawConnections();
    }

    // ========== 生成地图数据（只生成当前章） ==========
    void GenerateMapData()
    {
        GameProgress.map.Clear();
        GameProgress.mapRow = 0;

        int row = 0;   // 横排，0 = 最下面，往上递增
        int suit = GameProgress.currentSuit;   // 当前章（0=♠ 1=♥ 2=♦ 3=♣）

        for (int rank = 0; rank < rankCount; rank++)
        {
            // 1. 必过关（A~K，K 是 Boss）
            MapNodeData must = new MapNodeData();
            if (rank == rankCount - 1) must.type = NodeType.Boss;
            else must.type = NodeType.Battle;
            must.levelIndex = suit * rankCount + rank;
            must.row = row;
            must.col = 0;
            GameProgress.map.Add(must);
            row++;

            // 2. 每个必过关后（K 除外）插一段分岔区
            if (rank < rankCount - 1)
            {
                int depth = Random.Range(1, 4);   // 分岔区 1~3 层
                for (int d = 0; d < depth; d++)
                {
                    int count = Random.Range(2, 4);   // 每层 2~3 个叉
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
            }
        }

        Debug.Log("第 " + (suit + 1) + " 章地图生成完成，共 " + row + " 排");
    }

    // 随机一个分岔节点类型：小关 / 商店 / 强化
    NodeType RandomBranchType()
    {
        float r = Random.value;
        if (r < 0.4f) return NodeType.Extra;
        if (r < 0.7f) return NodeType.Shop;
        return NodeType.Upgrade;
    }

    // ========== 摆节点 ==========
    void BuildNodes()
    {
        for (int i = 0; i < GameProgress.map.Count; i++)
        {
            MapNodeData data = GameProgress.map[i];
            int rowCount = CountInRow(data.row);
            Vector3 pos = NodePosition(data.row, data.col, rowCount);

            GameObject go = Instantiate(nodePrefab, pos, Quaternion.identity, transform);

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Sprite s = SpriteFor(data.type);
            if (sr != null && s != null) sr.sprite = s;

            MapNode node = go.GetComponent<MapNode>();
            if (node != null) node.Setup(data);
        }
    }

    // 按节点类型选一张图（没拖的就返回 null，保持 prefab 自带图）
    Sprite SpriteFor(NodeType type)
    {
        if (type == NodeType.Boss && bossSprite != null) return bossSprite;
        if (type == NodeType.Shop && shopSprite != null) return shopSprite;
        if (type == NodeType.Upgrade && upgradeSprite != null) return upgradeSprite;
        if (type == NodeType.Extra && extraSprite != null) return extraSprite;
        if (battleSprite != null) return battleSprite;
        return null;
    }

    int CountInRow(int row)
    {
        int n = 0;
        for (int i = 0; i < GameProgress.map.Count; i++)
        {
            if (GameProgress.map[i].row == row) n++;
        }
        return n;
    }

    // 坐标：自下而上，横向居中
    Vector3 NodePosition(int row, int col, int rowCount)
    {
        float y = startY + row * rowGap;              // 往上递增
        float x = (col - (rowCount - 1) / 2f) * nodeGapX;
        return new Vector3(x, y, 0);
    }

    // ========== 连线：像邪恶铭刻那样，分岔再汇聚，不全连 ==========
    void DrawConnections()
    {
        if (lineMaterial == null) return;

        int maxRow = 0;
        for (int i = 0; i < GameProgress.map.Count; i++)
        {
            if (GameProgress.map[i].row > maxRow) maxRow = GameProgress.map[i].row;
        }

        for (int r = 0; r < maxRow; r++)
        {
            int countA = CountInRow(r);       // 这一排几个节点
            int countB = CountInRow(r + 1);   // 下一排几个节点

            if (countA == 1 || countB == 1)
            {
                ConnectAll(r, countA, countB);       // 必经点 ↔ 分岔层：全连（发散/汇聚）
            }
            else
            {
                ConnectSparse(r, countA, countB);    // 分岔层 ↔ 分岔层：就近稀疏连
            }
        }
    }

    // 必经点（单节点）与分岔层之间：全连（发散/汇聚）
    void ConnectAll(int r, int countA, int countB)
    {
        for (int i = 0; i < GameProgress.map.Count; i++)
        {
            MapNodeData a = GameProgress.map[i];
            if (a.row != r) continue;

            for (int j = 0; j < GameProgress.map.Count; j++)
            {
                MapNodeData b = GameProgress.map[j];
                if (b.row != r + 1) continue;
                DrawOne(a, b, countA, countB);
            }
        }
    }

    // 分岔层之间：就近稀疏连，保证每个节点都有路（不断链）
    void ConnectSparse(int r, int countA, int countB)
    {
        // 收集这两排的节点
        List<MapNodeData> rowA = new List<MapNodeData>();
        List<MapNodeData> rowB = new List<MapNodeData>();
        for (int i = 0; i < GameProgress.map.Count; i++)
        {
            if (GameProgress.map[i].row == r) rowA.Add(GameProgress.map[i]);
            if (GameProgress.map[i].row == r + 1) rowB.Add(GameProgress.map[i]);
        }

        // 第一步：每个下游（rowB）就近连一个上游，保证下游不孤立
        bool[] aUsed = new bool[rowA.Count];
        for (int bi = 0; bi < rowB.Count; bi++)
        {
            int bestA = 0;
            int bestDist = 9999;
            for (int ai = 0; ai < rowA.Count; ai++)
            {
                int d = Mathf.Abs(rowA[ai].col - rowB[bi].col);
                if (d < bestDist) { bestDist = d; bestA = ai; }
            }
            DrawOne(rowA[bestA], rowB[bi], countA, countB);
            aUsed[bestA] = true;
        }

        // 第二步：还没连出去的上游，就近连一个下游（保证走到这还能继续）
        for (int ai = 0; ai < rowA.Count; ai++)
        {
            if (aUsed[ai]) continue;

            int bestB = 0;
            int bestDist = 9999;
            for (int bi = 0; bi < rowB.Count; bi++)
            {
                int d = Mathf.Abs(rowA[ai].col - rowB[bi].col);
                if (d < bestDist) { bestDist = d; bestB = bi; }
            }
            DrawOne(rowA[ai], rowB[bestB], countA, countB);
        }
    }

    void DrawOne(MapNodeData a, MapNodeData b, int countA, int countB)
    {
        Vector3 pa = NodePosition(a.row, a.col, countA);
        Vector3 pb = NodePosition(b.row, b.col, countB);
        DrawLine(pa, pb);
    }

    void DrawLine(Vector3 a, Vector3 b)
    {
        GameObject line = new GameObject("Line");
        line.transform.SetParent(transform);
        LineRenderer lr = line.AddComponent<LineRenderer>();
        lr.material = lineMaterial;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
    }
}
