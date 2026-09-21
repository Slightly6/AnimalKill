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

    // ---- 觉醒技能运行时 ----
    // runtimeAbility：这张牌本局生效的技能（玩家=已觉醒 / 敌方=关卡配置觉醒），未觉醒为 null
    [System.NonSerialized] public AbilitySO runtimeAbility;

    // ---- 技能状态（每出一张新牌由 Init 清零；牌活多久状态留多久）----
    [System.NonSerialized] public bool flagGrowAnyDeath;  // ♠2 食腐：场上每死一个 +1
    [System.NonSerialized] public bool flagSkillImmune;   // ♠3 鸮佑：不受敌方技能影响
    [System.NonSerialized] public bool flagEvade;         // ♠4 闪避：被攻击有概率扑空
    [System.NonSerialized] public float evadeChance;      // 闪避概率（0~1）
    [System.NonSerialized] public bool flagColony;        // ♠5 团居：伤害×友军数
    [System.NonSerialized] public bool flagDeathRoll;     // ♠6 死亡翻滚：随机攻击1~5次
    [System.NonSerialized] public bool flagDeepHunter;    // ♠7 深海猎手：技能总开关
    [System.NonSerialized] public int teeth;              // 鲨鱼牙齿数量（0~20）
    [System.NonSerialized] public bool flagDive;          // 鲨鱼潜水态（满20牙进入）
    [System.NonSerialized] public bool diveInvincibleUsed; // 潜水首次被攻击无敌是否已用
    [System.NonSerialized] public HashSet<Card> damagedBy; // 记录谁直接伤过自己（捡牙判定用）

    const int TEETH_MAX = 20;     // 集满20颗牙进入潜水
    const int TEETH_DECAY = 2;    // 潜水后每回合掉2颗

    // ---- 卡牌 Shader（Custom/PlayingCard）反馈参数，用 PropertyBlock 每卡独立、不实例化材质 ----
    private Renderer[] cardRenderers;
    private MaterialPropertyBlock propBlock;
    private float hitFlash;        // 受击红闪 1→0
    private float skillFlash;      // 技能青绿闪 1→0
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
        // 技能闪光 0.5 秒衰减（比受击稍长，让玩家看清）
        skillFlash = Mathf.MoveTowards(skillFlash, 0f, Time.deltaTime / 0.5f);
        // 选中扫光平滑过渡（避免硬切）
        float target = IsSelected ? 1f : 0f;
        selectGlow = Mathf.MoveTowards(selectGlow, target, Time.deltaTime / 0.12f);

        if (hitFlash <= 0f && skillFlash <= 0f && selectGlow <= 0f && target <= 0f) return;

        for (int i = 0; i < cardRenderers.Length; i++)
        {
            cardRenderers[i].GetPropertyBlock(propBlock);
            propBlock.SetFloat("_HitFlash", hitFlash);
            propBlock.SetFloat("_SkillFlash", skillFlash);
            propBlock.SetFloat("_SelectGlow", selectGlow);
            cardRenderers[i].SetPropertyBlock(propBlock);
        }
    }

    // 被打中时整牌红闪一下（受击动画/死亡时调用）
    public void FlashHit()
    {
        hitFlash = 1f;
    }

    // 技能发动时整牌青绿闪 + 轻微抖动（让玩家明确知道效果发生了）
    public void FlashSkill()
    {
        skillFlash = 1f;
        // 避免多次触发叠加协程：已在抖就先停掉旧的
        if (skillJitterRoutine != null) StopCoroutine(skillJitterRoutine);
        skillJitterRoutine = StartCoroutine(SkillJitterRoutine());
    }

    private Coroutine skillJitterRoutine;

    // 技能发动抖动：用 localScale 做"鼓一下+高频小抖"，不动 position/rotation，
    // 避免和攻击/受击/翻面等动画的 transform 写入冲突（之前改 position 会瞬移走卡牌导致特效跟着消失）。
    IEnumerator SkillJitterRoutine()
    {
        Vector3 baseScale = transform.localScale;
        float duration = 0.25f;
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            // 主脉冲：0→0.4 涨到 +12%，0.4→1 落回 1（"被弹一下"）
            float pulse = p < 0.4f
                ? (p / 0.4f) * 0.12f
                : (1f - (p - 0.4f) / 0.6f) * 0.12f;
            // 高频小抖：随时间衰减，模拟"颤"
            float jitter = Mathf.Sin(t * 55f) * 0.04f * (1f - p);
            transform.localScale = baseScale * (1f + pulse + jitter);
            yield return null;
        }
        transform.localScale = baseScale;
        skillJitterRoutine = null;
    }

    public void Init(CardDataSO data, bool isPlayer, int bonusPower = 0, bool forceAwakened = false)
    {
        if (data == null)
        {
            Debug.LogError("[卡牌] Init 收到空数据（牌组里有失效引用，重新生成卡牌后要点 DeckManager 的「自动填入52张卡」）");
            return;
        }
        Data = data;
        IsPlayer = isPlayer;
        CurrentPower = Data.GetPower() + bonusPower;   // 基础战力 + 运行时加成（觉醒/额外）
        PowerBonus = 0;   // 技能加的战力，每张牌从 0 开始
        IsDead = false;

        // 技能状态全部清零（对象池/复用时也安全）
        runtimeAbility = null;
        flagGrowAnyDeath = flagSkillImmune = flagEvade = flagColony = false;
        flagDeathRoll = flagDeepHunter = flagDive = false;
        evadeChance = 0f;
        teeth = 0;
        diveInvincibleUsed = false;
        damagedBy = null;

        // 玩家牌按觉醒名单判定；敌方牌由关卡配置（enemyAwakened）决定
        bool awakened = isPlayer ? GameProgress.IsCardAwakened(data) : forceAwakened;
        if (awakened && data.awakenedAbility != null)
        {
            runtimeAbility = data.awakenedAbility;
            if (runtimeAbility.icon != null && stackedSkillIcon == null)
                stackedSkillIcon = runtimeAbility.icon;
        }

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

    // ========== 技能触发 ==========

    // 触发本机技能（trigger 不匹配的自动跳过）
    // 注意：这里不再调 FlashSkill——出牌挂标记不算"发动"，真正发动由具体逻辑位置触发
    // （食腐/捡牙/闪避/潜水/翻滚等在各自生效点 FlashSkill）
    public void TriggerAbility(AbilityTrigger trigger, Card target)
    {
        if (IsDead) return;
        FireOne(runtimeAbility, trigger, target);
    }

    bool FireOne(AbilitySO ab, AbilityTrigger trigger, Card target)
    {
        if (ab == null || ab.trigger != trigger || ab.effect == null) return false;
        // 只有 Apply 真正执行了效果（没被免疫/条件满足）才算技能发动，才会触发 FlashSkill
        return ab.effect.Apply(this, target);
    }

    // ========== 新技能运行时接口（战斗流程读取）==========

    // ♠4 闪避：本次受击是否扑空（每次独立掷骰）
    public bool RollEvade()
    {
        bool evaded = flagEvade && Random.value < evadeChance;
        if (evaded) FlashSkill();   // 闪避成功→青绿闪
        return evaded;
    }

    // ♠7 潜水首击无敌：还没消耗过就挡下这一击并消耗标记
    public bool ConsumeDiveInvincible()
    {
        if (flagDive && !diveInvincibleUsed)
        {
            diveInvincibleUsed = true;
            FlashSkill();   // 潜水无敌触发→青绿闪
            return true;
        }
        return false;
    }

    // ♠5 团居：扑击伤害 = 力量 × 同阵营在场数量；♠7 潜水时再 × 牙齿数
    public int GetStrikeDamage()
    {
        int dmg = CurrentPower;
        if (flagColony)
        {
            int count = 0;
            BoardManager.Instance.ForEachCard(IsPlayer, c => { if (!c.IsDead) count++; });//lambda表达式传入ccount计算人数
            dmg = CurrentPower * Mathf.Max(1, count);
        }
        if (flagDive && teeth > 0) dmg *= teeth;
        return dmg;
    }

    // ♠6 死亡翻滚：每轮随机扑 1~5 次；普通牌 1 次
    public int GetStrikeCount()
    {
        return flagDeathRoll ? Random.Range(1, 6) : 1;
    }

    // ♠7 记录谁直接造成过伤害（主动扑击走这里；毒/技能伤害以后若加，传 null 即可不记录）
    public void RecordDamager(Card c)
    {
        if (c == null || c == this) return;
        if (damagedBy == null) damagedBy = new HashSet<Card>();
        damagedBy.Add(c);
    }

    // ♠7 捡牙：+N 颗，集满 20 进入潜水（潜水后不再捡）
    public void AddTeeth(int n)
    {
        if (!flagDeepHunter || flagDive) return;
        teeth = Mathf.Min(TEETH_MAX, teeth + n);
        if (teeth >= TEETH_MAX)
        {
            flagDive = true;
            diveInvincibleUsed = false;
            FlashSkill();   // 进入潜水→青绿闪
            Debug.Log("[技能] " + CardName + " 集满20颗牙，进入潜水状态");
        }
    }

    // ♠7 回合开始掉牙：潜水态每回合 -2，归零退出潜水、可重新积攒
    public void TickTeeth()
    {
        if (!flagDeepHunter || !flagDive) return;
        teeth = Mathf.Max(0, teeth - TEETH_DECAY);
        if (teeth == 0)
        {
            flagDive = false;
            diveInvincibleUsed = false;
            Debug.Log("[技能] " + CardName + " 牙齿耗尽，退出潜水");
        }
    }

    // ========== 战斗 ==========

    // 单次伤害：把 damage 打到 target 身上，处理死亡与技能触发
    void DealDamage(Card target, int damage)
    {
        if (target == null || target.IsDead || damage <= 0) return;

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

        // ♠7 伤害来源记录（在死亡前，供捡牙判定）
        target.RecordDamager(this);

        target.CurrentPower -= damage;
        bool killed = false;
        if (target.CurrentPower <= 0)
        {
            target.CurrentPower = 0;
            target.FlashHit();
            target.Die();
            killed = true;
        }
        else
        {
            // 没死：后仰 + 刷新战力
            target.PlayHitReaction(hitDir);
            target.RefreshDisplay();
        }

        // 技能触发：命中 → 击杀；目标受击
        TriggerAbility(AbilityTrigger.OnHit, target);
        if (killed) TriggerAbility(AbilityTrigger.OnKill, target);
        target.TriggerAbility(AbilityTrigger.OnDamaged, this);
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
    // dealDamage=false = 扑空（闪避/潜水无敌）：照播冲撞动画但不结算伤害。
    public IEnumerator StrikeAndReturn(Card target, bool dealDamage = true)
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

        if (IsDead)
        {
            sortingGroup.sortingOrder = oldOrder;
            yield break;
        }

        if (dealDamage)
        {
            // ② 命中：扣血（团居/潜水的伤害倍率在 GetStrikeDamage 里算）
            int damage = GetStrikeDamage();
            // ♠5 团居 / ♠7 潜水：技能驱动的伤害加成，攻击命中时闪一下
            if (flagColony || (flagDive && teeth > 0)) FlashSkill();
            DealDamage(target, damage);
            Debug.Log("[战斗] " + CardName + " 打 " + target.CardName + " " + damage + " 点");
        }
        else
        {
            Debug.Log("[战斗] " + CardName + " 扑空（" + target.CardName + " 闪避或无敌）");
        }

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

        // 打脸类技能（打劫：额外筹码）
        TriggerAbility(AbilityTrigger.OnHit, null);

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

        TriggerAbility(AbilityTrigger.OnDeath, null);

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
