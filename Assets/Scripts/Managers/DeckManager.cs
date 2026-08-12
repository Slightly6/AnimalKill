using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理牌组和手牌。
/// 管洗牌、抽牌、手牌上限。
/// 监听 PhaseChanged 事件，抽牌阶段自动抽牌。
/// </summary>
public class DeckManager : Singleton<DeckManager>
{
    [Header("牌组配置")]
    public List<CardDataSO> deckCards = new List<CardDataSO>();  // 初始牌组

    [Header("手牌设置")]
    public int maxHandSize = 20;     // 手牌上限
    public int initialHandSize = 6;  // 开局给几张（后续技能可以改）
    public int drawPerTurn = 1;      // 每回合抽几张

    [Header("卡牌预制体")]
    public GameObject cardPrefab;

    [Header("牌堆位置（卡从这里出生）")]
    public Transform deckPile;

    [Header("手牌父节点")]
    public Transform handPanel;

    // 抽牌堆和手牌
    private List<CardDataSO> drawPile = new List<CardDataSO>();// 抽牌堆
    public List<Card> HandCards { get; private set; } = new List<Card>();// 手牌

    private void Start()
    {
        InitDeck();
        StartCoroutine(DrawCards(initialHandSize));   // 开局给 initialHandSize 张（带翻面）
        EventBus.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
    }

    // 初始化牌组
    private void InitDeck()
    {
        drawPile = new List<CardDataSO>(deckCards);
        Shuffle(drawPile);
        Debug.Log("牌组初始化完成，共 " + drawPile.Count + " 张");
    }

    // 回合阶段变了（先不用自动抽牌，改为手动点牌堆抽）
    private void OnPhaseChanged(PhaseChangedEvent e)
    {
        // 以后需要自动抽牌再打开
        // if (e.phase == TurnPhase.Draw && e.isPlayerTurn)
        // {
        //     StartCoroutine(DrawCards(drawPerTurn));
        // }
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

        CardDataSO clone = data.Clone();

        // 从牌堆位置出生
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

        card.Init(clone, true);   // Init 里默认扣着（背面朝上）

        // 翻面动画
        yield return StartCoroutine(card.FlipAnim());

        // 翻完进手牌
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
