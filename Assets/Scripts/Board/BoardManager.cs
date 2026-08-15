using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 5路 × 3排 = 15个槽位。
/// 上排：敌方预出（你能看到下一张牌）
/// 中排：敌方当前
/// 下排：玩家
/// </summary>
public class BoardManager : Singleton<BoardManager>
{
    [Header("槽位预制体（拖一个就行，剩下自动生成）")]
    public GameObject slotPrefab;           // 玩家排 + 敌方当前排
    public GameObject previewSlotPrefab;    // 敌方预出排（单独图，空则用 slotPrefab）

    [Header("布局")]
    public float slotSpacing = 2.2f;
    public float previewRowY = 3.5f;
    public float enemyRowY = 1.5f;
    public float playerRowY = -1.5f;

    private List<CardSlot> playerSlots = new List<CardSlot>();
    private List<CardSlot> enemySlots = new List<CardSlot>();
    private List<CardSlot> enemyPreviewSlots = new List<CardSlot>();

    [Header("敌方卡牌")]
    public GameObject enemyCardPrefab;

    private LevelConfig currentConfig;   // 当前关配置
    private int previewStuckTurns = 0;   // 预出排连续几回合没前移（被正式排顶住）

    void Start()
    {
        playerSlots = GenerateSlots(playerRowY, true, slotPrefab);
        enemySlots = GenerateSlots(enemyRowY, false, slotPrefab);
        enemyPreviewSlots = GenerateSlots(previewRowY, false, PreviewPrefab());

        EventBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
        EventBus.Subscribe<CardDiedEvent>(OnCardDied);
    }

    List<CardSlot> GenerateSlots(float y, bool isPlayer, GameObject prefab)
    {
        List<CardSlot> list = new List<CardSlot>();
        float totalWidth = 4 * slotSpacing;
        float startX = -totalWidth / 2f;
        for (int i = 0; i < 5; i++)
        {
            GameObject go = Instantiate(prefab, transform);
            go.transform.position = new Vector3(startX + i * slotSpacing, y, 0);
            go.name = (isPlayer ? "Player" : "Enemy") + "_Slot_" + i;
            CardSlot slot = go.GetComponent<CardSlot>();
            slot.laneIndex = i;
            slot.isPlayerSide = isPlayer;
            list.Add(slot);
        }
        return list;
    }

    // 预出排的槽位 prefab（没拖单独的图就用普通 slotPrefab）
    GameObject PreviewPrefab()
    {
        return previewSlotPrefab != null ? previewSlotPrefab : slotPrefab;
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
        EventBus.Unsubscribe<CardDiedEvent>(OnCardDied);
    }

    // ========== 每关重置 ==========

    // 进一关：清空敌方，按 cfg 重新摆敌人（玩家棋盘/手牌跨关保留）
    public void ResetLevel(LevelConfig cfg)
    {
        currentConfig = cfg;
        previewStuckTurns = 0;   // 新关卡重置卡住计数
        ClearEnemyBoard();

        if (cfg == null || cfg.enemyDeck.Count == 0) return;

        int n = Random.Range(cfg.minPreviewCards, cfg.maxPreviewCards + 1);
        List<int> lanes = ShuffledLanes();   // 打乱 0~4，预出牌随机落位
        for (int i = 0; i < 5 && i < n; i++)
        {
            CreateEnemyPreview(lanes[i]);
        }
    }

    // 清空敌方当前排 + 预出排
    void ClearEnemyBoard()
    {
        for (int i = 0; i < 5; i++)
        {
            CardSlot enemy = GetEnemySlot(i);
            CardSlot preview = GetPreviewSlot(i);
            if (enemy != null && enemy.CurrentCard != null) Destroy(enemy.CurrentCard.gameObject);
            if (preview != null && preview.CurrentCard != null) Destroy(preview.CurrentCard.gameObject);
            if (enemy != null) enemy.RemoveCard();
            if (preview != null) preview.RemoveCard();
        }
    }

    // 只清空预出排（卡住两回合后强制刷新用）
    void ClearPreview()
    {
        for (int i = 0; i < 5; i++)
        {
            CardSlot preview = GetPreviewSlot(i);
            if (preview != null && preview.CurrentCard != null) Destroy(preview.CurrentCard.gameObject);
            if (preview != null) preview.RemoveCard();
        }
    }

    // ========== 获取槽位 ==========

    public CardSlot GetPlayerSlot(int lane) { return GetFromList(playerSlots, lane); }
    public CardSlot GetEnemySlot(int lane) { return GetFromList(enemySlots, lane); }
    public CardSlot GetPreviewSlot(int lane) { return GetFromList(enemyPreviewSlots, lane); }

    CardSlot GetFromList(List<CardSlot> list, int lane)
    {
        if (lane < 0 || lane >= list.Count) return null;
        return list[lane];
    }

    public Card GetCardAt(int lane, bool isPlayerSide)
    {
        CardSlot slot = isPlayerSide ? GetPlayerSlot(lane) : GetEnemySlot(lane);
        return slot != null ? slot.CurrentCard : null;
    }

    public CardSlot GetSlot(int lane, bool isPlayerSide)
    {
        return isPlayerSide ? GetPlayerSlot(lane) : GetEnemySlot(lane);
    }

    // ========== 查找 ==========

    public CardSlot FindSlotOfCard(Card card)
    {
        List<CardSlot>[] all = { playerSlots, enemySlots, enemyPreviewSlots };
        for (int r = 0; r < all.Length; r++)
            for (int i = 0; i < all[r].Count; i++)
                if (all[r][i].CurrentCard == card) return all[r][i];
        return null;
    }

    // ========== AI ==========

    // 在预出排创建一张卡（扣着）
    void CreateEnemyPreview(int lane)
    {
        CardSlot slot = GetPreviewSlot(lane);
        if (slot == null || !slot.IsEmpty) return;
        if (currentConfig == null || currentConfig.enemyDeck.Count == 0 || enemyCardPrefab == null) return;

        CardDataSO data = currentConfig.enemyDeck[Random.Range(0, currentConfig.enemyDeck.Count)].Clone();
        if (currentConfig.enemyBonusPower != 0) data.AddBonus(currentConfig.enemyBonusPower);
        if (currentConfig.enemyAwakened) data.Awaken();

        GameObject go = Instantiate(enemyCardPrefab, slot.transform);
        Card card = go.GetComponent<Card>();
        if (card == null) { Destroy(go); return; }

        card.Init(data, false);
        card.SetFaceDown(false);   // 敌方卡翻开显示正面
        slot.PlaceCard(card);
    }

    // 打乱 0~4，返回随机顺序的通道列表（让预出牌随机落位，不按顺序）
    private List<int> ShuffledLanes()
    {
        List<int> lanes = new List<int> { 0, 1, 2, 3, 4 };
        for (int i = lanes.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = lanes[i];
            lanes[i] = lanes[j];
            lanes[j] = temp;
        }
        return lanes;
    }

    // ========== 回合推进 ==========

    // 预出排是否有牌（任意位置有牌返回 true）
    private bool HasPreviewCard()
    {
        for (int i = 0; i < 5; i++)
        {
            if (!GetPreviewSlot(i).IsEmpty) return true;
        }
        return false;
    }

    // 补预出排：没牌就补 n 张（随机落位），前移完立刻补
    private void RefillPreview()
    {
        if (currentConfig == null) return;
        if (HasPreviewCard()) return;   // 还有牌就不补

        int n = Random.Range(currentConfig.minRefillCards, currentConfig.maxRefillCards + 1);
        List<int> lanes = ShuffledLanes();   // 打乱 0~4，随机落位
        for (int i = 0; i < 5 && i < n; i++)
        {
            CreateEnemyPreview(lanes[i]);
        }
    }

    // 预出排下移到当前排（敌方出牌阶段调用）
    public void MovePreviewToCurrent()
    {
        bool movedAny = false;   // 这次有没有预出牌成功前移

        for (int i = 0; i < 5; i++)
        {
            CardSlot preview = GetPreviewSlot(i);
            CardSlot current = GetEnemySlot(i);

            if (preview.IsEmpty) continue;
            if (!current.IsEmpty) continue;   // 正式排有牌，这张被顶住，不前移

            Card card = preview.CurrentCard;
            preview.RemoveCard();
            card.transform.SetParent(current.transform);
            card.transform.localPosition = Vector3.zero;
            current.PlaceCard(card);
            movedAny = true;

            Debug.Log("[棋盘] 敌方预出 " + card.CardName + " 移到第" + (i + 1) + "路");
        }

        // 卡住检测：预出排有牌却一张都没前移，说明被正式排顶住了
        if (movedAny)
        {
            previewStuckTurns = 0;   // 有前移，正常，重置
        }
        else if (HasPreviewCard())
        {
            previewStuckTurns++;
            // 顶住两回合 → 清空预出排，下面 RefillPreview 会从空位置重新随机补，防止卡 bug
            if (previewStuckTurns >= 2)
            {
                ClearPreview();
                previewStuckTurns = 0;
            }
        }

        RefillPreview();   // 前移完（或卡住被清空后），从空位置随机补
    }

    // ========== 事件 ==========

    void OnCardPlayed(CardPlayedEvent e)
    {
        CardSlot slot = GetSlot(e.laneIndex, e.isPlayerSide);
        if (slot != null && slot.IsEmpty)
        {
            slot.PlaceCard(e.card);
            Debug.Log("[棋盘] " + e.card.CardName + " 放到第" + (e.laneIndex + 1) + "路");
        }
    }

    void OnCardDied(CardDiedEvent e)
    {
        CardSlot slot = FindSlotOfCard(e.card);
        if (slot != null) slot.RemoveCard();
    }
}
