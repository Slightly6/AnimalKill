using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理牌组和手牌。
/// 管洗牌、抽牌、手牌上限。
/// 每关 SetupLevel 从 LevelConfig 读配置，牌组跨关继承。
/// </summary>
public class DeckManager : Singleton<DeckManager>
{
    [Header("牌组配置")]
    public List<CardDataSO> deckCards = new List<CardDataSO>();  // 初始牌组

    [Header("手牌设置")]
    public int maxHandSize = 100;     // 手牌上限
    public int drawPerTurn = 1;      // 每回合抽几张（每关 SetupLevel 更新）

    [Header("卡牌预制体")]
    public GameObject cardPrefab;

    [Header("牌堆位置（卡从这里出生）")]
    public Transform deckPile;

    [Header("手牌父节点")]
    public Transform handPanel;

    // 抽牌堆和手牌
    private List<CardDataSO> drawPile = new List<CardDataSO>();// 抽牌堆
    public List<Card> HandCards { get; private set; } = new List<Card>();// 手牌
    private int drawsThisTurn = 0;   // 本回合已经抽了几张

    void Awake()
    {
        if (deckCards.Count == 0)
        {
            // ★ 加载 Resources/Cards/ 下所有 CardDataSO
            CardDataSO[] all = Resources.LoadAll<CardDataSO>("Data/Cards");
            deckCards.AddRange(all);

            Debug.Log("加载了 " + all.Length + " 张牌");
        }
    }
    private void Start()
    {
        InitDeck();
        EventBus.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
    }

    // 初始化牌组（整局只洗一次，跨关继承）
      private void InitDeck()
    {
      // 跨关继承：进度里已经有牌组就用它；没有（第一次）用 Inspector 的初始牌组
      if (GameProgress.playerDeck != null && GameProgress.playerDeck.Count > 0)
      {
          drawPile = new List<CardDataSO>(GameProgress.playerDeck);
      }
      else
      {
          drawPile = new List<CardDataSO>(deckCards);
          GameProgress.playerDeck = new List<CardDataSO>(deckCards);   // 存进进度，以后跨关继承
      }
      Shuffle(drawPile);
      Debug.Log("牌组初始化完成，共 " + drawPile.Count + " 张");
    }

    // 每关配置：更新每回合抽牌数，手牌补到开局数（MapManager 调用）
    public void SetupLevel(LevelConfig cfg)
    {
        drawPerTurn = cfg.drawPerTurn;

        if (HandCards.Count < cfg.initialHandSize)
        {
            StartCoroutine(DrawCards(cfg.initialHandSize - HandCards.Count));
        }

        drawsThisTurn = drawPerTurn;   // 本关开局补了牌，第一回合不能再抽
    }

    // 同场景换关时调用：原来切场景会把 DeckManager 整个销毁重建（Start→InitDeck 重洗牌），
    // 现在不切场景，手动模拟——销毁所有手牌实体，从 GameProgress.playerDeck 重新洗一副。
    public void ResetForNewLevel()
    {
        for (int i = HandCards.Count - 1; i >= 0; i--)
        {
            if (HandCards[i] != null) Destroy(HandCards[i].gameObject);
        }
        HandCards.Clear();
        drawsThisTurn = 0;
        InitDeck();
    }

    // 回合阶段变了：回合结束（End 阶段）重置抽牌次数，下一回合又能抽
    private void OnPhaseChanged(PhaseChangedEvent e)
    {
        if (e.phase == TurnPhase.End && e.isPlayerTurn)
        {
            drawsThisTurn = 0;
        }
    }

    // 尝试抽一张（受每回合 drawPerTurn 限制）
    public void TryDrawOne()
    {
        // 敲钟后（战斗/结束阶段）不能抽牌，直到这回合打完
        TurnPhase phase = BattleManager.Instance.CurrentPhase;
        if (phase == TurnPhase.Battle || phase == TurnPhase.End)
        {
            Debug.Log("现在不能抽牌");
            return;
        }

        if (drawsThisTurn >= drawPerTurn)
        {
            Debug.Log("本回合已经抽过牌了");
            Narrator.Say(SpeakTopic.DrawAlreadyUsed);
            return;
        }
        if (HandCards.Count >= maxHandSize)
        {
            Debug.Log("手牌满了");
            Narrator.Say(SpeakTopic.HandFull);
            return;
        }
        drawsThisTurn++;
        StartCoroutine(DrawCards(1));
    }

    // 抽 N 张（协程，一张张翻面）
    public System.Collections.IEnumerator DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return DrawOneCard();
        }
    }

    // 编辑器测试用：把指定卡直接生成进手牌（不走抽牌堆），觉醒状态自动生效
    public void DebugAddToHand(CardDataSO data)
    {
        if (data == null) return;
        StartCoroutine(CreateCardInHand(data));
    }

    // 编辑器测试用：整副牌替换成传入的卡（清手牌 → 换牌组数据 → 重洗抽牌堆），
    // 不切场景，立刻生效。下一步抽牌抽到的就是新牌堆。
    public void DebugReplaceDeck(List<CardDataSO> cards)
    {
        if (cards == null || cards.Count == 0)
        {
            Debug.LogWarning("[测试] 传入的牌组是空的，不替换");
            return;
        }
        GameProgress.playerDeck = new List<CardDataSO>(cards);
        ResetForNewLevel();
        EventBus.Publish(new HandChangedEvent());
        Debug.Log("[测试] 抽牌堆已替换为 " + cards.Count + " 张卡");
    }

    // 抽 1 张（翻面后进手牌）
    private System.Collections.IEnumerator DrawOneCard()
    {
        if (HandCards.Count >= maxHandSize)
        {
            Debug.Log("手牌满了");
            yield break;
        }

        if (drawPile.Count == 0)
        {
            Debug.Log("牌堆空了");
            yield break;
        }

        CardDataSO data = drawPile[0];
        drawPile.RemoveAt(0);

        yield return CreateCardInHand(data);
    }

    // 从牌堆出生一张卡，扣着，翻面，进手牌
    private System.Collections.IEnumerator CreateCardInHand(CardDataSO data)
    {
        if (cardPrefab == null)
        {
            Debug.LogError("CardPrefab 没设置！在 DeckManager 上拖入卡牌预制体");
            yield break;
        }

        GameObject go = Instantiate(cardPrefab);
        if (deckPile != null)
            go.transform.position = deckPile.position + Vector3.up * 0.1f;   // 略高于牌堆顶，别叠穿

        Card card = go.GetComponent<Card>();
        if (card == null)
        {
            Debug.LogError("CardPrefab 上缺 Card 组件！");
            Destroy(go);
            yield break;
        }

        card.Init(data, true);   // Init 里默认扣着（背面朝上）
        go.transform.rotation = Quaternion.Euler(90, 0, 0);   // 平放在牌堆上，面朝下（不竖着穿模）
        AudioManager.Instance.PlayDraw();   // 抽牌音效

        yield return StartCoroutine(card.FlatFlipAnim());   // 平着翻到正面

        card.transform.SetParent(handPanel);
        HandCards.Add(card);
        EventBus.Publish(new HandChangedEvent());

        // 抽到时触发的技能（OnDraw，后期自己配）
        card.TriggerAbility(AbilityTrigger.OnDraw, null);
    }

    // 从手牌移除
    public void RemoveFromHand(Card card)
    {
        HandCards.Remove(card);
        EventBus.Publish(new HandChangedEvent());
    }

    // 涅槃生还：把场上的牌收回手牌（以 1 力量留下，技能标记保持已用）。
    // 由卡牌受致命伤且带 FeignDeath 时调用。
    public void ReturnToHand(Card card)
    {
        if (card == null) return;

        BoardManager.Instance.RemoveCardFromBoard(card);
        card.IsPlayed = false;
        card.transform.SetParent(handPanel, false);
        card.transform.localPosition = Vector3.zero;
        card.transform.localRotation = Quaternion.identity;
        card.transform.localScale = Vector3.one;

        if (!HandCards.Contains(card)) HandCards.Add(card);
        EventBus.Publish(new HandChangedEvent());
        Debug.Log("[技能] " + card.CardName + " 回到手牌");
    }

    // 繁殖/前赴后继：死亡时从抽牌堆免费拉一张放到指定道，继承 inheritPower 点力量
    public void SpawnNextOntoSlot(int lane, int inheritPower)
    {
        if (drawPile.Count == 0)
        {
            Debug.Log("[技能] 牌堆空了，无法繁殖补位");
            return;
        }

        CardDataSO data = drawPile[0];
        drawPile.RemoveAt(0);

        CardSlot slot = BoardManager.Instance.GetSlot(lane, true);
        if (slot == null || !slot.IsEmpty) return;   // 道没了/被占就不补
        if (cardPrefab == null) return;

        GameObject go = Instantiate(cardPrefab, slot.transform);
        Card card = go.GetComponent<Card>();
        if (card == null) { Destroy(go); return; }

        card.Init(data, true);
        card.SetFaceDown(false);
        slot.PlaceCard(card);
        if (inheritPower > 0) card.AddPower(inheritPower - 1);   // 基础点数已含 1，补差量
        card.TriggerAbility(AbilityTrigger.OnPlay, null);
        Debug.Log("[技能] 繁殖补位：" + card.CardName + " 顶上第 " + (lane + 1) + " 路");
    }

    // 洗牌
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
