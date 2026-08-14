using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡流程总管。
/// 管关卡进度、生成地图、调度节点。
/// 流程：第1关直接进 → 过关 → 地图选节点 → 下一关 → ... → 第52关 boss → 胜利。
/// </summary>
public class MapManager : Singleton<MapManager>
{
    [Header("关卡数据库（52关大数组）")]
    public LevelDatabase database;

    [Header("地图节点预制体（可空，空则用占位方块）")]
    public GameObject mapNodePrefab;

    [Header("地图布局")]
    public Vector2 battleNodePos = new Vector2(0, 3f);   // 战斗节点位置
    public float upgradeSpacing = 2.5f;                  // 升级节点间距

    // 当前打到第几关 0~51
    public int CurrentLevel { get; private set; }

    // 当前地图上的节点
    private List<MapNodeObject> mapNodes = new List<MapNodeObject>();

    private void Start()
    {
        EventBus.Subscribe<LevelClearedEvent>(OnLevelCleared);
        StartCoroutine(BeginRun());
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<LevelClearedEvent>(OnLevelCleared);
    }

    // 开局：延迟一帧等所有 Manager 的 Start 跑完，再进第 1 关
    private IEnumerator BeginRun()
    {
        yield return null;
        CurrentLevel = 0;
        StartLevel(CurrentLevel);
    }

    // 进入一关
    private void StartLevel(int index)
    {
        if (database == null)
        {
            Debug.LogError("MapManager 没设置 LevelDatabase！");
            return;
        }
        if (index >= database.levels.Count)
        {
            Debug.LogError("关卡索引 " + index + " 超出数据库范围（共 " + database.levels.Count + " 关）");
            return;
        }

        LevelConfig cfg = database.levels[index];

        GameManager.Instance.LoadLevel(cfg);       // 设敌人筹码、清战利品
        BoardManager.Instance.ResetLevel(cfg);     // 清空敌方、重摆敌人
        DeckManager.Instance.SetupLevel(cfg);      // 补手牌、更新抽牌数
        EventBus.Publish(new LevelStartedEvent { levelIndex = index, isBoss = cfg.isBoss });
        BattleManager.Instance.StartLevel(cfg);    // 开打
    }

    // 过关了：判断是否打满 52 关
    private void OnLevelCleared(LevelClearedEvent e)
    {
        CurrentLevel++;

        if (CurrentLevel >= database.levels.Count)
        {
            // 打满 52 关 → 整局胜利
            GameManager.Instance.WinGame();
        }
        else
        {
            GenerateMap();
        }
    }

    // 生成地图：下一关的战斗节点 + 随机 1~2 个升级占位节点
    private void GenerateMap()
    {
        ClearMap();

        LevelConfig nextCfg = database.levels[CurrentLevel];
        NodeType battleType = nextCfg.isBoss ? NodeType.Boss : NodeType.Battle;
        CreateNode(battleType, CurrentLevel, battleNodePos);

        int upgradeCount = Random.Range(1, 3);
        float startX = -upgradeSpacing * (upgradeCount - 1) / 2f;
        for (int i = 0; i < upgradeCount; i++)
        {
            Vector2 pos = new Vector2(startX + i * upgradeSpacing, battleNodePos.y + 2f);
            CreateNode(NodeType.Upgrade, -1, pos);
        }
    }

    // 点击节点（MapNodeObject 转到这里）
    public void OnNodeClicked(MapNodeObject node)
    {
        if (node.type == NodeType.Battle || node.type == NodeType.Boss)
        {
            StartLevel(node.levelIndex);
            ClearMap();
        }
        else if (node.type == NodeType.Upgrade)
        {
            // 占位：升级内容后期用户自己设计
            Debug.Log("[地图] 升级节点（占位，后期设计）");
            Destroy(node.gameObject);
            mapNodes.Remove(node);
        }
    }

    // 创建一个地图节点
    private MapNodeObject CreateNode(NodeType type, int levelIndex, Vector2 pos)
    {
        GameObject go;
        if (mapNodePrefab != null)
        {
            go = Instantiate(mapNodePrefab, pos, Quaternion.identity);
        }
        else
        {
            go = new GameObject(type.ToString() + "_Node");
            go.transform.position = pos;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSquareSprite();
            sr.color = (type == NodeType.Battle || type == NodeType.Boss) ? Color.red : Color.green;
            sr.sortingOrder = 5;
        }

        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col == null) col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = Vector2.one;

        MapNodeObject node = go.GetComponent<MapNodeObject>();
        if (node == null) node = go.AddComponent<MapNodeObject>();
        node.type = type;
        node.levelIndex = levelIndex;
        mapNodes.Add(node);
        return node;
    }

    // 占位方块 Sprite
    private Sprite CreateSquareSprite()
    {
        return Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    // 清空地图节点
    private void ClearMap()
    {
        for (int i = 0; i < mapNodes.Count; i++)
        {
            if (mapNodes[i] != null) Destroy(mapNodes[i].gameObject);
        }
        mapNodes.Clear();
    }
}
