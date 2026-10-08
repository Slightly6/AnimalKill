using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyController : Singleton<EnemyController>
{
    private EnemyConfig config;

    private readonly List<CardDataSO> hand = new List<CardDataSO>();

    private readonly List<CardDataSO> drawPile =new List<CardDataSO>();

    private readonly List<CardDataSO> discardPile = new List<CardDataSO>();

    private EnemyIntentType currentIntent =  EnemyIntentType.Check;

    [Header("下次攻击伤害倍率")]
    public float nextAttackMultiplier = 1f;


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

        config = enemyConfig;


        hand.Clear();
        drawPile.Clear();
        discardPile.Clear();

        drawPile.AddRange(config.deck);
        Shuffle(drawPile);

        DrawToFull();

        EventBus.Publish(new EnemyHandChangedEvent{enemy=this});
    }

    public void PrepareIntent()
    {
        if (config == null)
            return;

        float value = UnityEngine.Random.value;

        if (value < config.checkChance)
        {
            currentIntent = EnemyIntentType.Check;
        }
        else if (value <config.checkChance + config.raiseChance)
        {
            currentIntent = EnemyIntentType.Raise;
        }
        else
        {
            currentIntent = EnemyIntentType.Call;
        }
        if (IsDead)
        {
            return;
        }
        

        EventBus.Publish(new EnemyIntentChangedEvent{enemy=this,intent=currentIntent});
    }

    public EnemyPlayData BuildPlayData()
    {
        EnemyPlayData result = new EnemyPlayData
        {
            intent = currentIntent
        };

        if (currentIntent == EnemyIntentType.Check)
        {
            result.damageMultiplier = 0f;
            return result;
        }

        int cardCount =currentIntent == EnemyIntentType.Raise? config.raiseCardCount: config.callCardCount;


        cardCount = Mathf.Min(cardCount,hand.Count);

        cardCount = UnityEngine.Random.Range(1, cardCount + 1);
        List<int> indices =FindBestCombination(cardCount);

        foreach (int index in indices)
        {
            result.handIndices.Add(index);
            result.cards.Add(hand[index]);
        }

        result.damageMultiplier =currentIntent == EnemyIntentType.Raise? config.raiseDamageMultiplier: config.callDamageMultiplier;

        return result;
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

    private int CalculateRawDamage(
        List<CardDataSO> cards)
    {
        HandType type =
            PokerResolver.Evaluate(cards);

        int baseChips =
            PokerResolver.GetBaseChips(type);

        int multiplier =
            PokerResolver.GetMultiplier(type);

        HashSet<int> coreIndices =
            PokerResolver.GetCoreCardIndices(
                cards,
                type
            );

        int cardBonus = 0;

        for (int i = 0; i < cards.Count; i++)
        {
            if (!coreIndices.Contains(i))
                continue;

            cardBonus += GetRankValue(
                cards[i].rank
            );
        }

        return (baseChips + cardBonus) * multiplier;
    }

    private void DrawToFull()
    {
        while (hand.Count < config.handSize)
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

    private int GetRankValue(CardRank rank)
    {
        int value = (int)rank;

        if (value == 1)
            value = 14;

        return value;
    }

    
    //-------------特殊效果卡牌----------------

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