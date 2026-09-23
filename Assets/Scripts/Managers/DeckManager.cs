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
    private List<CardDataSO> discardPile = new List<CardDataSO>();// 弃牌堆（抽干后洗回去）
    // 手牌
    public List<Card> HandCards{get;private set;}=new List<Card>();
    private void Start()
    {
        InitDeck();
    }

    private void OnDestroy()
    {
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

        if (HandCards.Count < cfg.initialHandSize)
        {
            StartCoroutine(DrawCards(cfg.initialHandSize - HandCards.Count));
        }
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
        InitDeck();
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

    // 手牌内换位（拖拽换序）：把牌移到新索引
    public void MoveHandCard(Card card, int newIndex)
    {
        int oldIndex = HandCards.IndexOf(card);
        if (oldIndex < 0) return;
        newIndex = Mathf.Clamp(newIndex, 0, HandCards.Count - 1);
        if (oldIndex == newIndex) return;
        HandCards.RemoveAt(oldIndex);
        HandCards.Insert(newIndex, card);
        EventBus.Publish(new HandChangedEvent());
    }

    // 弃牌：数据进弃牌堆（抽干后洗回），销毁手牌实体
    public void DiscardToPile(Card card)
    {
        if (card == null) return;
        discardPile.Add(card.Data);
        HandCards.Remove(card);
        Destroy(card.gameObject);
        EventBus.Publish(new HandChangedEvent());
    }

    // 补手牌到 7 张（每轮结算完调用）；抽牌堆空了把弃牌堆洗回去，两堆都空则有几张补几张
    public System.Collections.IEnumerator RefillHand()
    {
        while (HandCards.Count < 7)
        {
            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0) yield break;   // 两堆全空，补不了了
                drawPile = new List<CardDataSO>(discardPile);
                discardPile.Clear();
                Shuffle(drawPile);
                Debug.Log("[牌堆] 抽牌堆已空，弃牌堆洗回（" + drawPile.Count + " 张）");
            }
            yield return DrawOneCard();
        }
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
