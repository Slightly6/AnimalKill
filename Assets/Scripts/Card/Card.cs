using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
/// <summary>
/// 一张扑克牌动物卡。战力 = 攻 = 血，一个数值。
/// </summary>
public class Card : MonoBehaviour
{
    [Header("渲染（拖入）")]
    public TextMeshPro[] rankTexts;          // 正面花色点数文字
    public TextMeshPro bonusText;            // 战力加/减的浮动文字（+1 / -1，没拖就空着不显示）
    public MeshRenderer frontRenderer;       
    public float frontArtScale = 0.12f;      // 正面动物图大小
    // ---- 运行时状态 ----
    public CardDataSO Data;
    public int CurrentPower;// 当前力量
    [System.NonSerialized] public int PowerBonus;   // 技能累计加的战力（显示在 bonusText 上，+1/-1）
    public bool IsDead;
    public bool IsPlayer;
    //public bool IsPlayed;   // 已经打出去的牌（不能再拖）
    public float flipDuration=0.3f;   // 翻面动画时长

    [System.NonSerialized] public bool IsFaceDown = true;   // 默认扣着（背面朝上），不在 Inspector 显示
    [System.NonSerialized] public SortingGroup sortingGroup;   // 缓存引用，避免每帧 GetComponent
    [System.NonSerialized] public bool IsSelected;   // 这张牌被点选（准备出牌）
    [System.NonSerialized] public bool IsStaged;   // 已拖上桌集结（还没结算）
    [System.NonSerialized] public CardDataSO originalData;   // 拟态变身前的本体数据（弃牌/牌库归属用它）

    // ---- 拟态（CardDataSO.isMimic = true 的牌）----

    public bool IsMimic => Data != null && Data.isMimic;

    // 牌库归属数据：拟态牌无论变成谁，进弃牌堆/统计的都是本体
    public CardDataSO DeckData => originalData != null ? originalData : Data;

    // 完全变成目标牌（点数/花色/技能全复制，视觉同步刷新）
    public void MimicInto(Card target)
    {
        if (target == null || target == this || target.Data == null) return;
        if (originalData == null) originalData = Data;   // 只记第一次，本体不丢
        Data = target.Data;
        RefreshDisplay();
    }

    // 打出时随机变成手里一张（含自己 = 概率保持原样）
    public void MimicRandomly(List<Card> hand)
    {
        var pool = new List<Card>();
        if (hand != null) pool.AddRange(hand);
        if (!pool.Contains(this)) pool.Add(this);
        pool.RemoveAll(c => c == null);
        if (pool.Count == 0) return;
        Card target = pool[Random.Range(0, pool.Count)];
        MimicInto(target);
        Debug.Log($"[拟态] {originalData.name} 打出时变成了 {Data.name}");
    }

    // 显回原形（拟态牌自己排到最左时调用）
    public void RestoreFromMimic()
    {
        if (originalData == null) return;   // 没变过身
        Data = originalData;
        originalData = null;
        RefreshDisplay();
    }
    // 已废弃：统一用 Data.effects

    private Renderer[] cardRenderers;
    private MaterialPropertyBlock propBlock;// 材质属性块
    private float selectGlow;      // 选中扫光 平滑到 0/1
    private const string SHADER_NAME = "Custom/PlayingCard";

    CardEffectBells effectBells;   // 效果铃铛标记（预制体上没挂 = null，不影响）

    void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
        effectBells = GetComponent<CardEffectBells>();
        // 只收集挂着 PlayingCard shader 的 MeshRenderer（自动排除 TextMeshPro 文字渲染器）
        var all = GetComponentsInChildren<Renderer>(true);
        var list = new List<Renderer>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].sharedMaterial != null && all[i].sharedMaterial.shader != null
                && all[i].sharedMaterial.shader.name == SHADER_NAME)
            {
                list.Add(all[i]);
            }
        }
        cardRenderers = list.ToArray();
        propBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (cardRenderers == null || cardRenderers.Length == 0) return;
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        float target = IsSelected ? 1f : 0f;
        selectGlow = Mathf.MoveTowards(selectGlow, target, Time.deltaTime / 0.12f);

        if (selectGlow <= 0f && target <= 0f) return;

        for (int i = 0; i < cardRenderers.Length; i++)
        {
            cardRenderers[i].GetPropertyBlock(propBlock);   // 先读出已有属性（含牌面 _MainTex），否则下面 Set 会把纹理冲掉
            propBlock.SetFloat("_SelectGlow", selectGlow);
            cardRenderers[i].SetPropertyBlock(propBlock);
        }
    }

    public IEnumerator StandUp(float duration = 0.15f)
    {
        Quaternion from = transform.rotation;
        // 直立朝向：面向玩家（和手牌直立时同角度，抄你手牌的rotation）
        Quaternion to = Quaternion.Euler(0, 180, 0);
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(from, to, t / duration);
            yield return null;
        }
        transform.rotation = to;
    }
    //private Coroutine skillJitterRoutine;   // 旧动物战斗抖动协程（已注释）
    internal Vector3 position;


    public void Init(CardDataSO data, bool isPlayer, int bonusPower = 0, bool forceAwakened = false)
    {
        if (data == null)
        {
            Debug.LogError("[卡牌] Init 收到空数据（牌组里有失效引用，重新生成卡牌后要点 DeckManager 的「自动填入52张卡」）");
            return;
        }
        Data = data;
        originalData = null;   // 拟态变身记录清零（对象池/复用时安全）
        IsPlayer = isPlayer;
        PowerBonus = 0;   // 技能加的战力，每张牌从 0 开始
        IsDead = false;

        // 技能状态全部清零（对象池/复用时也安全）
        // 已废弃：效果直接从 Data.effects 读取

        RefreshDisplay();
    

      

        SetFaceDown(IsFaceDown);
    }

    // 瞬间切换正反面：绕 Y 轴转（0° 正面朝上，180° 背面朝上），文字只在正面显示
    public void SetFaceDown(bool faceDown)
    {
        IsFaceDown = faceDown;
        transform.localRotation = Quaternion.Euler(0, faceDown ? 0f : 180f, 0);
        // SetTextsVisible(!faceDown);
    }

    // 把这张牌（含所有子物体）切到指定 Layer。
    // 入手牌 → 切到手牌层（HandCamera 只渲染这层，永远在最上层）；
    // 出手牌 → 切回 Default，恢复正常遮挡。
    public void SetLayerRecursively(int layer)
    {
        SetLayerRecursive(transform, layer);
    }

    void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursive(t.GetChild(i), layer);
    }

    // 点数文字只在正面显示（背面时被背图盖住，这里只控文字的显隐）
    void SetTextsVisible(bool showFront)
    {
        for (int i = 0; i < rankTexts.Length; i++)
            if (rankTexts[i] != null) rankTexts[i].gameObject.SetActive(showFront);
        RefreshBonusText();   // 加/减的文字只在正面显示，且只有非 0 才显示
    }

    // 翻面动画：绕 Y 轴从当前面转到另一面（像翻真卡）
    public IEnumerator FlipAnim()
    {
        AudioManager.Instance.PlayFlip();   // 翻面音效
        float fromY = IsFaceDown ? 0f : 180f;
        float toY = IsFaceDown ? 180f : 0f;

        float t = 0;
        while (t < flipDuration)
        {
            t += Time.deltaTime;
            float p = t / flipDuration;
            transform.localRotation = Quaternion.Euler(0, Mathf.Lerp(fromY, toY, p), 0);
            yield return null;
        }

        IsFaceDown = !IsFaceDown;
        transform.localRotation = Quaternion.Euler(0, toY, 0);
        // SetTextsVisible(!IsFaceDown);
    }

    public IEnumerator FlatFlipAnim()
    {
        // AudioManager.Instance.PlayFlip();   // 翻面音效  （排查卡死：暂时关闭）
        Debug.Log("[翻面] FlatFlipAnim 开始");
        Quaternion flatDown = Quaternion.Euler(90, 0, 0);   // 平放、面朝下
        float from = IsFaceDown ? 0f : 180f;
        float to = IsFaceDown ? 180f : 0f;

        float t = 0;
        while (t < flipDuration)
        {
            if (this == null) yield break;  
            t += Time.deltaTime;
            float p = t / flipDuration;
            float angle = Mathf.Lerp(from, to, p);
            transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward) * flatDown;
            yield return null;
        }

        IsFaceDown = !IsFaceDown;
        transform.localRotation = Quaternion.AngleAxis(to, Vector3.forward) * flatDown;
        // SetTextsVisible(!IsFaceDown);
    }

    public void RefreshDisplay()
    {
        if (effectBells != null) effectBells.Refresh();   // 效果铃铛跟着牌面一起刷新

        // 有整张牌面贴图：直接换掉正面 _MainTex，隐藏点数文字
        if (Data.faceTexture != null && frontRenderer != null)
        {
            MaterialPropertyBlock faceBlock = new MaterialPropertyBlock();
            frontRenderer.GetPropertyBlock(faceBlock);
            faceBlock.SetTexture("_MainTex", Data.faceTexture);
            frontRenderer.SetPropertyBlock(faceBlock);

            for (int i = 0; i < rankTexts.Length; i++)
                if (rankTexts[i] != null) rankTexts[i].gameObject.SetActive(false);
            return;
        }

        // 没贴图：回退到花色+点数文字
        string rankStr = Data.GetRankText();
        string suitStr = Data.GetSuitSymbol();

        for (int i = 0; i < rankTexts.Length; i++)
        {
            if (rankTexts[i] != null)
                rankTexts[i].text = suitStr + rankStr;
        }
    }


    // 把累计的战力加成显示到 bonusText（+1 / -1），没有加成或没拖文字就不显示
    void RefreshBonusText()
    {
        if (bonusText == null) return;
        bool show = !IsFaceDown && PowerBonus != 0;
        bonusText.gameObject.SetActive(show);
        if (show) bonusText.text = PowerBonus > 0 ? "+" + PowerBonus : PowerBonus.ToString();
    }

    // 打出时触发所有效果（按 timing 分流）
    public void ApplyEffects()
    {
        if (IsDead || Data == null || Data.effects == null) return;
        foreach (var effect in Data.effects)
        {
            if (effect == null) continue;
            switch (effect.timing)
            {
                case EffectTiming.Immediate:
                    Debug.Log($"[即时] {effect.GetType().Name}");
                    effect.Apply((int)Data.rank);   // 传入本牌点数（按点数结算的效果用它）
                    break;
                case EffectTiming.Delayed:
                    BattleManager.Instance.QueueDelayed(effect);
                    break;
                case EffectTiming.Wave:
                    BattleManager.Instance.QueueWave(effect);
                    break;
            }
        }
    }
    void Die()
    {
        if (IsDead) return;
        IsDead = true;
        AudioManager.Instance.PlayDeath();   // 死亡音效

        EventBus.Publish(new CardDiedEvent
        {
            card = this,
            laneIndex = -1,   // 槽位已删除
            isPlayerSide = IsPlayer
        });

        StartCoroutine(DeathAnim());
    }

    // ========== 动画 ==========

    System.Collections.IEnumerator DeathAnim()
    {
        // 从躺平的槽位里解出来到世界坐标，才能真正向"上"弹飞（槽位局部 Y 是水平的）
        Vector3 startScale = transform.lossyScale;
        transform.SetParent(null, true);
        transform.localScale = startScale;

        float duration = 0.32f;
        float t = 0;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float spinDir = Random.value > 0.5f ? 1f : -1f;   // 随机往左/往右翻倒

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            // 缩小：前慢后快（被打飞后才消失）
            float scaleK = 1f - Mathf.Pow(p, 2.5f);
            transform.localScale = startScale * scaleK;
            // 翻转：沿前进轴翻倒 + 轻微竖直轴乱转
            transform.rotation = startRot
                * Quaternion.AngleAxis(p * 100f * spinDir, Vector3.forward)
                * Quaternion.AngleAxis(p * 60f * spinDir, Vector3.up);
            // 向上弹一下再落回（被砸飞的抛物线）
            transform.position = startPos + Vector3.up * Mathf.Sin(p * Mathf.PI) * 0.4f;
            yield return null;
        }

        Destroy(gameObject);
    }

    void OnMouseOver()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (effectBells != null && effectBells.HoveringBell) return;   // 右键点在铃铛上，让铃铛自己显示效果说明

            if (Data == null) return;
            PhoneDisplay.Instance.ShowCard(Data);   // 卡牌信息投到手机屏幕上
        }
    }
    void OnMouseExit()
    {
        PhoneDisplay.Instance.SlideBack();   // 移开卡牌，手机滑回起点
    }
}
