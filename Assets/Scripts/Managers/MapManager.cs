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
    //筹码
    public Chips playerChips;
    public Chips enemyChips;
    public PotArea potArea;
    public SidePotArea playerSidePot;

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
        // 预加载音频：提前触发 AudioManager.Awake 同步加载所有 AudioClip，
        // 避免首次进战斗节点时 Resources.Load 卡顿（重启项目第一次必现）
        var _ = AudioManager.Instance;
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


    // 开始一关
    public void StartLevel(int index)
    {
        // 商店/奖励关：不摆棋盘不抽手牌，直接进面板
        if (GameProgress.IsNonBattleNode()
            || GameProgress.currentNodeType == MapNodeType.Treasure)
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
        DeckManager.Instance.ResetForNewLevel();    // 销毁手牌实体、重新洗牌

        GameManager.Instance.LoadLevel(cfg);       // 设敌人筹码、清战利品
        EventBus.Publish(new LevelStartedEvent { levelIndex = index, isBoss = cfg.isBoss });
        BattleManager.Instance.StartLevel(cfg);    // 开打（GameLoop 的 Draw 阶段负责抽牌）
    }


    public void ClearSpawnedNonBattleObjects()
    {
        MapNodeType type = GameProgress.currentNodeType;

        switch (type)
        {
            case MapNodeType.Monster:
            case MapNodeType.Elite:
            case MapNodeType.Boss:
                // 战斗节点：不用清（牌/敌人由 BattleManager 管）
                break;

            case MapNodeType.Treasure:
                
                break;

            case MapNodeType.Shop:
                clearshopCards();
                // 清商品
                // DestroySpawned("Goods");
                break;

            case MapNodeType.Event:
                // 这两类当前没生成物，啥也不清
                break;
            case MapNodeType.Rest:
                if (RestManager.Instance != null)
                    RestManager.Instance.Despawn();
                break;
        }
    }
    //清理商品
    void clearshopCards()
    {
        StartCoroutine(ClearShopCardsRoutine());

    }

    IEnumerator ClearShopCardsRoutine()
    {
        if (ShopManager.Instance == null) yield break;
        var list = ShopManager.Instance.shopCards;
        if (list == null || list.Count == 0) yield break;

        Transform deckPos = DeckPile.Instance != null ? DeckPile.Instance.transform : null;
        if (deckPos == null) yield break;

        List<Coroutine> coroutines = new List<Coroutine>();
        foreach (var t in list)
        {
            if (t == null) continue;
            var flyable = t.GetComponent<IShopFlyable>();
            if (flyable != null)
                coroutines.Add(StartCoroutine(flyable.FlyStraight(t, new Vector3(15, 3, -1), 0.5f)));
        }
        list.Clear();

        foreach (var c in coroutines) if (c != null) yield return c;
        OpenMapUI();
    }


    // 商店/奖励关/休息（非战斗节点）
    void EnterNonBattleNode()
    {
        Debug.Log("[节点] 进入 " + GameProgress.currentNodeType);
        if (GameProgress.currentNodeType == MapNodeType.Treasure)
        {
            Chest();
        }
        else if (GameProgress.currentNodeType == MapNodeType.Shop)
        {
            Shopping();
        }
        else if (GameProgress.currentNodeType == MapNodeType.Rest)
        {
            Resting();
        }
    }

    // 商店刷 2 个互不相同的道具（本次两个不能重复；桌上已有同款不影响，照样会刷）
    void Shopping()
    {
        ShopManager.Instance.CardPoolInit();
        ShopManager.Instance.JokerPoolInit();
        if (CameraRig.Instance != null) CameraRig.Instance.JumpToIndex(1);
    }

    void Chest()
    {
        GameObject chestObj = Instantiate(chest, new Vector3(0, 7, -1), Quaternion.identity);
        spawnedObjects.Add(chestObj);
    }

    // 休息节点：左右放两个选项，点完由 RestManager 自己回地图
    void Resting()
    {
        if (RestManager.Instance != null)
            RestManager.Instance.SpawnRestOptions();
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
        
        //筹码区
        if(playerChips!=null) playerChips.ClearLedger();
        else foreach (Chips stack in FindObjectsOfType<Chips>())stack.ClearLedger();
        if(enemyChips!=null) enemyChips.ClearLedger();
        if(potArea!=null) potArea.ClearCoins();
        else FindObjectOfType<PotArea>().ClearCoins();
        if(playerSidePot!=null) playerSidePot.ClearLedger();
        else FindObjectOfType<SidePotArea>().ClearLedger();
        
        //手牌区
        DeckManager.Instance.ClearHandObjects();  // 删玩家手牌
        BattleView.Instance.ClearGhosts();        // 删虚影
        BattleView.Instance.ClearEnemyHand();
        // AwardHide();   // 按刚打完的节点给兽皮
        SaveManager.Instance.Save();   // 打完一关存档一次

        LevelConfig clearedCfg = null;
        if (database != null
            && GameProgress.currentLevel >= 0
            && GameProgress.currentLevel < database.levels.Count)
        {
            clearedCfg = database.levels[GameProgress.currentLevel];
        }

        if (clearedCfg != null && clearedCfg.isBoss)
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

    // 打开地图：优先用 3D 卷纸 RollingMap3D，没有则 fallback 到旧 MapScrollUI
    void OpenMapUI()
    {
        var rolling = FindObjectOfType<RollingMap3D>();
        if (rolling != null)
        {
            rolling.RollOut();
        }
        else
        {
            // var ui = FindObjectOfType<MapScrollUI>();
            // if (ui != null) ui.OpenMap();
            // else Debug.LogError("[MapManager] 场景里找不到 RollingMap3D 或 MapScrollUI");
        }
    }
    // ===== 3D 地图节点进入：levelIndex = 本章第几个战斗节点（非战斗节点为 -1） =====

    private const int LevelsPerSuit = 13;       // 每章在 LevelDatabase 占的关卡数
    private const int MaxNormalBattleIndex = 11; // 普通/精英战斗序号上限（12 留给 Boss）

    public void EnterNodeByType(MapNodeType type, int levelIndex)
    {
        GameProgress.currentNodeType = type;
        // ClearSpawnedNonBattleObjects();

        switch (type)
        {
            case MapNodeType.Monster:
            case MapNodeType.Elite:
                GameProgress.currentLevel =
                    GameProgress.currentSuit * LevelsPerSuit
                    + Mathf.Clamp(levelIndex, 0, MaxNormalBattleIndex);
                StartLevel(GameProgress.currentLevel);
                break;

            case MapNodeType.Boss:
                GameProgress.currentLevel =
                    GameProgress.currentSuit * LevelsPerSuit + (LevelsPerSuit - 1);
                StartLevel(GameProgress.currentLevel);
                break;

            case MapNodeType.Treasure:
            case MapNodeType.Shop:
                EnterNonBattleNode();
                break;
            case MapNodeType.Rest:
                EnterNonBattleNode();
                //
                break;

            case MapNodeType.Event:
                // TODO：事件玩法还没做，先直接回地图，保证整条流程能跑通
                Debug.Log($"[节点] {type} 内容未实现，暂时直接返回地图。");
                FinishNonBattleNode();
                break;
        }
    }
}
