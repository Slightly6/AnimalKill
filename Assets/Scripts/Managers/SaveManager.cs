using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 存档系统。把 GameProgress 存成 JSON 字符串放进 PlayerPrefs。
/// 存档点：每过一关（MapManager.OnLevelCleared）、每次商店/宝箱结束（FinishNonBattleNode）。
/// 读档：主菜单「继续」调 Load() 把进度写回 GameProgress，再进游戏场景；
///       道具名单由 MapManager.BeginRun 恢复成桌上实体（ScriptableObject 引用无法直接 JSON 序列化，只存资产名）。
/// 牌组现在每局固定不变，不存档；以后献祭改牌组再补。
/// </summary>
public class SaveManager : Singleton<SaveManager>
{
    const string SAVE_KEY = "AnimalKill_Save";

    // 牌组槽位：一张卡 = 原资产名 + 附加效果名列表（克隆体不能 JSON 序列化，按名存，读档靠 CardRegistry 还原）
    [System.Serializable]
    public class CardSlotSave
    {
        public string baseName;
        public List<string> effectNames = new List<string>();
    }

    // 存档里要存的数据（[System.Serializable] 才能用 JsonUtility）
    [System.Serializable]
    public class SaveData
    {
        public int currentLevel;
        public int currentSuit;
        public int playerChips;
        public int playerChipsMax;
        public int sidePotChips;
        public int targetHandSize;
        public bool chipsInitialized;
        public int hides;
        public int currentNodeId;
        public bool mapGenerated;
        public int mapSuit;
        public int currentNodeType;                              // NodeType 转成 int 存
        public List<MapNodeData> map = new List<MapNodeData>();
        public List<string> ownedItemNames = new List<string>(); // 桌上道具资产名（跨会话恢复用）
        public List<CardSlotSave> deck = new List<CardSlotSave>();   // 牌组槽位（含强化效果）
        // public List<string> awakenedCardNames = new List<string>(); // 本局已觉醒动物（资产名）
    }

    // 存：把当前进度写进 PlayerPrefs
    public void Save()
    {
        SaveData data = new SaveData();
        data.currentLevel = GameProgress.currentLevel;
        data.currentSuit = GameProgress.currentSuit;
        data.targetHandSize = GameProgress.targetHandSize;
        data.playerChips = GameProgress.playerChips;
        data.playerChipsMax = GameProgress.playerChipsMax;
        data.sidePotChips = GameProgress.sidePotChips;
        data.chipsInitialized = GameProgress.chipsInitialized;
        data.hides = GameProgress.hides;
        data.currentNodeId = GameProgress.currentNodeId;
        data.mapGenerated = GameProgress.mapGenerated;
        data.mapSuit = GameProgress.mapSuit;
        data.currentNodeType = (int)GameProgress.currentNodeType;
        data.map = new List<MapNodeData>(GameProgress.map);

        // 牌组：逐张存"原资产名 + 效果名"（克隆体的 name 抹过后缀 = 原资产名）
        data.deck = new List<CardSlotSave>();
        if (GameProgress.playerDeck != null)
        {
            for (int i = 0; i < GameProgress.playerDeck.Count; i++)
            {
                CardDataSO c = GameProgress.playerDeck[i];
                if (c == null) continue;
                CardSlotSave slot = new CardSlotSave();
                slot.baseName = c.name;
                if (c.effects != null)
                {
                    for (int j = 0; j < c.effects.Count; j++)
                    {
                        if (c.effects[j] != null) slot.effectNames.Add(c.effects[j].name);
                    }
                }
                data.deck.Add(slot);
            }
        }

        // 道具：直接扫描场景里桌上的 TableItem 实体（实体是局内唯一真源），
        // 存 ScriptableObject 资产名（.asset 文件名），读档后由 MapManager 按名找回 prefab。
        TableItem[] tableItems = FindObjectsOfType<TableItem>();
        for (int i = 0; i < tableItems.Length; i++)
        {
            if (tableItems[i] != null && tableItems[i].itemData != null)
                data.ownedItemNames.Add(tableItems[i].itemData.name);
        }

        // 觉醒动物名单
        // data.awakenedCardNames = new List<string>(GameProgress.awakenedCardNames);

        PlayerPrefs.SetString(SAVE_KEY, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Debug.Log("[存档] 已保存，进度到第 " + GameProgress.currentLevel + " 关");
    }

    // 读：有存档就把进度写回 GameProgress，返回有没有读到
    public bool Load()
    {
        if (!HasSave()) return false;

        string json = PlayerPrefs.GetString(SAVE_KEY);
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        if (data == null) return false;

        GameProgress.currentLevel = data.currentLevel;
        GameProgress.currentSuit = data.currentSuit;
        GameProgress.sidePotChips = data.sidePotChips;
        GameProgress.playerChips = data.playerChips;
        if (data.playerChipsMax > 0) GameProgress.playerChipsMax = data.playerChipsMax;
        GameProgress.targetHandSize = data.targetHandSize;
        GameProgress.chipsInitialized = data.chipsInitialized;
        GameProgress.hides = data.hides;
        GameProgress.currentNodeId = data.currentNodeId;
        GameProgress.mapGenerated = data.mapGenerated;
        GameProgress.mapSuit = data.mapSuit;
        GameProgress.currentNodeType = (MapNodeType)data.currentNodeType;
        if (data.map != null) GameProgress.map = data.map;
        // 道具名单：BeginRun 进 SampleScene 后按名恢复成实体（新游戏/旧存档缺字段时为空）
        GameProgress.ownedItemNames = data.ownedItemNames ?? new List<string>();

        // 牌组重建：注册表按名查资产 → CloneCard 重建独立个体 → 按效果名挂强化
        // （进场景后 DeckManager.InitDeck 检测 playerDeck 非空会直接用，不再走初始牌组）
        GameProgress.playerDeck = new List<CardDataSO>();
        if (data.deck != null && data.deck.Count > 0)
        {
            CardRegistry registry = Resources.Load<CardRegistry>("CardRegistry");
            if (registry == null)
            {
                Debug.LogError("[存档] Resources 里找不到 CardRegistry，牌组无法重建！检查注册表资产是否放在 Resources 下");
            }
            else
            {
                for (int i = 0; i < data.deck.Count; i++)
                {
                    CardSlotSave slot = data.deck[i];
                    CardDataSO baseAsset = registry.GetCard(slot.baseName);
                    if (baseAsset == null)
                    {
                        Debug.LogWarning("[存档] 注册表里找不到卡：" + slot.baseName + "，这张丢了");
                        continue;
                    }
                    CardDataSO clone = GameProgress.CloneCard(baseAsset);
                    if (clone.effects == null) clone.effects = new List<AbilityEffectSO>();
                    for (int j = 0; j < slot.effectNames.Count; j++)
                    {
                        AbilityEffectSO fx = registry.GetEffect(slot.effectNames[j]);
                        if (fx != null) clone.effects.Add(fx);
                        else Debug.LogWarning("[存档] 注册表里找不到效果：" + slot.effectNames[j]);
                    }
                    GameProgress.playerDeck.Add(clone);
                }
                Debug.Log("[存档] 牌组已重建：" + GameProgress.playerDeck.Count + " 张（含强化效果）");
            }
        }
        // 旧存档没有牌组字段 → playerDeck 留空，InitDeck 走初始牌组，兼容不炸
        // // 觉醒名单恢复
        // GameProgress.awakenedCardNames = data.awakenedCardNames != null
        //     ? new HashSet<string>(data.awakenedCardNames)
        //     : new HashSet<string>();
        return true;
    }

    // 删存档（新游戏 / 重新开始用）
    public void Clear()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
    }

    // 有没有存档（主菜单判断「继续」按钮能不能点）
    public bool HasSave()
    {
        return PlayerPrefs.HasKey(SAVE_KEY);
    }
}
