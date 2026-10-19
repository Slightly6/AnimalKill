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

    // 本关从敌人池抽的 runtimeEnemy（由 EnterNodeByType 传入，StartLevel 用它替代 LevelConfig.enemyConfig）
    private EnemyConfig currentEnemyOverride;

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

        // 如果本关从敌人池抽到了 runtimeEnemy，克隆 cfg 替换 enemyConfig（不动原资产）
        if (currentEnemyOverride != null)
        {
            cfg = new LevelConfig
            {
                levelName = cfg.levelName,
                isBoss = cfg.isBoss,
                enemyConfig = currentEnemyOverride,
                enemyBonusPower = cfg.enemyBonusPower,
                enemyAwakened = cfg.enemyAwakened,
                enemyStartingChips = cfg.enemyStartingChips,
                initialHandSize = cfg.initialHandSize
            };
        }

        // 同场景换关：手动模拟原来切场景的销毁重建
        DeckManager.Instance.ResetForNewLevel();    // 销毁手牌实体、重新洗牌

        GameManager.Instance.LoadLevel(cfg);       // 设敌人筹码、清战利品
        JokerManager.Instance.NotifyNewLevel();     // 通知小丑换关（清战斗内状态）
        EventBus.Publish(new LevelStartedEvent { levelIndex = index, isBoss = cfg.isBoss });
        BattleManager.Instance.StartLevel(cfg);    // 开打（GameLoop 的 Draw 阶段负责抽牌）
    }


    IEnumerator ClearSpawnedNonBattleObjectsRoutine()
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
                // 帽子在外面（进过商店）就先完整演一遍原路返回，等它回到常驻位再开卷轴，避免相机立刻切走把回程盖掉
                if (TOPhat.Instance != null && TOPhat.Instance.IsOutside)
                {
                    TOPhat.Instance.ExitShop();
                    yield return TOPhat.Instance.WaitIdle();
                }
                SaveManager.Instance.Save();   // 买完/选完立即存，不必等下一关打完（桌上实体被扫进存档）
                Narrator.Hide();   // 收掉商店常驻的"持有骰子数"播报，别跟着飘到地图
                // 清商品
                // DestroySpawned("Goods");
                break;

            case MapNodeType.Event:
                // 轮盘赌离场：① 灭灯 + 轮盘、按钮回天上失活 ② 帽子归位 ③ 复用关卡结束的强化流程把攒的宝箱机会逐个开掉 ④ 开卷轴
                GameProgress.levelEndBusy = true;   // 收尾期间锁住卡牌交互
                if (RouletteDrop.Instance != null)
                {
                    Coroutine fly = RouletteDrop.Instance.FlyBack();
                    if (fly != null) yield return fly;
                }
                // 帽子（悬浮骰子用的）原路收回去，等它归位
                if (TOPhat.Instance != null && TOPhat.Instance.IsOutside)
                {
                    TOPhat.Instance.ExitShop();
                    yield return TOPhat.Instance.WaitIdle();
                }
                Narrator.Hide();   // 收掉常驻的宝箱数播报，别跟着飘到地图

                // 攒了几次机会就开几次：每次都是关卡结束那一套（带镶嵌的宝箱 + 抽牌堆飞三张）
                // 开完由 PlayEventRewards 自己开卷轴，所以这里直接 yield break，别让下面那句 OpenMapUI 抢跑
                PlayEventRewards(RouletteEventManager.Instance != null ? RouletteEventManager.Instance.TakeChances() : 0);
                yield break;
            case MapNodeType.Rest:
                if (RestManager.Instance != null)
                    RestManager.Instance.Despawn();
                break;
        }

        OpenMapUI();   // 收场全演完（商店=帽子回位、商品飞走）再开卷轴，回程不会被盖掉
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
        if (TOPhat.Instance != null) TOPhat.Instance.EnterShop();   // 帽子甩出来，悬浮卡冒骰子从它这儿出
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

    // ========== 商店买完 / 宝箱选完：不切场景，帽子原路回去后卷轴再掉下来选下一关 ==========
    // public void FinishNonBattleNode()
    // {
    //     StartCoroutine(FinishNonBattleNodeRoutine());
    // }

    public void FinishNonBattleNode()
    {
        StartCoroutine(ClearSpawnedNonBattleObjectsRoutine());
    }

    // 过关：K（章节 Boss）→ 解锁下一章 / 胜利；普通关 → 卷轴掉下来
    void OnLevelCleared(LevelClearedEvent e)
    {
        // 开挂「跳过此关」在商店里按：这是非战斗节点，没有战斗结算/强化三选一，
        // 按正常离场收场（商品清场 → 帽子原路返回 → 开卷轴），否则帽子会永远留在商店位
        if (GameProgress.currentNodeType == MapNodeType.Shop)
        {
            FinishNonBattleNode();
            return;
        }

        StartCoroutine(LevelClearRoutine());
    }

    System.Collections.IEnumerator LevelClearRoutine()
    {
        // 一进收尾就锁死所有卡牌交互：手牌飞回 + 强化三选一排版期间鼠标点啥都没用
        GameProgress.levelEndBusy = true;

        //筹码区
        // 我的筹码不还池：整个跑局都在同一场景，玩家筹码堆跨关保留原样，
        // 下一关 ChipsChangedEvent 报同样的数，SetCount 不会动它（不重掉一遍开场下筹码雨）
        if(enemyChips!=null) enemyChips.ClearLedger();
        if(potArea!=null) potArea.ClearCoins();
        else FindObjectOfType<PotArea>().ClearCoins();

        //手牌区：先全部飞回抽牌堆（演出），飞完再清虚影/敌人手牌，避免两套卡牌逻辑同时跑
        if (DeckManager.Instance != null)
            yield return DeckManager.Instance.FlyAllHandToDeck();
        BattleView.Instance.ClearGhosts();        // 删虚影
        BattleView.Instance.ClearEnemyHand();
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
                GameProgress.levelEndBusy = false;
                yield break;
            }
            GameProgress.currentSuit++;   // 解锁下一章（卷轴打开时会检测 suit 变化自动生成新章地图）
        }

        // 相机不动（留在战斗视角看宝箱砸桌开盖），宝箱开完盖 EnhanceManager 才切强化视角
        // EnhanceManager 在三张候选牌全部落桌后会把 levelEndBusy 解锁，恢复交互
        EnhanceManager.Instance.Show(AfterEnhance);   // 强化三选一：选完补存档再开卷轴
    }

    // 强化选完（或没得选）的回调：强化结果发生在过关存档之后，这里补一刀
    void AfterEnhance()
    {
        SaveManager.Instance.Save();
        OpenMapUI();
    }

    // 事件关离场奖励：直接复用关卡结束的强化三选一（EnhanceManager.Show）——
    // 一次机会 = 带镶嵌的宝箱从天上掉下来开盖 + 从抽牌堆飞三张候选牌，选完接着开下一次，全开完再开卷轴
    void PlayEventRewards(int remain)
    {
        if (remain <= 0)
        {
            GameProgress.levelEndBusy = false;   // 没有机会可开：解锁交互（有开的话由强化流程自己解锁）
            SaveManager.Instance.Save();
            OpenMapUI();
            return;
        }
        GameProgress.levelEndBusy = true;        // 这一轮开箱+飞牌期间继续锁交互
        EnhanceManager.Instance.Show(() => PlayEventRewards(remain - 1));
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

    public void EnterNodeByType(MapNodeType type, int levelIndex, EnemyConfig overrideEnemy = null)
    {
        GameProgress.currentNodeType = type;
        currentEnemyOverride = overrideEnemy;   // StartLevel 会用它替代 LevelConfig.enemyConfig
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
                // 轮盘赌：点事件节点后，轮盘砸下来落地，按钮组和轮盘同时从桌里冒出来；点颜色按钮开转
                Debug.Log("[节点] 进入事件关：轮盘赌");
                if (TOPhat.Instance != null) TOPhat.Instance.EnterShop();   // 复用商店帽子：悬浮按钮时从帽子里冒骰子
                if (RouletteDrop.Instance != null) RouletteDrop.Instance.Drop();
                // 敌人先报一次下注规则（3 骰子 / 否则 5 红筹码）
                if (RouletteEventManager.Instance != null) RouletteEventManager.Instance.AnnounceStake();
                break;
        }
    }
}
