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
    public GameObject slotPrefab;

    [Header("布局")]
    public float slotSpacing = 2.2f;
    public float previewRowY = 3.5f;
    public float enemyRowY = 1.5f;
    public float playerRowY = -1.5f;

    private List<CardSlot> playerSlots = new List<CardSlot>();
    private List<CardSlot> enemySlots = new List<CardSlot>();
    private List<CardSlot> enemyPreviewSlots = new List<CardSlot>();

    [Header("敌方卡牌")]
    public List<CardDataSO> enemyDeck = new List<CardDataSO>();
    public GameObject enemyCardPrefab;

    void Start()
    {
        playerSlots = GenerateSlots(playerRowY, true);
        enemySlots = GenerateSlots(enemyRowY, false);
        enemyPreviewSlots = GenerateSlots(previewRowY, false);

        EventBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
        EventBus.Subscribe<CardDiedEvent>(OnCardDied);
        EventBus.Subscribe<PhaseChangedEvent>(OnPhaseChanged);

        // 开局随机 2~5 张预告
        int n = Random.Range(2, 6);
        for (int i = 0; i < n; i++)
            CreateEnemyPreview(i);
    }

    List<CardSlot> GenerateSlots(float y, bool isPlayer)
    {
        List<CardSlot> list = new List<CardSlot>();
        float totalWidth = 4 * slotSpacing;
        float startX = -totalWidth / 2f;
        for (int i = 0; i < 5; i++)
        {
            GameObject go = Instantiate(slotPrefab, transform);
            go.transform.position = new Vector3(startX + i * slotSpacing, y, 0);
            go.name = (isPlayer ? "Player" : "Enemy") + "_Slot_" + i;
            CardSlot slot = go.GetComponent<CardSlot>();
            slot.laneIndex = i;
            slot.isPlayerSide = isPlayer;
            list.Add(slot);
        }
        return list;
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
        EventBus.Unsubscribe<CardDiedEvent>(OnCardDied);
        EventBus.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
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

    // 保留旧接口兼容
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

    public CardSlot FindEmptyPlayerSlot()
    {
        for (int i = 0; i < playerSlots.Count; i++)
            if (playerSlots[i].IsEmpty) return playerSlots[i];
        return null;
    }

    public bool HasEmptyPlayerSlot()
    {
        return FindEmptyPlayerSlot() != null;
    }

    // 兼容旧接口：在敌方当前排创建一张卡
    public Card CreateEnemyCardAt(int lane)
    {
        CardSlot slot = GetEnemySlot(lane);
        if (slot == null || !slot.IsEmpty) return null;
        if (enemyDeck.Count == 0 || enemyCardPrefab == null) return null;

        CardDataSO data = enemyDeck[Random.Range(0, enemyDeck.Count)].Clone();
        GameObject go = Instantiate(enemyCardPrefab, slot.transform);
        Card card = go.GetComponent<Card>();
        if (card == null) { Destroy(go); return null; }

        card.Init(data, false);
        card.SetFaceDown(false);   // 敌方卡翻开显示正面
        slot.PlaceCard(card);
        return card;
    }

    // ========== AI ==========

    // 在预出排创建一张卡（扣着）
    void CreateEnemyPreview(int lane)
    {
        CardSlot slot = GetPreviewSlot(lane);
        if (slot == null || !slot.IsEmpty) return;
        if (enemyDeck.Count == 0 || enemyCardPrefab == null) return;

        CardDataSO data = enemyDeck[Random.Range(0, enemyDeck.Count)].Clone();
        GameObject go = Instantiate(enemyCardPrefab, slot.transform);
        Card card = go.GetComponent<Card>();
        if (card == null) { Destroy(go); return; }

        card.Init(data, false);
        card.SetFaceDown(false);   // 敌方卡翻开显示正面
        slot.PlaceCard(card);
    }

    // ========== 回合推进 ==========

    void OnPhaseChanged(PhaseChangedEvent e)
    {
        // 敌方回合结束时，随机补 2~5 张预告
        if (e.phase == TurnPhase.End && !e.isPlayerTurn)
        {
            int n = Random.Range(2, 6);
            int added = 0;
            for (int i = 0; i < 5 && added < n; i++)
            {
                if (GetPreviewSlot(i).IsEmpty)
                {
                    CreateEnemyPreview(i);
                    added++;
                }
            }
        }
    }

    // 预出排下移到当前排（敌方出牌阶段调用）
    public void MovePreviewToCurrent()
    {
        for (int i = 0; i < 5; i++)
        {
            CardSlot preview = GetPreviewSlot(i);
            CardSlot current = GetEnemySlot(i);

            if (preview.IsEmpty) continue;

            Card card = preview.CurrentCard;
            preview.RemoveCard();

            if (!current.IsEmpty)
            {
                // 当前排有卡 → 旧的死了，预出卡顶上
                Card oldCard = current.CurrentCard;
                current.RemoveCard();
                Destroy(oldCard.gameObject);
            }

            card.transform.SetParent(current.transform);
            card.transform.localPosition = Vector3.zero;
            current.PlaceCard(card);

            Debug.Log("[棋盘] 敌方预出 " + card.CardName + " 移到第" + (i + 1) + "路");
        }
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
