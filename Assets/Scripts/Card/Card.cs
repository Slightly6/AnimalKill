using UnityEngine;
using TMPro;
using System.Collections;
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
    public Sprite stackedSkillIcon;          // 叠加得到的技能图标（献祭来的，没叠是 null）
    // ---- 运行时状态 ----
    public CardDataSO Data;
    public int CurrentPower;// 当前力量
    [System.NonSerialized] public int PowerBonus;   // 技能累计加的战力（显示在 bonusText 上，+1/-1）
    public bool IsDead;
    public bool IsPlayer;
    public bool IsPlayed;   // 已经打出去的牌（不能再拖）
    public float flipDuration=0.3f;   // 翻面动画时长
    public float ScaleX=0.8f;
    public float arcHeight=1.2f;          // 半圆弧猛冲的高度（跳多高）
    public float tiltAngle=30f;           // 猛冲时前倾的角度
    public float rushDuration=0.12f;      // 猛冲出去的时间（快，爆发感）
    public float returnDuration=0.22f;     // 回原位的时间（稍慢，带弹性回弹）
    public float thrustPivotOffset=0.9f;  // 后仰支点离中心多远 = 半张牌长（牌高2.6×缩放0.7÷2）
    public float recoilAngle=22f;         // 被打后仰的角度（大一点才有被砸中的感觉）
    [System.NonSerialized] public bool IsFaceDown = true;   // 默认扣着（背面朝上），不在 Inspector 显示
    [System.NonSerialized] public SortingGroup sortingGroup;   // 缓存引用，避免每帧 GetComponent
    [System.NonSerialized] public bool IsSelected;   // 这张牌被点选（准备出牌）

    // ---- 卡牌 Shader（Custom/PlayingCard）反馈参数，用 PropertyBlock 每卡独立、不实例化材质 ----
    private Renderer[] cardRenderers;
    private MaterialPropertyBlock propBlock;
    private float hitFlash;        // 受击红闪 1→0
    private float selectGlow;      // 选中扫光 平滑到 0/1
    private const string SHADER_NAME = "Custom/PlayingCard";

    public string CardName { get { return Data.animalName; } }

    void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();

        // 只收集挂着 PlayingCard shader 的 MeshRenderer（自动排除 TextMeshPro 文字渲染器）
        var all = GetComponentsInChildren<Renderer>(true);
        var list = new System.Collections.Generic.List<Renderer>(all.Length);
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

        // 受击红闪 0.32 秒衰减
        hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.deltaTime / 0.32f);
        // 选中扫光平滑过渡（避免硬切）
        float target = IsSelected ? 1f : 0f;
        selectGlow = Mathf.MoveTowards(selectGlow, target, Time.deltaTime / 0.12f);

        if (hitFlash <= 0f && selectGlow <= 0f && target <= 0f) return;

        for (int i = 0; i < cardRenderers.Length; i++)
        {
            cardRenderers[i].GetPropertyBlock(propBlock);
            propBlock.SetFloat("_HitFlash", hitFlash);
            propBlock.SetFloat("_SelectGlow", selectGlow);
            cardRenderers[i].SetPropertyBlock(propBlock);
        }
    }

    // 被打中时整牌红闪一下（受击动画/死亡时调用）
    public void FlashHit()
    {
        hitFlash = 1f;
    }

    public void Init(CardDataSO data, bool isPlayer, int bonusPower = 0)
    {
        Data = data;
        IsPlayer = isPlayer;
        CurrentPower = Data.GetPower() + bonusPower;   // 基础战力 + 运行时加成（觉醒/额外）
        PowerBonus = 0;   // 技能加的战力，每张牌从 0 开始
        IsDead = false;
        RefreshDisplay();

        // 换正面动物图（材质贴图，运行时替换）
        if (frontRenderer != null && Data.artwork != null)
        {
            frontRenderer.material.mainTexture = Data.artwork.texture;
        }

        // 技能图标：有叠加技能显示叠加的，否则显示卡牌自带技能图标
        if (skillIconRenderer != null)
        {
            Sprite icon = stackedSkillIcon != null ? stackedSkillIcon : Data.abilityIcon;
            if (icon != null) skillIconRenderer.material.mainTexture = icon.texture;
        }

        SetFaceDown(IsFaceDown);
    }

    // 瞬间切换正反面：绕 Y 轴转（0° 正面朝上，180° 背面朝上），文字只在正面显示
    public void SetFaceDown(bool faceDown)
    {
        IsFaceDown = faceDown;
        transform.localRotation = Quaternion.Euler(0, faceDown ? 0f : 180f, 0);
        SetTextsVisible(!faceDown);
    }

    // 点数文字只在正面显示（背面时被背图盖住，这里只控文字的显隐）
    void SetTextsVisible(bool showFront)
    {
        for (int i = 0; i < rankTexts.Length; i++)
            if (rankTexts[i] != null) rankTexts[i].gameObject.SetActive(showFront);
        RefreshBonusText();   // 加/减的文字只在正面显示，且只有非 0 才显示
    }

    // 把某个技能图标叠到这张牌上（奖励关献祭后调用）
    public void ApplyStackedSkill(Sprite icon)
    {
        stackedSkillIcon = icon;
        if (skillIconRenderer != null)
        {
            if (icon != null) skillIconRenderer.material.mainTexture = icon.texture;
            skillIconRenderer.gameObject.SetActive(icon != null);
        }
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
        SetTextsVisible(!IsFaceDown);
    }

    // 平着翻面（抽牌用）：牌躺在牌堆上，绕世界 Z 轴（长边）翻过去露出正面。
    // 宝箱还走上面的 FlipAnim（绕 Y 轴竖着翻），这里单独给抽牌加个平翻，互不影响。
    public IEnumerator FlatFlipAnim()
    {
        AudioManager.Instance.PlayFlip();   // 翻面音效
        Quaternion flatDown = Quaternion.Euler(90, 0, 0);   // 平放、面朝下
        float from = IsFaceDown ? 0f : 180f;
        float to = IsFaceDown ? 180f : 0f;

        float t = 0;
        while (t < flipDuration)
        {
            t += Time.deltaTime;
            float p = t / flipDuration;
            float angle = Mathf.Lerp(from, to, p);
            transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward) * flatDown;
            yield return null;
        }

        IsFaceDown = !IsFaceDown;
        transform.localRotation = Quaternion.AngleAxis(to, Vector3.forward) * flatDown;
        SetTextsVisible(!IsFaceDown);
    }

    public void RefreshDisplay()
    {
        // 正面显示当前战力点数
        string rankStr = PowerToRankString(CurrentPower);
        string suitStr = Data.GetSuitSymbol();

        for (int i = 0; i < rankTexts.Length; i++)
        {
            if (rankTexts[i] != null)
                rankTexts[i].text = suitStr + rankStr;
        }
    }

    // 加/减战力（技能用）。delta 正数加、负数减，并在 bonusText 上显示 +N / -N。
    public void AddPower(int delta)
    {
        if (delta == 0) return;
        CurrentPower += delta;
        if (CurrentPower < 1) CurrentPower = 1;   // 战力最低 1，别减成 0 或负数
        PowerBonus += delta;
        RefreshDisplay();
        RefreshBonusText();
    }

    // 把累计的战力加成显示到 bonusText（+1 / -1），没有加成或没拖文字就不显示
    void RefreshBonusText()
    {
        if (bonusText == null) return;
        bool show = !IsFaceDown && PowerBonus != 0;
        bonusText.gameObject.SetActive(show);
        if (show) bonusText.text = PowerBonus > 0 ? "+" + PowerBonus : PowerBonus.ToString();
    }

    // 战力数值 → 点数文字  例: 1→A  13→K  8→8
    string PowerToRankString(int power)
    {
        if (power <= 1) return "A";
        if (power >= 13) return "K";
        if (power >= 12) return "Q";
        if (power >= 11) return "J";
        return power.ToString();   // 2~10
    }

    // ========== 战斗 ==========

    // 单次伤害：把 damage 打到 target 身上，处理死亡
    void DealDamage(Card target, int damage)
    {
        target.CurrentPower -= damage;
        AudioManager.Instance.PlayHit();   // 命中音效

        EventBus.Publish(new CardAttackedEvent
        {
            attacker = this,
            target = target,
            damage = damage
        });

        // 从攻击者指向目标的方向（目标朝这个方向后仰）
        Vector3 hitDir = target.transform.position - transform.position;
        hitDir.y = 0;
        hitDir.Normalize();

        if (target.CurrentPower <= 0)
        {
            // 打死：红闪一下，死亡动画接管
            target.CurrentPower = 0;
            target.FlashHit();
            target.Die();
        }
        else
        {
            // 没死：后仰 + 刷新战力
            target.PlayHitReaction(hitDir);
            target.RefreshDisplay();
        }
    }

    // 被打的反馈：红闪 + 后仰一下再回正（攻击方调用，没打死时）
    public void PlayHitReaction(Vector3 hitDir)
    {
        FlashHit();
        StartCoroutine(HitReactionRoutine(hitDir));
    }

    IEnumerator HitReactionRoutine(Vector3 hitDir)
    {
        Vector3 homePos = transform.position;
        Quaternion homeRot = transform.rotation;

        // ① 后仰：被猛砸一下，快速甩出去（OutCubic）
        Vector3 pivot = homePos + hitDir * thrustPivotOffset;   // 背对攻击者那一侧当支点
        Vector3 axis = Vector3.Cross(Vector3.up, hitDir);
        yield return CardAnimator.ThrustOut(transform, pivot, axis, recoilAngle, 0.06f, CardAnimator.Ease.OutCubic);

        // ② 回正：带轻微过冲，像有弹性的牌（OutBack）
        yield return CardAnimator.MoveAndRotate(transform, homePos, homeRot, 0.16f, CardAnimator.Ease.OutBack);

        // ③ 落地余震：小幅快速抖两下
        yield return CardAnimator.Jitter(transform, homePos, 0.05f, 0.09f);
    }

    // 通用攻击动作：前倾 + 半圆弧猛冲过去，命中瞬间停顿+震屏，再弹回原位。
    public IEnumerator StrikeAndReturn(Card target)
    {
        Vector3 homePos = transform.position;
        Quaternion homeRot = transform.rotation;

        // 攻击方向（桌面 XZ 平面，指向目标）
        Vector3 dir = target.transform.position - homePos;
        dir.y = 0;
        dir.Normalize();

        // 打出去的牌全程显示在最上面，避免穿模
        int oldOrder = sortingGroup.sortingOrder;
        sortingGroup.sortingOrder = 50;

        // ① 前倾 + 半圆弧猛冲（快，OutQuad 爆发），撞上时保持前倾姿势
        yield return CardAnimator.ArcWithTilt(transform, target.transform.position, arcHeight, tiltAngle, dir, rushDuration);

        // ② 命中：扣血（音效、死亡/后仰都在这里触发）
        int damage = CurrentPower;
        DealDamage(target, damage);
        Debug.Log("[战斗] " + CardName + " 打 " + target.CardName + " " + damage + " 点");

        // ③ 命中停顿 + 震屏：打死卡更久、更猛
        bool killed = target.IsDead;
        if (CameraRig.Instance != null)
            CameraRig.Instance.AddShake(killed ? 0.7f : 0.35f);
        yield return CardAnimator.HitStop(killed ? 0.08f : 0.055f);

        // ④ 弹回原位（OutBack 带回弹过冲）
        yield return CardAnimator.MoveAndRotate(transform, homePos, homeRot, returnDuration, CardAnimator.Ease.OutBack);

        sortingGroup.sortingOrder = oldOrder;
    }

    // 打脸动画：对面没卡，猛冲打脸，命中停顿+震屏，再弹回原位
    public IEnumerator FaceAnim()
    {
        Vector3 homePos = transform.position;
        Quaternion homeRot = transform.rotation;
        // 玩家朝 -Z（打向敌人远端），敌方朝 +Z（打向玩家）
        Vector3 dir = IsPlayer ? -Vector3.forward : Vector3.forward;

        Vector3 reachPos = homePos + dir * 1.0f;   // 冲过去落在对方脸前面一点

        // 打出去的牌全程显示在最上面，避免穿模
        int oldOrder = sortingGroup.sortingOrder;
        sortingGroup.sortingOrder = 50;

        // ① 前倾 + 半圆弧猛冲（爆发）
        yield return CardAnimator.ArcWithTilt(transform, reachPos, arcHeight, tiltAngle, dir, rushDuration);

        // ② 命中：打脸
        int damage = CurrentPower;
        AudioManager.Instance.PlayFace();   // 打脸音效
        if (IsPlayer)
        {
            GameManager.Instance.TransferChips(damage, true);   // 敌人筹码转给我（打脸赢的）
        }
        else
        {
            GameManager.Instance.TransferChips(damage, false);  // 我的筹码转给敌人（被打脸输的）
        }
        Debug.Log("[战斗] " + CardName + " 打脸 " + damage + " 点");

        // ③ 打脸停顿 + 震屏（被打脸更痛，震动稍大）
        if (CameraRig.Instance != null)
            CameraRig.Instance.AddShake(IsPlayer ? 0.45f : 0.55f);
        yield return CardAnimator.HitStop(0.07f);

        // ④ 弹回原位
        yield return CardAnimator.MoveAndRotate(transform, homePos, homeRot, returnDuration, CardAnimator.Ease.OutBack);

        sortingGroup.sortingOrder = oldOrder;
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;
        AudioManager.Instance.PlayDeath();   // 死亡音效

        CardSlot slot = BoardManager.Instance.FindSlotOfCard(this);
        EventBus.Publish(new CardDiedEvent
        {
            card = this,
            laneIndex = slot != null ? slot.laneIndex : -1,
            isPlayerSide = IsPlayer
        });

        Debug.Log("[死亡] " + CardName + " 被消灭");
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
}
