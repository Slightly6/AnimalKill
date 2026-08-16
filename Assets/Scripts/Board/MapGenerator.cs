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

    [Header("关卡数据库（52关）")]
    public LevelDatabase database;

    [Header("生成参数")]
    public int suitCount = 4;         // 花色数（先测结构可改成 1）
    public int rankCount = 13;        // 每花色点数

    [Header("布局（世界坐标，自下而上）")]
    public float startY = -4f;        // 最下面 A 的 y
    public float rowGap = 0.35f;      // 每横排往上走多少
    public float nodeGapX = 1.2f;     // 同一排节点横向间距

    [Header("连线（可选，没材质先留空）")]
    public Material lineMaterial;

    void Start()
    {
        if (!GameProgress.mapGenerated)
        {
            GenerateMapData();
            GameProgress.mapGenerated = true;
        }
        BuildNodes();
        DrawConnections();
    }

    // ========== 生成地图数据 ==========
    void GenerateMapData()
    {
        GameProgress.map.Clear();
        GameProgress.mapRow = 0;

        int row = 0;   // 横排，0 = 最下面，往上递增

        for (int suit = 0; suit < suitCount; suit++)
        {
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
        }

        Debug.Log("地图生成完成，共 " + row + " 排");
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
            MapNode node = go.GetComponent<MapNode>();
            if (node != null) node.Setup(data);
        }
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

    // ========== 连线：相邻两排全连接 ==========
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
            for (int i = 0; i < GameProgress.map.Count; i++)
            {
                MapNodeData a = GameProgress.map[i];
                if (a.row != r) continue;

                for (int j = 0; j < GameProgress.map.Count; j++)
                {
                    MapNodeData b = GameProgress.map[j];
                    if (b.row != r + 1) continue;

                    Vector3 pa = NodePosition(a.row, a.col, CountInRow(a.row));
                    Vector3 pb = NodePosition(b.row, b.col, CountInRow(b.row));
                    DrawLine(pa, pb);
                }
            }
        }
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
