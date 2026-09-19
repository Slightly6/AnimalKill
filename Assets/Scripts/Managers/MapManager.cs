using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 关卡流程总管（战斗场景里，全程不切场景）。
/// 开局：开始 GameProgress.currentLevel 关。
/// 过关：进度 +1 → 卷轴地图掉下来（同场景选关，不再 LoadScene）。
/// 选关：卷轴节点点击 → SelectNode → 重建桌面/棋盘/手牌 → 直接打下一关。
/// </summary>
public class MapManager : Singleton<MapManager>
{
    [Header("关卡数据库（52关）")]
    public LevelDatabase database;

    [Header("场景名（要和 Build Settings 里一致）")]
    public string mapSceneName = "Map";            // 地图场景（已不再加载，保留字段兼容）
    public string battleSceneName = "SampleScene"; // 战斗场景
    public GameObject chest;
    public List<GameObject> Goods = new List<GameObject>();

    // 非战斗节点刷出来的实体（商品/宝箱），离开该节点时统一清理
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();

    void Start()
    {
        EventBus.Subscribe<LevelClearedEvent>(OnLevelCleared);
        StartCoroutine(BeginRun());
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<LevelClearedEvent>(OnLevelCleared);
    }

    // 延迟一帧，等所有 Manager 初始化。
    // 开局不自动开打：先按存档恢复桌上道具（读档进入时），卷轴再掉下来，
    // 玩家点第一关节点后由 SelectNode → StartLevel 进战斗。
    IEnumerator BeginRun()
    {
        yield return null;
        RestoreItemsFromSave();
        OpenMapUI();
    }

    // 读档进入时按资产名名单恢复桌上道具实体。仅整局开局调这一次：
    // 局内换关从不重建，实体一直留在场景里（见 SelectNode）。
    void RestoreItemsFromSave()
    {
        var names = GameProgress.ownedItemNames;
        if (names == null || names.Count == 0) return;

        Transform[] targets = GoodsManager.Instance != null
            ? GoodsManager.Instance.dropTargets : null;
        if (targets == null || targets.Length == 0)
        {
            Debug.LogWarning("[MapManager] GoodsManager.dropTargets 没配置，存档道具无法恢复");
            GameProgress.ownedItemNames.Clear();
            return;
        }

        int restored = 0;
        for (int i = 0; i < names.Count && restored < targets.Length; i++)
        {
            string assetName = names[i];
            if (string.IsNullOrEmpty(assetName)) continue;

            GameObject prefab = FindGoodsPrefabByName(assetName);
            if (prefab == null)
            {
                Debug.LogWarning("[MapManager] 存档里的道具 '" + assetName + "' 找不到对应商品 prefab，跳过");
                continue;
            }

            ShopItemDataSO data = prefab.GetComponent<Goods>().itemData;   // 从 prefab 取数据
            Transform target = targets[restored];
            GameObject go = Instantiate(prefab, target.position, target.rotation);
            Destroy(go.GetComponent<Goods>());                 // 恢复的是玩家道具，不是商品
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            TableItem item = go.GetComponent<TableItem>();
            if (item == null) item = go.AddComponent<TableItem>();
            item.Setup(data);
            restored++;
        }

        // 桥接名单用完即清，之后以桌上实体为唯一真源
        names.Clear();
        Debug.Log("[MapManager] 存档道具恢复 " + restored + " 个");
    }

    // 按道具资产名找商品 prefab（Goods 列表里 itemData.name 匹配的那个）
    GameObject FindGoodsPrefabByName(string assetName)
    {
        for (int i = 0; i < Goods.Count; i++)
        {
            if (Goods[i] == null) continue;
            Goods g = Goods[i].GetComponent<Goods>();
            if (g != null && g.itemData != null && g.itemData.name == assetName)
                return Goods[i];
        }
        return null;
    }

    // ========== 卷轴选关入口（UIMapNode 点击调用，全程不切场景） ==========
    public void SelectNode(MapNodeData data)
    {
        if (data == null) return;

        // 前进到下一排，记住选了哪条线
        GameProgress.mapRow = data.row + 1;
        GameProgress.mapCol = data.col;
        GameProgress.currentNodeType = data.type;

        // 战斗/Boss 用节点自己的关卡索引；小关(Extra)复用上一关索引（levelIndex=-1）
        if (data.type == NodeType.Battle || data.type == NodeType.Boss)
            GameProgress.currentLevel = data.levelIndex;

        bool battleNode = data.type != NodeType.Shop
                       && data.type != NodeType.Upgrade
                       && data.type != NodeType.Chest;

        // 非战斗实体（未买商品、旧宝箱）先清掉
        ClearSpawnedNonBattleObjects();

        if (battleNode)
        {
            StartLevel(GameProgress.currentLevel);
            // 桌上道具就是场景里的实体，换关不动它们；
            // transitioning 保持 true，由 BattleManager 进入出牌阶段时解锁
        }
        else
        {
            EnterNonBattleNode();
            GameProgress.transitioning = false;   // 非战斗节点没有出牌阶段，直接可操作
        }
    }

    // 开始一关
    public void StartLevel(int index)
    {
        // 商店/奖励关：不摆棋盘不抽手牌，直接进面板
        if (GameProgress.IsNonBattleNode()
            || GameProgress.currentNodeType == NodeType.Chest)
        {
            EnterNonBattleNode();
            return;
        }

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

        // 同场景换关：手动模拟原来切场景的销毁重建
        BoardManager.Instance.ClearPlayerBoard();   // 清玩家槽里上一关的牌
        DeckManager.Instance.ResetForNewLevel();    // 销毁手牌实体、重新洗牌

        GameManager.Instance.LoadLevel(cfg);       // 设敌人筹码、清战利品
        BoardManager.Instance.ResetLevel(cfg);     // 清空敌方、重摆敌人
        DeckManager.Instance.SetupLevel(cfg);      // 补手牌
        EventBus.Publish(new LevelStartedEvent { levelIndex = index, isBoss = cfg.isBoss });
        BattleManager.Instance.StartLevel(cfg);    // 开打
    }

    // ========== 非战斗节点实体管理（商店商品/宝箱）==========

    // 离开商店/宝箱节点：销毁刷出来的实体（已经变成 TableItem 的保留——
    // 买下的道具是留在场景里的实体，不进任何持有列表，靠这个判断存活）
    void ClearSpawnedNonBattleObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            GameObject go = spawnedObjects[i];
            if (go == null) { spawnedObjects.RemoveAt(i); continue; }
            if (go.GetComponent<TableItem>() != null) { spawnedObjects.RemoveAt(i); continue; }  // 玩家道具保留
            Destroy(go);
        }
        spawnedObjects.Clear();
    }

    // 过关给兽皮：小关(Extra) 33% 掉 1 / 大关(Battle必过关) 稳定 1 / Boss 3
    void AwardHide()
    {
        NodeType t = GameProgress.currentNodeType;
        if (t == NodeType.Boss)
        {
            GameProgress.hides += 3;
        }
        else if (t == NodeType.Extra)
        {
            if (Random.value < 0.33f) GameProgress.hides += 1;
        }
        else if (t == NodeType.Battle)
        {
            GameProgress.hides += 1;
        }
        Debug.Log("[兽皮] 现在共 " + GameProgress.hides + " 片");
    }

    // 商店/奖励关（非战斗节点）
    void EnterNonBattleNode()
    {
        Debug.Log("[节点] 进入 " + GameProgress.currentNodeType);
        if (GameProgress.currentNodeType == NodeType.Chest
            || GameProgress.currentNodeType == NodeType.Upgrade)
        {
            Chest();
        }
        else if (GameProgress.currentNodeType == NodeType.Shop)
        {
            Shopping();
        }
    }

    // 商店刷 2 个互不相同的道具（本次两个不能重复；桌上已有同款不影响，照样会刷）
    void Shopping()
    {
        // 候选池：所有配了 itemData 的商品 prefab
        var pool = new List<GameObject>();
        for (int i = 0; i < Goods.Count; i++)
        {
            if (Goods[i] == null) continue;
            Goods g = Goods[i].GetComponent<Goods>();
            if (g != null && g.itemData != null) pool.Add(Goods[i]);
        }

        // Fisher-Yates 洗牌，取前 2 个（天然互不相同）
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            GameObject tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        int count = Mathf.Min(3, pool.Count);
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(pool[i], new Vector3(i * 2f-2, 7, -1), Quaternion.identity);
            spawnedObjects.Add(obj);
        }
    }

    void Chest()
    {
        GameObject chestObj = Instantiate(chest, new Vector3(0, 7, -1), Quaternion.identity);
        spawnedObjects.Add(chestObj);
    }

    // ========== 商店买完 / 宝箱选完：不切场景，卷轴重新掉下来选下一关 ==========
    public void FinishNonBattleNode()
    {
        ClearSpawnedNonBattleObjects();
        SaveManager.Instance.Save();   // 买完/选完立即存，不必等下一关打完（桌上实体被扫进存档）
        OpenMapUI();
    }

    // 过关：K（章节 Boss）→ 解锁下一章 / 胜利；普通关 → 卷轴掉下来
    void OnLevelCleared(LevelClearedEvent e)
    {
        AwardHide();   // 按刚打完的节点给兽皮
        SaveManager.Instance.Save();   // 打完一关存档一次

        int rank = GameProgress.currentLevel % 13;   // 0=A ... 12=K

        if (rank == 12)   // 打的是 K = 章节 Boss
        {
            if (GameProgress.currentSuit >= 3)   // 最后一章（♣）→ 整局胜利
            {
                GameManager.Instance.WinGame();
                return;
            }
            GameProgress.currentSuit++;   // 解锁下一章（卷轴打开时会检测 suit 变化自动生成新章地图）
        }

        OpenMapUI();   // 普通关/Boss章节末：同场景卷轴选关
    }

    // 打开卷轴地图（找场景里的 MapScrollUI）
    void OpenMapUI()
    {
        var ui = FindObjectOfType<MapScrollUI>();
        if (ui != null) ui.OpenMap();
        else Debug.LogError("[MapManager] 场景里找不到 MapScrollUI，无法打开地图");
    }
}
