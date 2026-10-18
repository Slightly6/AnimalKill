using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyController : Singleton<EnemyController>
{
    public EnemyConfig Config { get; private set; }

    private readonly List<CardDataSO> hand = new List<CardDataSO>();

    private readonly List<CardDataSO> drawPile =new List<CardDataSO>();

    private readonly List<CardDataSO> discardPile = new List<CardDataSO>();

    private EnemyIntentType currentIntent =  EnemyIntentType.Check;

    [Header("下次攻击伤害倍率")]
    public float nextAttackMultiplier = 1f;

    private EnemyBehaviorSO selectedBehavior;

    public EnemyBehaviorSO SelectedBehavior => selectedBehavior;

    [Tooltip("当前回合数（Rush 等机制用，Initialize 时重置为 0）")]
    public int RoundNumber { get; private set; } = 0;

    [Tooltip("上回合意图（Double 加倍等机制用）")]
    public EnemyIntentType LastIntent { get; private set; } = EnemyIntentType.Check;

    // 当前生效的行为池（Switch 变脸时在 behaviors ↔ backupBehaviors 间切换）
    private List<EnemyBehaviorSO> activeBehaviors;

    public IReadOnlyList<CardDataSO> Hand
    {
        get { return hand; }
    }
    
    public EnemyIntentType CurrentIntent
    {
        get { return currentIntent; }
    }

    public bool IsDead => GameManager.Instance != null && GameManager.Instance.EnemyChips <= 0;


    public void Initialize(EnemyConfig enemyConfig)
    {
        if (enemyConfig == null)
        {
            Debug.LogError(
                "[EnemyController] EnemyConfig 为空"
            );
            return;
        }

        Config = enemyConfig;


        hand.Clear();
        drawPile.Clear();
        discardPile.Clear();

        drawPile.AddRange(Config.deck);
        Shuffle(drawPile);

        DrawToFull();

        // 被动机制：战斗开始钩子
        RoundNumber = 0;
        LastIntent = EnemyIntentType.Check;
        activeBehaviors = Config.behaviors;   // 默认用主行为池
        if (Config.passives != null)
        {
            foreach (var p in Config.passives)
                p.OnBattleStart(this);
        }

        EventBus.Publish(new EnemyHandChangedEvent{enemy=this});
    }

    public void PrepareIntent()
    {
        RoundNumber++;   // 每回合选意图前 +1

        // 被动机制：敌人回合开始钩子（Bluff/Switch/Double 等在这里触发）
        if (Config.passives != null)
        {
            foreach (var p in Config.passives)
                p.OnEnemyTurnStart(this);
        }

        if (activeBehaviors == null || activeBehaviors.Count == 0)
            return;

        // 按 weight 加权随机
        float totalWeight = 0f;
        foreach (var b in activeBehaviors) totalWeight += b.weight;

        float value = UnityEngine.Random.value * totalWeight;
        float cum = 0f;
        selectedBehavior = activeBehaviors[activeBehaviors.Count - 1];  // 兜底
        foreach (var b in activeBehaviors)
        {
            cum += b.weight;
            if (value <= cum) { selectedBehavior = b; break; }
        }

        currentIntent = selectedBehavior.intent;
        LastIntent = currentIntent;   // 记录本回合意图，供下回合 Double 用

        if (IsDead) return;
        EventBus.Publish(new EnemyIntentChangedEvent { enemy = this, intent = currentIntent });
    }

    public EnemyPlayData BuildPlayData()
    {
        EnemyPlayData result = new EnemyPlayData { intent = currentIntent };

        // Check/Fold 不出牌；出牌数从敌人配置的区间随机
        if (selectedBehavior == null
            || currentIntent == EnemyIntentType.Check
            || currentIntent == EnemyIntentType.Fold)
            return result;

        int cardCount = UnityEngine.Random.Range(Config.minCardCount, Config.maxCardCount + 1);
        cardCount = Mathf.Min(cardCount, hand.Count);
        List<int> indices = FindBestCombination(cardCount);

        foreach (int index in indices)
        {
            result.handIndices.Add(index);
            result.cards.Add(hand[index]);
        }

        // 让 behavior 有机会改写选牌（如只挑同花色），默认不动
        selectedBehavior.ModifyCardChoice(this, result.handIndices);

        return result;
    }
    public void ExecuteSelectedBehavior()
    {
        if (selectedBehavior != null) selectedBehavior.Apply(this);
    }

    /// <summary>
    /// Switch 变脸用：在主行为池和备用行为池之间切换。
    /// </summary>
    public void SwitchBehaviors()
    {
        if (Config.backupBehaviors != null && Config.backupBehaviors.Count > 0)
        {
            activeBehaviors = activeBehaviors == Config.behaviors
                ? Config.backupBehaviors
                : Config.behaviors;
        }
    }

    /// <summary>
    /// Double 加倍用：强制把 selectedBehavior 改成 lastIntent 对应的行为。
    /// </summary>
    public void ForceIntent(EnemyIntentType intent)
    {
        if (activeBehaviors == null) return;
        foreach (var b in activeBehaviors)
        {
            if (b.intent == intent) { selectedBehavior = b; currentIntent = intent; return; }
        }
    }

    // Fold 用：按敌人配置的弃牌区间随机弃牌进弃牌堆
    public void DiscardByConfig()
    {
        int count = UnityEngine.Random.Range(Config.minDiscard, Config.maxDiscard + 1);
        for (int i = 0; i < count && hand.Count > 0; i++)
        {
            int index = UnityEngine.Random.Range(0, hand.Count);
            discardPile.Add(hand[index]);
            hand.RemoveAt(index);
        }
        EventBus.Publish(new EnemyHandChangedEvent { enemy = this });
    }

    // Steal 等被动用：把一张牌塞进敌人弃牌堆
    public void AddToDiscardPile(CardDataSO card)
    {
        discardPile.Add(card);
    }

    public void CommitPlayedCards(
        EnemyPlayData playData)
    {
        if (playData == null)
            return;

        List<int> indices =
            new List<int>(playData.handIndices);

        // 从后往前删，避免索引错位
        indices.Sort((a, b) => b.CompareTo(a));

        foreach (int index in indices)
        {
            if (index < 0 || index >= hand.Count)
                continue;

            discardPile.Add(hand[index]);
            hand.RemoveAt(index);
        }

        EventBus.Publish(new EnemyHandChangedEvent{enemy=this});
    }

    public void RefillHand()
    {
        DrawToFull();
        EventBus.Publish(new EnemyHandChangedEvent{enemy=this});
    }


    public void SkipEnemyAttack()
    {
        if (IsDead)
        {
            return;
        }

        currentIntent = EnemyIntentType.Check;
        EventBus.Publish(new EnemyIntentChangedEvent{enemy=this,intent=currentIntent});
    }

    private List<int> FindBestCombination(
        int targetCount)
    {
        List<int> best =
            new List<int>();

        int bestDamage = -1;

        List<int> current =
            new List<int>();

        SearchCombination(
            targetCount,
            0,
            current,
            ref best,
            ref bestDamage
        );

        return best;
    }

    private void SearchCombination(
        int targetCount,
        int startIndex,
        List<int> current,
        ref List<int> best,
        ref int bestDamage)
    {
        if (current.Count == targetCount)
        {
            List<CardDataSO> cards =
                new List<CardDataSO>();

            foreach (int index in current)
                cards.Add(hand[index]);

            int damage =
                CalculateRawDamage(cards);

            if (damage > bestDamage)
            {
                bestDamage = damage;
                best = new List<int>(current);
            }

            return;
        }

        int needed =
            targetCount - current.Count;

        for (
            int i = startIndex;
            i <= hand.Count - needed;
            i++)
        {
            current.Add(i);

            SearchCombination(
                targetCount,
                i + 1,
                current,
                ref best,
                ref bestDamage
            );

            current.RemoveAt(
                current.Count - 1
            );
        }
    }

    // 敌人选牌评分：牌型固定伤害（含掷骰永久加成）
    private int CalculateRawDamage(List<CardDataSO> cards)
    {
        return PokerResolver.GetHandDamage(PokerResolver.Evaluate(cards));
    }

    private void DrawToFull()
    {
        while (hand.Count < Config.handSize)
        {
            if (drawPile.Count == 0)
                RecycleDiscardPile();

            if (drawPile.Count == 0)
                break;

            int index =
                drawPile.Count - 1;

            hand.Add(drawPile[index]);
            drawPile.RemoveAt(index);
        }
    }

    private void RecycleDiscardPile()
    {
        if (discardPile.Count == 0)
            return;

        drawPile.AddRange(discardPile);
        discardPile.Clear();

        Shuffle(drawPile);
    }

    private void Shuffle(List<CardDataSO> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int index =UnityEngine.Random.Range( 0,i + 1 );

            CardDataSO temp = cards[i];
            cards[i] = cards[index];
            cards[index] = temp;
        }
    }

    // 庄家钮：随机搅乱成行为池里的另一个意图
    public void RandomizeIntent()
    {
        if (activeBehaviors == null || activeBehaviors.Count == 0) return;
        selectedBehavior = activeBehaviors[UnityEngine.Random.Range(0, activeBehaviors.Count)];
        currentIntent = selectedBehavior.intent;
        EventBus.Publish(new EnemyIntentChangedEvent { enemy = this, intent = currentIntent });
        Debug.Log("[庄家钮] 敌人意图被搅乱成 " + currentIntent);
    }

    //-------------特殊效果卡牌----------------

    // 扑克脸：下一次攻击伤害被压成固定值（-1 = 不压）
    private int nextAttackFlat = -1;
    public void SetNextAttackFlat(int dmg) { nextAttackFlat = dmg; }
    public int ConsumeNextAttackFlat()
    {
        int f = nextAttackFlat;
        nextAttackFlat = -1;
        return f;
    }

    // 效果：乘上倍率
    public void MultiplyNextAttack(float m)
    {
        nextAttackMultiplier *= m;
    }

    public float ConsumeNextAttackMultiplier()
    {
        float m = nextAttackMultiplier;
        nextAttackMultiplier = 1f;
        return m;
    }
}
