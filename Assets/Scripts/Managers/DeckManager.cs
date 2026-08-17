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
    public int maxHandSize = 20;     // 手牌上限
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
            return;
        }
        if (HandCards.Count >= maxHandSize)
        {
            Debug.Log("手牌满了");
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
            go.transform.position = deckPile.position;

        Card card = go.GetComponent<Card>();
        if (card == null)
        {
            Debug.LogError("CardPrefab 上缺 Card 组件！");
            Destroy(go);
            yield break;
        }

        card.Init(data, true);   // Init 里默认扣着（背面朝上）

        yield return StartCoroutine(card.FlipAnim());

        card.transform.SetParent(handPanel);
        HandCards.Add(card);
        EventBus.Publish(new HandChangedEvent());
    }

    // 从手牌移除
    public void RemoveFromHand(Card card)
    {
        HandCards.Remove(card);
        EventBus.Publish(new HandChangedEvent());
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
