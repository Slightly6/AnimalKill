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

    [Header("抽牌飞入手牌（抛物线手感，和筹码同款配方）")]
    public float drawApexHeight = 0.25f;     // 抛物线比直线多拱多少（低一点：牌是发到手上，不是抛过来）
    public float drawDuration = 0.4f;        // 单张从牌堆飞到手的时长
    public float drawBounceHeight = 0f;      // 落位后小跳多高（0 = 不做 Q 弹）
    public float drawBounceDuration = 0.12f; // 小跳用时
    [Header("抽多张的节奏（逐张加快，和筹码一个味道）")]
    public float drawStagger = 0.22f;        // 上一张起飞后多久，下一张才开始翻面
    public float drawStaggerDecay = 0.92f;   // 每抽一张，下一张的间隔乘这个数（越小越密）
    public float drawStaggerMin = 0.1f;      // 间隔下限

    // 手牌排列：飞行落点和"飞到哪张牌之后"都问它
    private HandManager cachedHand;
    private HandManager Hand
    {
        get
        {
            if (cachedHand == null) cachedHand = FindObjectOfType<HandManager>();
            return cachedHand;
        }
    }
    private int drawJobs;               // 还在翻面的抽牌任务数
    private int drawFlights;            // 还在空中飞的牌数（DrawCards 结尾等它们全部落位）

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
    // 回合结束：手牌数据全部进弃牌堆，实体沿弧线飞回弃牌堆再逐张销毁（和结算牌同一套演出）
    // 批量窗口：清手整个动作只算 1 次手牌变动（船锚计数用）
    public System.Collections.IEnumerator DiscardAllHandFly()
    {
        TrinketRuntime.BeginHandChangeBatch();
        List<Card> toFly = new List<Card>();
        for (int i = HandCards.Count - 1; i >= 0; i--)
        {
            Card c = HandCards[i];
            DiscardCardData(c);          // 数据先入账，实体不动
            if (c != null) toFly.Add(c);
        }
        SyncMimicVisuals();
        EventBus.Publish(new HandChangedEvent());
        TrinketRuntime.EndHandChangeBatch();

        if (toFly.Count == 0) yield break;

        if (BattleView.Instance != null)
            yield return BattleView.Instance.FlyOutToDiscard(toFly);   // 飞行演出交给 BattleView
        else
            foreach (Card c in toFly)   // 没有演出层（个别测试场景）：当场销毁兜底
                if (c != null) Destroy(c.gameObject);
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
    // 抽 N 张（协程）：一张张"甩"出去 —— 上一张起飞后等一下下，下一张才开始翻面，
    // 间隔逐张收紧（和筹码飞进主池同一个节奏），前几张连成一串在空中飞；
    // 全部落位后才继续流程（不然流程先走了，玩家能点到还在空中的牌）。
    // 批量窗口：一轮补牌不管几张只算 1 次手牌变动（船锚计数用）
    public System.Collections.IEnumerator DrawCards(int count)
    {
        TrinketRuntime.BeginHandChangeBatch();

        // 任务数在翻面前就加上，后面几张翻面期间也在等它们；飞行数在牌真起飞时才算上，
        // 这样"翻面中的"和"空中的"都能等到，收尾不会提前放行
        float wait = drawStagger;
        for (int i = 0; i < count; i++)
        {
            drawJobs++;
            StartCoroutine(DrawOneCard());
            yield return new WaitForSeconds(wait);
            wait = Mathf.Max(drawStaggerMin, wait * drawStaggerDecay);
        }

        while (drawJobs > 0 || drawFlights > 0) yield return null;

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
            EndDrawJob();
            yield break;
        }
         
        if (drawPile.Count == 0)
        {
            if (discardPile.Count == 0)
            {
                Debug.Log("牌堆和弃牌堆都空了");
                EndDrawJob();
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
        EndDrawJob();
    }

    // 这个抽牌任务收工（没牌可抽也要记一笔，不然 DrawCards 会一直等）
    private void EndDrawJob()
    {
        drawJobs = Mathf.Max(0, drawJobs - 1);
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
            // 略高于牌堆顶，别叠穿；连抽时可能有两张同时在牌堆上翻面，高度错开一点免得完全重合
            go.transform.position = deckPile.position + Vector3.up * (0.1f + 0.035f * (HandCards.Count % 4));

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

        // 入列 + 换父（SetParent 保持世界坐标，所以这一刻不瞬移）：飞行终点就是它以后待的手牌格位
        card.transform.SetParent(handPanel, true);
        HandCards.Add(card);
        SyncMimicVisuals();
        EventBus.Publish(new HandChangedEvent());

        // 从牌堆沿一条抛物线飞进手牌（顶点高度+时长反解，落点精确），落地小跳一下 = q弹。
        // 飞行期间在手牌列表里，但先不让 HandManager 抢位置，由这段协程写位置。
        HandManager hand = Hand;
        if (hand != null)
        {
            card.SkipArrange = true;
            drawFlights++;
            StartCoroutine(FlyIntoHand(card, hand));
        }
        else if (HandCamera.HandLayerIndex >= 0)
        {
            card.SetLayerRecursively(HandCamera.HandLayerIndex);
        }

        if (card == null) yield break;
        Debug.Log("[发牌] 已入手牌：" + data.name);
    }

    // 一张牌从牌堆顶飞进手牌扇面：
    // 目标格位每帧重算 —— 飞行途中又抽了新牌，手牌会重排，这张就自然改追自己的新位置。
    // 主飞行走"起点→格位"的平滑插值 + 中途拱起；落位后再小跳一下压住。
    // 只有"到手"和"被移出手牌/销毁"两种结局，收尾统一走下面，drawFlights 一定减得回来。
    private System.Collections.IEnumerator FlyIntoHand(Card card, HandManager hand)
    {
        Vector3 startPos = card.transform.position;
        Quaternion startRot = card.transform.rotation;

        float T = Mathf.Max(0.05f, drawDuration);
        float ct = 0f;
        bool alive = true;

        // 主飞行
        while (alive && ct < T)
        {
            int index = card != null ? HandCards.IndexOf(card) : -1;
            if (index < 0) { alive = false; break; }   // 被移出手牌（清场/打出）

            int count = HandCards.Count;
            Vector3 to = hand.GetSlotPosition(index, count);
            Quaternion toRot = hand.GetSlotRotation(index, count);

            ct += Time.deltaTime;
            float p = Mathf.Clamp01(ct / T);
            float k = 1f - (1f - p) * (1f - p);   // 先快后慢：被抽出去，再被手接住

            Vector3 pos = Vector3.Lerp(startPos, to, k);
            pos.y += Mathf.Sin(p * Mathf.PI) * drawApexHeight;   // 比直线多拱一点：飞的感觉
            card.transform.position = pos;
            card.transform.rotation = Quaternion.Slerp(startRot, toRot, k);
            yield return null;
        }

        // 落位小跳一下再压住（q弹的来源）
        if (alive && drawBounceHeight > 0f && drawBounceDuration > 0f)
        {
            float bt = 0f;
            while (alive && bt < 1f)
            {
                int index = card != null ? HandCards.IndexOf(card) : -1;
                if (index < 0) { alive = false; break; }

                Vector3 to = hand.GetSlotPosition(index, HandCards.Count);
                bt += Time.deltaTime / drawBounceDuration;
                card.transform.position = to + Vector3.up * (Mathf.Sin(Mathf.Clamp01(bt) * Mathf.PI) * drawBounceHeight);
                yield return null;
            }
        }

        if (card != null)
        {
            card.SkipArrange = false;   // 交回 HandManager 正常排列
            if (alive && HandCamera.HandLayerIndex >= 0)
                card.SetLayerRecursively(HandCamera.HandLayerIndex);   // 落进手牌才切手牌层（HandCamera 渲染，永远在最上层）
        }

        drawFlights = Mathf.Max(0, drawFlights - 1);
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

    // 弃牌的数据部分：进弃牌堆（抽干后洗回）、移出手牌列表，不动实体——实体销毁交给调用方
    // 道具弃掉也算"使用"：消耗（创可贴挂账则免死一次进弃牌堆）
    private void DiscardCardData(Card card)
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
    }

    // 弃一张牌：数据入账 + 当场销毁实体（主动弃牌用的瞬时版）
    public void DiscardToPile(Card card)
    {
        if (card == null) return;
        DiscardCardData(card);
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
