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
        public static int playerChipsMax = 100;  // 玩家筹码上限（篝火加满后+5）
        public static int targetHandSize = 7;    // 目标手牌数（跨关继承），默认 7= 开局手牌数
        public static int dice = 0;              // 骰子（商店用，关结束 Pot 分账获得）
        public static int sidePotChips = 0;     // 边池筹码（跨关继承），默认 0= 开局边池
        public static bool cheatMode = false;    // 开挂模式（跨场景，勾 GameManager 上的开关）
        public static bool chipsInitialized = false;   // 筹码是否初始化过（第一关用开局筹码，之后跨关继承）
        public static List<CardDataSO> playerDeck = new List<CardDataSO>();   // 玩家牌组数据（跨关继承），空 = 用初始牌组

        // 地图相关（本局随机地图，跨场景保留）
        public static List<MapNodeData> map = new List<MapNodeData>();   // 当前章的地图
        public static int currentNodeId = -1;   // 当前要打的关卡index（0~51），默认 -1 = 第1关
        public static bool mapGenerated = false;
        public static int currentSuit = 0;     // 当前第几章（0=♠ 1=♥ 2=♦ 3=♣）
        public static int mapSuit = -1;        // 已生成的地图属于哪章（-1=还没生成）
        public static int hides = 0;                    // 兽皮数（跨关继承，奖励关献祭用）
        public static MapNodeType currentNodeType = MapNodeType.Monster;   // 当前进的哪种节点（跨场景）

        // 道具不再做跨场景/跨关记录：买下的道具就是留在 SampleScene 里的 TableItem 实体，
        // 数据挂在实体上，换关不重建，消耗品用完销毁实体即可。
        //
        // 跨"游戏会话"存档例外：读档后到 SampleScene.BeginRun 之间，用资产名名单临时桥接，
        // BeginRun 按名单恢复成实体后立即清空，之后一切以桌上实体为准。
        public static List<string> ownedItemNames = new List<string>();

        // 本局已觉醒的动物卡（存 CardDataSO 的资产名）。
        // 同一种动物牌组里有多张也共享觉醒；商店购买后加入，死亡/重开清空。
        public static HashSet<string> awakenedCardNames = new HashSet<string>();

        // ===== 卡牌强化（三选一）=====
        // 牌库里每一张都是原资产的克隆体（独立个体），强化只改克隆体，原资产永远只读。
        // 重开一局 Reset + 场景重载后，InitDeck 从原资产重新克隆，强化自然归零。
        public static Dictionary<string, List<string>> cardEnhancements = new Dictionary<string, List<string>>();   // 原资产名 → 已附加的效果名列表（存档用；运行时真源在克隆体的 effects 上）

        // 克隆一张卡成运行时独立个体（进牌库统一走这，防"强化写进资产、重开残留"）
        public static CardDataSO CloneCard(CardDataSO src)
        {
            if (src == null) return null;
            if (src.isRuntimeClone) return src;   // 已经是克隆体就复用，不套娃
            CardDataSO clone = Object.Instantiate(src);
            clone.name = src.name;                // 抹掉 "(Clone)" 后缀，觉醒等按资产名匹配的逻辑照常工作
            clone.isRuntimeClone = true;
            return clone;
        }

        // 给一张牌附加效果（强化三选一选中时调），一张牌最多 3 个效果
        public static void ApplyEnhancement(CardDataSO target, AbilityEffectSO effect)
        {
            if (target == null) return;

            // 防御：万一传入的是原资产（哪个进牌入口漏了克隆），现场克隆并替换 playerDeck 里的引用
            if (!target.isRuntimeClone)
            {
                CardDataSO clone = CloneCard(target);
                int idx = playerDeck.IndexOf(target);
                if (idx >= 0) playerDeck[idx] = clone;
                target = clone;
                Debug.LogWarning("[强化] 传入的是原资产，已现场克隆替换：" + target.name);
            }

            if (target.effects == null) target.effects = new List<AbilityEffectSO>();
            if (target.effects.Count >= 3)
            {
                Debug.Log("[强化] " + target.name + " 已有 3 个效果，达到上限");
                return;
            }

            if (effect == null)
            {
                Debug.Log("[强化] 效果池为空，本次只走选牌流程，不挂效果");
                return;
            }

            target.effects.Add(effect);
            if (!cardEnhancements.ContainsKey(target.name))
                cardEnhancements[target.name] = new List<string>();
            cardEnhancements[target.name].Add(effect.name);
            Debug.Log("[强化] " + target.name + " 获得效果：" + effect.name + "（" + target.effects.Count + "/3）");
        }


        // 这张卡的动物觉醒了没有
        public static bool IsCardAwakened(CardDataSO data)
        {
            return data != null && awakenedCardNames.Contains(data.name);
        }

        // 觉醒一只动物（返回 false=已经觉醒过）
        public static bool AwakenCard(CardDataSO data)
        {
            if (data == null) return false;
            return awakenedCardNames.Add(data.name);
        }

        // 卷轴地图是否打开：打开时锁住战斗场景一切玩家点击（出牌/道具/牌堆/铃/箱子/商店）
        public static bool mapOpen = false;

        // 关卡切换中（淡入淡出+新关卡布阵期间）：同样锁住战斗交互，由 FadeManager 上锁、
        // 战斗进入出牌阶段（或非战斗节点淡入完成）解锁
        public static bool transitioning = false;

        public static bool chipFly=false;

        // 关卡收尾期间（手牌飞回牌堆 + 强化三选一排版）：锁住所有卡牌交互
        public static bool levelEndBusy = false;

        // 战斗交互总锁：看地图 或 切关过渡 或 关卡收尾 时，一切玩家点击禁用
        public static bool InputLocked => mapOpen || transitioning || chipFly || levelEndBusy;

        // 重新开始一局（玩家输光后重开用）
        public static void Reset()
        {
            currentLevel = 0;
            playerChips = 100;
            playerChipsMax = 100;
            sidePotChips = 0;
            targetHandSize = 7;
            dice = 0;
            chipsInitialized = false;
            playerDeck = new List<CardDataSO>();
            map = new List<MapNodeData>();
            currentNodeId = -1;
            mapGenerated = false;
            currentSuit = 0;
            mapSuit = -1;
            hides = 0;
            currentNodeType = MapNodeType.Monster;
            ownedItemNames = new List<string>();
            awakenedCardNames = new HashSet<string>();
            cardEnhancements = new Dictionary<string, List<string>>();
            mapOpen = false;
            transitioning = false;
            chipFly=false;
        }

        //是不是不用战斗的节点（商店/奖励关：进去不摆棋盘、不抽手牌）
        public static bool IsNonBattleNode()
        {
            return currentNodeType == MapNodeType.Shop;
        }


    }