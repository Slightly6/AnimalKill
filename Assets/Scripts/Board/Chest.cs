using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

// 宝箱能开出的东西类型，以后加新东西就往这加
public enum ChestRewardType
{
    Card,   // 卡牌
    Chip,   // 筹码
    Hide    // 兽皮
}

// 一条奖励类型 + 权重（权重越大越容易抽到）
[System.Serializable]
public class ChestRewardEntry
{
    public ChestRewardType type = ChestRewardType.Card;
    public int weight = 1;
}

/// <summary>
/// 宝箱：点开盖子，按权重随机抽一种奖励类型（卡牌/筹码/兽皮）。
/// 卡牌 → 抽 3 张（点数越高概率越小），三选一进牌堆（卡立起来、背面朝你，点一下放大+翻面，再点一下选中）。
/// 筹码/兽皮 → 以后填。
/// </summary>
public class Chest : MonoBehaviour
{
    [Header("直接拖子物体Lid进来")]
    public Transform lid;
    public float openAngle = 110f;
    public float openDuration = 0.8f;

    [Header("奖励池（把 52 张卡都拖进来）")]
    public List<CardDataSO> rewardPool = new List<CardDataSO>();

    [Header("卡牌预制体（不拖就用 DeckManager 那份）")]
    public GameObject cardPrefab;

    [Header("奖励卡布局（位置不对就调这俩）")]
    public int rewardCount = 3;                                // 出几张
    public Vector3 cardSpawnOffset = new Vector3(0, 0, 2f);    // 卡刷在箱子哪（相对箱子）
    public float cardSpacing = 2.5f;                           // 三张卡横向间距

    [Header("点击放大")]
    public float zoomScale = 1.2f;                             // 点一下放大到几倍
    public float zoomDuration = 0.15f;                         // 放大/缩小各多久

    [Header("奖励类型（随机抽一种，权重越大越容易出）")]
    public List<ChestRewardEntry> rewardTypes = new List<ChestRewardEntry>();

    private bool _isOpened = false;   // 防止重复点箱子
    private bool _picking = false;    // 防止重复选牌
    private List<Card> rewardCards = new List<Card>();   // 抽出的卡

    // 鼠标点箱子开箱
    private void OnMouseDown()
    {
        OpenChest();
    }

    public void OpenChest()
    {
        if (lid == null || _isOpened) return;
        _isOpened = true;
        StartCoroutine(PlayOpenAnim());
    }

    // 开盖动画，放完抽卡
    IEnumerator PlayOpenAnim()
    {
        float t = 0;
        Quaternion start = lid.localRotation;
        Quaternion end = Quaternion.Euler(openAngle, 0, 0);
        while (t < openDuration)
        {
            t += Time.deltaTime;
            float progress = t / openDuration;
            float ease = 1f - Mathf.Pow(1 - progress, 2);
            lid.localRotation = Quaternion.Lerp(start, end, ease);
            yield return null;
        }
        lid.localRotation = end;

        GiveReward();
    }

    // 开箱后：随机抽一种奖励类型，再走对应分支
    void GiveReward()
    {
        ChestRewardType t = RollRewardType();

        if (t == ChestRewardType.Card) GiveCardReward();
        else if (t == ChestRewardType.Chip) GiveChipReward();
        else if (t == ChestRewardType.Hide) GiveHideReward();
    }

    // 按权重随机抽一种奖励类型
    ChestRewardType RollRewardType()
    {
        if (rewardTypes == null || rewardTypes.Count == 0)
            return ChestRewardType.Card;   // 没配就默认卡牌

        int total = 0;
        for (int i = 0; i < rewardTypes.Count; i++)
            total += rewardTypes[i].weight;

        if (total <= 0) return ChestRewardType.Card;

        int r = Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < rewardTypes.Count; i++)
        {
            acc += rewardTypes[i].weight;
            if (r < acc) return rewardTypes[i].type;
        }

        return rewardTypes[rewardTypes.Count - 1].type;
    }

    // 卡牌奖励：抽 rewardCount 张，立起来、背面朝你
    void GiveCardReward()
    {
        if (rewardPool == null || rewardPool.Count == 0)
        {
            Debug.LogError("宝箱 rewardPool 没设置！把 52 张卡拖进来");
            return;
        }

        List<CardDataSO> chosen = PickWeighted(rewardPool, rewardCount);

        for (int i = 0; i < chosen.Count; i++)
        {
            Card card = SpawnCard(chosen[i], i, chosen.Count);
            if (card != null) rewardCards.Add(card);
        }
    }

    // 筹码奖励：以后填
    void GiveChipReward()
    {
        Debug.Log("[宝箱] 开出筹码（暂未实现）");
        StartCoroutine(ReturnToMap());
    }

    // 兽皮奖励：以后填
    void GiveHideReward()
    {
        Debug.Log("[宝箱] 开出兽皮（暂未实现）");
        StartCoroutine(ReturnToMap());
    }

    // 生一张卡：支架朝镜头（让卡立起来），卡挂支架下面、背面朝上
    Card SpawnCard(CardDataSO data, int index, int total)
    {
        GameObject prefab = cardPrefab;
        if (prefab == null && DeckManager.Instance != null)
            prefab = DeckManager.Instance.cardPrefab;
        if (prefab == null)
        {
            Debug.LogError("卡牌预制体没设置！");
            return null;
        }

        // 落点：箱子前，三张横向排开
        Vector3 pos = transform.position + cardSpawnOffset;
        pos.x += (index - (total - 1) / 2f) * cardSpacing;

        // 支架：正面朝镜头、头朝上（垂直立在桌上）
        Vector3 toCam = Vector3.forward;
        Camera cam = Camera.main;
        if (cam != null)
        {
            toCam = cam.transform.position - pos;
            toCam.y = 0;
        }
        if (toCam.sqrMagnitude < 0.0001f) toCam = Vector3.forward;

        GameObject stand = new GameObject("RewardCard");
        stand.transform.position = pos;
        stand.transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);

        GameObject go = Instantiate(prefab, stand.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        Card card = go.GetComponent<Card>();
        if (card == null)
        {
            Destroy(stand);
            return null;
        }

        card.Init(data, true);   // Init 里默认扣着（背面朝上）

        // 关掉卡自带的拖拽交互（那是战斗里用的），改用宝箱点击
        CardDisplay cd = go.GetComponent<CardDisplay>();
        if (cd != null) cd.enabled = false;

        // 挂点击：翻面/放大、再点选中
        ChestCardClick click = go.GetComponent<ChestCardClick>();
        if (click == null) click = go.AddComponent<ChestCardClick>();
        click.chest = this;

        return card;
    }

    // 玩家点了一张奖励卡
    public void OnRewardCardClicked(Card card)
    {
        if (_picking) return;

        if (card.IsFaceDown)
        {
            // 第一次点：放大一下 + 翻到正面
            StartCoroutine(ZoomAndFlip(card));
        }
        else
        {
            // 第二次点：选中这张
            PickCard(card);
        }
    }

    // 放大（让你知道点的是哪张）→ 翻面 → 缩回
    IEnumerator ZoomAndFlip(Card card)
    {
        Vector3 normal = card.transform.localScale;
        Vector3 big = normal * zoomScale;

        float t = 0;
        while (t < zoomDuration)
        {
            t += Time.deltaTime;
            card.transform.localScale = Vector3.Lerp(normal, big, t / zoomDuration);
            yield return null;
        }

        yield return StartCoroutine(card.FlipAnim());

        t = 0;
        while (t < zoomDuration)
        {
            t += Time.deltaTime;
            card.transform.localScale = Vector3.Lerp(big, normal, t / zoomDuration);
            yield return null;
        }
    }

    // 选中：写进永久牌堆，销毁另外两张，回地图
    void PickCard(Card card)
    {
        if (_picking) return;
        _picking = true;

        if (card.Data != null)
        {
            GameProgress.playerDeck.Add(card.Data);
            Debug.Log("[宝箱] 拿到 " + card.CardName + "，加入牌堆");
        }

        for (int i = 0; i < rewardCards.Count; i++)
        {
            if (rewardCards[i] != null && rewardCards[i] != card)
                Destroy(rewardCards[i].gameObject);
        }

        StartCoroutine(ReturnToMap());
    }

    IEnumerator ReturnToMap()
    {
        yield return new WaitForSeconds(0.5f);   // 停一下让玩家看清选了啥

        string mapScene = "Map";
        if (MapManager.Instance != null) mapScene = MapManager.Instance.mapSceneName;
        SceneManager.LoadScene(mapScene);
    }

    // 权重 = 1/点数 抽 count 张（去重）
    List<CardDataSO> PickWeighted(List<CardDataSO> pool, int count)
    {
        List<CardDataSO> result = new List<CardDataSO>();
        List<CardDataSO> remaining = new List<CardDataSO>(pool);

        for (int i = 0; i < count && remaining.Count > 0; i++)
        {
            // 先算总权重
            float total = 0;
            for (int j = 0; j < remaining.Count; j++)
            {
                total += 1f / (float)remaining[j].GetPower();
            }

            // 摇一个数，按权重走到哪张就是哪张
            float r = Random.value * total;
            float acc = 0;
            int pick = 0;
            for (int j = 0; j < remaining.Count; j++)
            {
                acc += 1f / (float)remaining[j].GetPower();
                if (r <= acc)
                {
                    pick = j;
                    break;
                }
            }

            result.Add(remaining[pick]);
            remaining.RemoveAt(pick);
        }

        return result;
    }
}
