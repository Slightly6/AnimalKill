using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 钩子：玩家从手牌里多选 1~5 张牌，点钩子挂上去，按牌型获得本关增益，每关限一次。
/// 挂在 Hook 物体上（物体本身带 BoxCollider，点击就挂）。5 个挂点 + Card 预制体拖进来。
/// 挂上的牌只是数据展示；手牌实体直接飞到钩子上，过关清场，下关洗牌时牌回到牌堆。
/// </summary>
public class HookDisplay : MonoBehaviour
{
    [Header("挂点（Hook 下建 5 个空物体，摆好，按顺序拖进来）")]
    public Transform[] hangPoints = new Transform[5];

    [Header("卡牌预制体（拖 Card.prefab）")]
    public GameObject cardPrefab;

    [Header("挂上去的牌外观")]
    public float cardScale = 0.6f;   // 牌缩放（比手牌的 0.7 小一圈）
    public float hangDrop = 0.78f;   // 牌中心离挂点往下多远 = 1.3×cardScale（牌高2.6的一半），改 cardScale 要跟着改

    [Header("新牌挂上来时，老牌被撞一下")]
    public float bumpKick = 30f;     // 踢一脚的角速度

    List<GameObject> spawned = new List<GameObject>();   // 当前挂着的牌（记的是支点物体）
    bool migrating = false;   // 手牌正在飞向钩子：事件重建时不补生成假牌

    void Start()
    {
        EventBus.Subscribe<TrophyChangedEvent>(OnTrophyChanged);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<TrophyChangedEvent>(OnTrophyChanged);
    }

    // 点钩子：把当前多选的手牌挂上去
    void OnMouseDown()
    {
        if (GameProgress.InputLocked) return;   // 卷轴地图打开/切关过渡时不能挂
        if (CardDisplay.draggingCard != null) return;   // 正在拖牌，别误触
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.HookLocked)
        {
            Debug.Log("[钩子] 本关已经挂过牌了");
            Narrator.Say(SpeakTopic.HookAlreadyHung);
            return;
        }

        // 只在战斗关的摸牌/出牌阶段能挂（和桌上道具同一时间窗）
        if (BattleManager.Instance == null) return;
        TurnPhase p = BattleManager.Instance.CurrentPhase;
        if (p != TurnPhase.Draw && p != TurnPhase.Play) return;

        // 收集当前多选的手牌（过滤掉已经打出/销毁的残留引用）
        List<Card> picked = new List<Card>();
        for (int i = 0; i < CardDisplay.selectedCards.Count; i++)
        {
            CardDisplay cd = CardDisplay.selectedCards[i];
            if (cd == null || cd.Card == null) continue;
            Card c = cd.Card;
            if (!c.IsPlayer || c.IsPlayed || c.Data == null) continue;
            if (!picked.Contains(c)) picked.Add(c);
        }

        if (picked.Count == 0)
        {
            Debug.Log("[钩子] 先点选手牌（1~" + hangPoints.Length + " 张），再点钩子挂牌");
            Narrator.Say(SpeakTopic.HookNoCard);
            return;
        }
        if (picked.Count > hangPoints.Length)
        {
            Debug.Log("[钩子] 最多挂 " + hangPoints.Length + " 张，当前选了 " + picked.Count + " 张");
            Narrator.Say(SpeakTopic.HookTooMany);
            return;
        }

        // 先取数据，再为每张牌在挂点上建「空支点」占位（先不实例化牌），
        // 这样 HangCards 发出的 TrophyChangedEvent 触发 Rebuild 时数量已对上，不会补生成假牌
        List<CardDataSO> dataList = new List<CardDataSO>();
        List<GameObject> pivots = new List<GameObject>();
        for (int i = 0; i < picked.Count; i++)
        {
            dataList.Add(picked[i].Data);
            pivots.Add(CreatePivot(picked[i].Data, i));
        }

        migrating = true;
        HandType? result = GameManager.Instance.HangCards(dataList);
        if (result == null)
        {
            // 没挂成（锁定/游戏结束）：撤销占位支点
            migrating = false;
            for (int i = 0; i < pivots.Count; i++)
            {
                spawned.Remove(pivots[i]);
                if (pivots[i] != null) Destroy(pivots[i]);
            }
            return;
        }

        // 挂成功：牌从手牌数据移除、禁交互，实体不销毁，直接飞向钩子
        for (int i = 0; i < picked.Count; i++)
        {
            if (DeckManager.Instance != null)
                DeckManager.Instance.RemoveFromHand(picked[i]);
            CardDisplay cd = picked[i].GetComponent<CardDisplay>();
            if (cd != null) cd.enabled = false;
            picked[i].IsSelected = false;
        }
        CardDisplay.selectedCards.Clear();

        StartCoroutine(FlyAllToHooks(picked, pivots));
    }

    // 在第 index 个挂点建一个空支点（牌稍后飞过来挂上）
    GameObject CreatePivot(CardDataSO data, int index)
    {
        Transform point = hangPoints[index];
        GameObject pivot = new GameObject("Hang_" + (data != null ? data.GetRankText() : index.ToString()));
        pivot.transform.SetParent(point, false);
        pivot.transform.localPosition = Vector3.zero;
        pivot.transform.localRotation = Quaternion.identity;
        spawned.Add(pivot);
        return pivot;
    }

    // 逐张飞向各自支点（复用宝箱选牌的 EaseIn 手感），全部挂好后结束迁移态
    IEnumerator FlyAllToHooks(List<Card> cards, List<GameObject> pivots)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            yield return FlyOneToHook(cards[i], pivots[i]);

            // 新牌挂上：踢一脚之前挂好的牌（钩子被撞一起晃）
            for (int j = 0; j < i; j++)
            {
                HangingCard h = pivots[j].GetComponent<HangingCard>();
                if (h != null) h.Bump(bumpKick);
            }
            yield return new WaitForSeconds(0.06f);   // 逐张错峰
        }
        migrating = false;
    }

    // 一张手牌飞到支点下方并挂住：位移 EaseIn + 缩到钩子牌尺寸 + 转向钩子姿态
    IEnumerator FlyOneToHook(Card card, GameObject pivot)
    {
        if (card == null || pivot == null) yield break;

        SortingGroup sg = card.sortingGroup;
        if (sg != null) sg.sortingOrder = 100;   // 飞行途中盖在最上面

        Vector3 startPos = card.transform.position;
        Quaternion startRot = card.transform.rotation;
        Vector3 startScale = card.transform.localScale;

        // 钩子牌终态（和原 SpawnCard 一致）：支点正下方、cardScale、正面朝外（Y 转 180°）
        Vector3 endPos = pivot.transform.TransformPoint(new Vector3(0f, -hangDrop, 0f));
        Quaternion endRot = pivot.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        Vector3 endScale = Vector3.one * cardScale;

        float duration = 0.4f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float ease = p * p;   // EaseIn：起手慢、挂上时快（同宝箱选牌动画）

            if (card == null) yield break;
            card.transform.position = Vector3.Lerp(startPos, endPos, ease);
            card.transform.rotation = Quaternion.Slerp(startRot, endRot, ease);
            card.transform.localScale = Vector3.Lerp(startScale, endScale, ease);
            yield return null;
        }

        if (card == null) yield break;

        // 挂入支点：归零局部变换，开始单摆
        card.transform.SetParent(pivot.transform, false);
        card.transform.localPosition = new Vector3(0f, -hangDrop, 0f);
        card.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        card.transform.localScale = endScale;
        card.SetFaceDown(false);   // 正面朝外、显示点数文字
        if (sg != null) sg.sortingOrder = 0;

        pivot.AddComponent<HangingCard>();
    }

    void OnTrophyChanged(TrophyChangedEvent e)
    {
        Rebuild();
    }

    // 按当前战利品重建钩子上的牌
    void Rebuild()
    {
        if (GameManager.Instance == null) return;

        int count = GameManager.Instance.TrophyCount;

        // 下一关 / 重开关卡（0 张）：全清空
        if (count == 0)
        {
            ClearAll();
            return;
        }

        // 手牌正在飞过来：支点已由点击流程建好，牌由飞行动画自己挂，不补生成
        if (migrating) return;

        // 有新牌挂上来：先踢一脚旧牌（钩子被撞，一起晃）
        for (int i = 0; i < spawned.Count; i++)
        {
            HangingCard h = spawned[i].GetComponent<HangingCard>();
            if (h != null) h.Bump(bumpKick);
        }

        // 只把多出来的新牌挂上去（旧牌一直留着，不用重建）
        for (int i = spawned.Count; i < count; i++)
        {
            SpawnCard(GameManager.Instance.GetTrophyCard(i), i);
        }
    }

    // 在第 index 个挂点挂一张牌
    void SpawnCard(CardDataSO data, int index)
    {
        if (data == null || cardPrefab == null) return;
        if (index < 0 || index >= hangPoints.Length) return;
        Transform point = hangPoints[index];
        if (point == null) return;

        // 支点：空物体放在挂点，牌挂在它下面，绕着它荡
        GameObject pivot = new GameObject("Hang_" + data.GetRankText());
        pivot.transform.SetParent(point, false);
        pivot.transform.localPosition = Vector3.zero;
        pivot.transform.localRotation = Quaternion.identity;

        // 牌本体：正面朝外、缩小、挂在支点下方
        GameObject go = Instantiate(cardPrefab, pivot.transform);
        go.transform.localPosition = new Vector3(0f, -hangDrop, 0f);
        go.transform.localScale = Vector3.one * cardScale;

        Card card = go.GetComponent<Card>();
        if (card != null)
        {
            card.Init(data, false);      // 只借它的外观，阵营填敌方（本身就不能拖）
            card.SetFaceDown(false);     // 正面朝外
        }

        // 关掉拖拽（挂着的牌不能拿）
        CardDisplay display = go.GetComponent<CardDisplay>();
        if (display != null) display.enabled = false;

        // 套上摆动物理（在支点上，绕着支点荡）
        pivot.AddComponent<HangingCard>();

        spawned.Add(pivot);
    }

    void ClearAll()
    {
        migrating = false;
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null) Destroy(spawned[i]);
        }
        spawned.Clear();
    }
}
