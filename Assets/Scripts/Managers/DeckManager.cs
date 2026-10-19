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

    [Header("卡牌预制体")]
    public GameObject cardPrefab;

    [Header("牌堆位置（卡从这里出生）")]
    public Transform deckPile;

    [Header("弃牌堆位置（结算后卡飞到这）")]
    public Transform discardPilePos;

    [Header("手牌父节点")]
    public Transform handPanel;

    // 抽牌堆和手牌
    private List<CardDataSO> drawPile = new List<CardDataSO>();// 抽牌堆
    private List<CardDataSO> discardPile = new List<CardDataSO>();// 弃牌堆（抽干后洗回去）
    // 弃牌堆张数（车库甩卖等镶嵌物读这个数）
    public int DiscardCount => discardPile.Count;
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
      discardPile.Clear();   // 换关重建时弃牌堆必须一起清，不然上一关的牌会洗回抽牌堆被重复抽

      // 跨关继承：进度里已经有牌组就用它；没有（第一次）用 Inspector 的初始牌组
      if (GameProgress.playerDeck != null && GameProgress.playerDeck.Count > 0)
      {
          drawPile = new List<CardDataSO>(GameProgress.playerDeck);
      }
      else
      {
          // 初始牌组：逐张克隆成运行时独立个体再进牌库（原资产只读，强化不污染）
          drawPile = new List<CardDataSO>();
          for (int i = 0; i < deckCards.Count; i++)
              drawPile.Add(GameProgress.CloneCard(deckCards[i]));
          GameProgress.playerDeck = new List<CardDataSO>(drawPile);   // 存进进度，以后跨关继承
      }
      Shuffle(drawPile);
      Debug.Log("牌组初始化完成，共 " + drawPile.Count + " 张");
    }
    // 回合结束：手牌全部进弃牌堆（销毁实体，数据留着抽干后洗回）
    // 批量窗口：清手整个动作只算 1 次手牌变动（船锚计数用）
    public void DiscardAllHand()
    {
        TrinketRuntime.BeginHandChangeBatch();
        for (int i = HandCards.Count - 1; i >= 0; i--)
            DiscardToPile(HandCards[i]);
        TrinketRuntime.EndHandChangeBatch();
    }
    // 同场景换关时调用：原来切场景会把 DeckManager 整个销毁重建（Start→InitDeck 重洗牌），
    // 现在不切场景，手动模拟——销毁所有手牌实体，从 GameProgress.playerDeck 重新洗一副。
    // 只销毁桌上的玩家手牌实体，不动牌堆
    public void ClearHandObjects()
    {
        for (int i = HandCards.Count - 1; i >= 0; i--)
        {
            if (HandCards[i] != null) Destroy(HandCards[i].gameObject);
        }
        HandCards.Clear();
    }

    public void ResetForNewLevel()
    {
        ClearHandObjects();
        InitDeck();
    }

    // 关卡收尾：把手牌全部飞回抽牌堆（演出），飞完销毁实体。数据不动（下关 InitDeck 会重建）
    // 期间手牌列表先清空，避免飞行中被当手牌操作
    public System.Collections.IEnumerator FlyAllHandToDeck()
    {
        Vector3 pilePos = deckPile != null ? deckPile.position : transform.position;
        List<Card> toFly = new List<Card>(HandCards);
        HandCards.Clear();
        SyncMimicVisuals();
        TrinketRuntime.BeginHandChangeBatch();

        for (int i = 0; i < toFly.Count; i++)
        {
            Card c = toFly[i];
            if (c == null) continue;
            ShopCard sc = c.GetComponent<ShopCard>();
            if (sc != null)
            {
                sc.StopHover();   // 先停悬浮，避免回落动画和飞回抢位置
                sc.StartCoroutine(sc.FlyStraight(c.transform, pilePos, 0.4f));
            }
            else
            {
                Destroy(c.gameObject);
            }
        }
        TrinketRuntime.EndHandChangeBatch();
        EventBus.Publish(new HandChangedEvent());
        yield return new WaitForSeconds(0.45f);   // 等最后一张飞完
    }
    // 抽 N 张（协程，一张张翻面）
    // 批量窗口：一轮补牌不管几张只算 1 次手牌变动（船锚计数用）
    public System.Collections.IEnumerator DrawCards(int count)
    {
        TrinketRuntime.BeginHandChangeBatch();
        for (int i = 0; i < count; i++)
        {
            yield return DrawOneCard();
        }
        TrinketRuntime.EndHandChangeBatch();
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
            if (discardPile.Count == 0)
            {
                Debug.Log("牌堆和弃牌堆都空了");
                yield break;
            }
            drawPile = new List<CardDataSO>(discardPile);
            Debug.Log("[牌堆] 抽牌堆已空，弃牌堆洗回（" + drawPile.Count + " 张）");
            discardPile.Clear();
            Shuffle(drawPile);
            Debug.Log("[牌堆] 抽牌堆已空，弃牌堆洗回（" + drawPile.Count + " 张）");
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

        Debug.Log("[发牌] 准备 Instantiate cardPrefab");
        GameObject go = Instantiate(cardPrefab);
        Debug.Log("[发牌] Instantiate 完成");
        // go.GetComponent<ShopCard>().enabled=false; // 禁用 ShopCard 脚本
        // go.GetComponent<CardDisplay>().enabled=true; // 启用 CardDisplay 脚本
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
        // 记号笔：镶了它的牌抽上手就做记号（金色角标）
        if (data.effects != null)
            foreach (var e in data.effects)
                if (e is MarkedCardTrinket) { card.SetMarked(true); break; }
        go.transform.rotation = Quaternion.Euler(90, 0, 0);   // 平放在牌堆上，面朝下（不竖着穿模）
        // AudioManager.Instance.PlayDraw();   // 抽牌音效  （排查卡死：暂时关闭）

        yield return StartCoroutine(card.FlatFlipAnim());   // 平着翻到正面
        if (card == null) yield break;   
        card.transform.SetParent(handPanel);
        HandCards.Add(card);
        if (HandCamera.HandLayerIndex >= 0)
            card.SetLayerRecursively(HandCamera.HandLayerIndex);   // 切到手牌层 → HandCamera 渲染 → 永远在最上层
        SyncMimicVisuals();
        EventBus.Publish(new HandChangedEvent());

        Debug.Log("[发牌] 已入手牌：" + data.name);
    }

    // 从手牌移除
    public void RemoveFromHand(Card card)
    {
        if (card == null) return;
        // 道具牌一次性：使用（打出或弃掉）即消耗。创可贴挂账：下一个道具第一次用完不销毁、进弃牌堆（洗回可再用）
        if (card.DeckData.cardType == CardType.Item)
        {
            if (TrinketRuntime.ConsumeNextItemSave())
                discardPile.Add(card.DeckData);
            else
                GameProgress.playerDeck.Remove(card.DeckData);
        }
        else if (!TrinketRuntime.IsBanished(card.DeckData))
        {
            discardPile.Add(card.DeckData);   // 拟态牌进本体，变身对象不重复；大邪典销毁的牌不进弃牌堆
        }
        HandCards.Remove(card);
        card.SetLayerRecursively(0);   // 切回 Default 层 → 恢复正常遮挡
        SyncMimicVisuals();
        EventBus.Publish(new HandChangedEvent());
    }

    // 手牌内换位（拖拽换序）：把牌移到新索引
    // 不发 HandChangedEvent——换位不算手牌变动（船锚计数不涨）
    public void MoveHandCard(Card card, int newIndex)
    {
        int oldIndex = HandCards.IndexOf(card);
        if (oldIndex < 0) return;
        newIndex = Mathf.Clamp(newIndex, 0, HandCards.Count - 1);
        if (oldIndex == newIndex) return;
        HandCards.RemoveAt(oldIndex);
        HandCards.Insert(newIndex, card);
        SyncMimicVisuals();
    }

    // 弃牌：数据进弃牌堆（抽干后洗回），销毁手牌实体
    // 道具弃掉也算"使用"：消耗（创可贴挂账则免死一次进弃牌堆）
    public void DiscardToPile(Card card)
    {
        if (card == null) return;
        if (card.DeckData.cardType == CardType.Item)
        {
            if (TrinketRuntime.ConsumeNextItemSave())
                discardPile.Add(card.DeckData);
            else
                GameProgress.playerDeck.Remove(card.DeckData);
        }
        else if (!TrinketRuntime.IsBanished(card.DeckData))
            discardPile.Add(card.DeckData);   // 拟态牌进本体，变身对象不重复；大邪典销毁的牌不进弃牌堆
        HandCards.Remove(card);
        Destroy(card.gameObject);
        SyncMimicVisuals();
        EventBus.Publish(new HandChangedEvent());
    }

    // 拟态牌视觉同步：手牌里完全变成最左那张（自己排到最左时显原形）
    // 每次手牌变动（抽牌/弃牌/换位）都要调一遍
    void SyncMimicVisuals()
    {
        if (HandCards == null || HandCards.Count == 0) return;
        Card leftmost = HandCards[0];
        for (int i = 0; i < HandCards.Count; i++)
        {
            Card c = HandCards[i];
            if (c == null || !c.IsMimic) continue;
            if (c == leftmost) c.RestoreFromMimic();   // 自己在最左 → 原图
            else c.MimicInto(leftmost);                // 完全变成最左那张
        }
    }

    // // 补手牌到 7 张（每轮结算完调用）
    // public System.Collections.IEnumerator RefillHand()
    // {
    //     while (HandCards.Count < 7)
    //         yield return DrawOneCard(); 
    // }

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
    public void AddToDeck(CardDataSO data)
    {
        CardDataSO clone = GameProgress.CloneCard(data);   // 商店买/宝箱给的卡：克隆成独立个体再进牌库
        drawPile.Add(clone);
        GameProgress.playerDeck.Add(clone);
    }

    // 镶嵌物·吸铁石：把抽牌堆里的指定卡挪到堆顶（下次抽牌必抽到）
    public void MoveToDrawPileTop(CardDataSO data)
    {
        if (data == null) return;
        if (!drawPile.Remove(data)) return;   // 不在抽牌堆（在手里/弃牌堆）就算了
        drawPile.Insert(0, data);
    }

    // 镶嵌物·钩子：把弃牌堆里的指定卡直接拿回手牌（生成实体走正常入手机）
    public void ReturnCardFromDiscard(CardDataSO data)
    {
        if (data == null) return;
        if (!discardPile.Remove(data)) return;   // 不在弃牌堆就算了
        StartCoroutine(CreateCardInHand(data));
        Debug.Log("[钩子] " + data.name + " 从弃牌堆勾回手牌");
    }

    // 镶嵌物·窥孔：看弃牌堆顶（空堆返回 null）
    public CardDataSO PeekDiscardTop()
    {
        if (discardPile.Count == 0) return null;
        return discardPile[discardPile.Count - 1];
    }
}
