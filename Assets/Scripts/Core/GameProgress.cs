using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 跨场景进度。静态变量不随场景切换销毁，整个游戏过程一直保留。
/// 存：当前关卡、玩家筹码、玩家牌组。
/// </summary>
    public static class GameProgress
    {
        public static int currentLevel = 0;      // 当前要打的关卡index（0~51），默认 0 = 第1关
        public static int playerChips = 100;     // 玩家筹码（跨关继承），默认 100= 开局筹码
        public static List<CardDataSO> playerDeck = new List<CardDataSO>();   // 玩家牌组数据（跨关继承），空 = 用初始牌组

        // 地图相关（本局随机地图，跨场景保留）
        public static List<MapNodeData> map = new List<MapNodeData>();   // 当前章的地图
        public static int mapRow = 0;          // 玩家当前要选第几横排
        public static bool mapGenerated = false;
        public static int currentSuit = 0;     // 当前第几章（0=♠ 1=♥ 2=♦ 3=♣）
        public static int mapSuit = -1;        // 已生成的地图属于哪章（-1=还没生成）

        // 重新开始一局（玩家输光后重开用）
        public static void Reset()
        {
            currentLevel = 0;
            playerChips = 100;
            playerDeck = new List<CardDataSO>();
            map = new List<MapNodeData>();
            mapRow = 0;
            mapGenerated = false;
            currentSuit = 0;
            mapSuit = -1;
        }
    }