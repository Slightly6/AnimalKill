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

    // 存档里要存的数据（[System.Serializable] 才能用 JsonUtility）
    [System.Serializable]
    public class SaveData
    {
        public int currentLevel;
        public int currentSuit;
        public int playerChips;
        public bool chipsInitialized;
        public int hides;
        public int mapRow;
        public int mapCol;
        public bool mapGenerated;
        public int mapSuit;
        public int currentNodeType;                              // NodeType 转成 int 存
        public List<MapNodeData> map = new List<MapNodeData>();
        public List<string> ownedItemNames = new List<string>(); // 桌上道具资产名（跨会话恢复用）
        public List<string> awakenedCardNames = new List<string>(); // 本局已觉醒动物（资产名）
    }

    // 存：把当前进度写进 PlayerPrefs
    public void Save()
    {
        SaveData data = new SaveData();
        data.currentLevel = GameProgress.currentLevel;
        data.currentSuit = GameProgress.currentSuit;
        data.playerChips = GameProgress.playerChips;
        data.chipsInitialized = GameProgress.chipsInitialized;
        data.hides = GameProgress.hides;
        data.mapRow = GameProgress.mapRow;
        data.mapCol = GameProgress.mapCol;
        data.mapGenerated = GameProgress.mapGenerated;
        data.mapSuit = GameProgress.mapSuit;
        data.currentNodeType = (int)GameProgress.currentNodeType;
        data.map = new List<MapNodeData>(GameProgress.map);

        // 道具：直接扫描场景里桌上的 TableItem 实体（实体是局内唯一真源），
        // 存 ScriptableObject 资产名（.asset 文件名），读档后由 MapManager 按名找回 prefab。
        TableItem[] tableItems = FindObjectsOfType<TableItem>();
        for (int i = 0; i < tableItems.Length; i++)
        {
            if (tableItems[i] != null && tableItems[i].itemData != null)
                data.ownedItemNames.Add(tableItems[i].itemData.name);
        }

        // 觉醒动物名单
        data.awakenedCardNames = new List<string>(GameProgress.awakenedCardNames);

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
        GameProgress.playerChips = data.playerChips;
        GameProgress.chipsInitialized = data.chipsInitialized;
        GameProgress.hides = data.hides;
        GameProgress.mapRow = data.mapRow;
        GameProgress.mapCol = data.mapCol;
        GameProgress.mapGenerated = data.mapGenerated;
        GameProgress.mapSuit = data.mapSuit;
        GameProgress.currentNodeType = (NodeType)data.currentNodeType;
        if (data.map != null) GameProgress.map = data.map;
        // 道具名单：BeginRun 进 SampleScene 后按名恢复成实体（新游戏/旧存档缺字段时为空）
        GameProgress.ownedItemNames = data.ownedItemNames ?? new List<string>();
        // 觉醒名单恢复
        GameProgress.awakenedCardNames = data.awakenedCardNames != null
            ? new HashSet<string>(data.awakenedCardNames)
            : new HashSet<string>();
        // 牌组不动（每局固定），DeckManager.InitDeck 会按初始牌组重建
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
