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
    public MeshRenderer frontRenderer;       // 正面动物图
    public MeshRenderer skillIconRenderer;   // 正面技能图标（小）
    public float frontArtScale = 0.12f;      // 正面动物图大小
    // public Sprite stackedSkillIcon;          // 叠加得到的技能图标（献祭来的，没叠是 null）
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
    [System.NonSerialized] public AbilitySO runtimeAbility;

    private Renderer[] cardRenderers;
    private MaterialPropertyBlock propBlock;// 材质属性块
    private float selectGlow;      // 选中扫光 平滑到 0/1
    private const string SHADER_NAME = "Custom/PlayingCard";
    [Header("悬浮提示")]
    public GameObject tooltip; 

    void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
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
        IsPlayer = isPlayer;
        PowerBonus = 0;   // 技能加的战力，每张牌从 0 开始
        IsDead = false;

        // 技能状态全部清零（对象池/复用时也安全）
        runtimeAbility = null;

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

    public void TriggerAbility(AbilityTrigger trigger, Card target)
    {
        if (IsDead) return;
        FireOne(runtimeAbility, trigger, target);
    }

    bool FireOne(AbilitySO ab, AbilityTrigger trigger, Card target)
    {
        if (ab == null || ab.trigger != trigger) return false;
        
        bool anyFired = false;
        foreach (var effect in ab.effects)
        {
            if (effect != null && effect.Apply(this, target))
                anyFired = true;
        }
        return anyFired;
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

        TriggerAbility(AbilityTrigger.OnDeath, null);

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
        if (Input.GetMouseButtonDown(1))   // 右键
        {
            Card card = GetComponent<Card>();
            if (card == null || card.Data == null || tooltip == null) return;

            string desc = string.IsNullOrEmpty(card.Data.description) ? "" : "\n<size=80%>" + card.Data.description;
            tooltip.GetComponentInChildren<TextMeshPro>().text = "<b>" + card.Data.abilityName + "</b>" + desc;
            tooltip.SetActive(true);
        }
    }
    void OnMouseExit()
    {
        if (tooltip != null) tooltip.SetActive(false);
    }
}
