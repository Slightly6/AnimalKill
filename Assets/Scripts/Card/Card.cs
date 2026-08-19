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
    public SpriteRenderer backRenderer;      // 背面
    public TextMeshPro[] rankTexts;          // 正面花色点数文字
    public SpriteRenderer frontRenderer;     // 正面动物图
    public SpriteRenderer skillIconRenderer; // 正面技能图标（小）
    public float frontArtScale = 0.12f;      // 正面动物图大小
    public Sprite stackedSkillIcon;          // 叠加得到的技能图标（献祭来的，没叠是 null）
    // ---- 运行时状态 ----
    public CardDataSO Data;
    public int CurrentPower;// 当前力量
    public bool IsDead;
    public bool IsPlayer;
    public bool IsPlayed;   // 已经打出去的牌（不能再拖）
    public float flipDuration=0.3f;   // 翻面动画时长
    public float ScaleX=0.8f;
    public float arcHeight=1.2f;          // 半圆弧猛冲的高度（跳多高）
    public float tiltAngle=30f;           // 猛冲时前倾的角度
    public float rushDuration=0.15f;      // 猛冲出去的时间（快）
    public float returnDuration=0.2f;     // 回原位的时间（慢）
    public float thrustPivotOffset=0.9f;  // 后仰支点离中心多远 = 半张牌长（牌高2.6×缩放0.7÷2）
    public float recoilAngle=14f;         // 被打后仰的角度
    [System.NonSerialized] public bool IsFaceDown = true;   // 默认扣着（背面朝上），不在 Inspector 显示
    [System.NonSerialized] public SortingGroup sortingGroup;   // 缓存引用，避免每帧 GetComponent
    [System.NonSerialized] public bool IsSelected;   // 这张牌被点选（准备出牌）

    public string CardName { get { return Data.animalName; } }

    void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
    }

    public void Init(CardDataSO data, bool isPlayer, int bonusPower = 0)
    {
        Data = data;
        IsPlayer = isPlayer;
        CurrentPower = Data.GetPower() + bonusPower;   // 基础战力 + 运行时加成（觉醒/额外）
        IsDead = false;
        RefreshDisplay();

        // 换正面动物图 + 按 frontArtScale 缩放
        if (frontRenderer != null)
        {
            frontRenderer.sprite = Data.artwork;
            frontRenderer.transform.localScale = new Vector3(frontArtScale, frontArtScale, 1f);
        }

        // 技能图标：有叠加技能显示叠加的，否则显示卡牌自带技能图标
        if (skillIconRenderer != null)
        {
            Sprite icon = stackedSkillIcon != null ? stackedSkillIcon : Data.abilityIcon;
            skillIconRenderer.sprite = icon;
        }

        SetFaceDown(IsFaceDown);
    }

    // 瞬间切换正反面（FlipAnim 中间那一步用）
    public void SetFaceDown(bool faceDown)
    {
        bool showFront = !faceDown;

        for (int i = 0; i < rankTexts.Length; i++)
            if (rankTexts[i] != null) rankTexts[i].gameObject.SetActive(showFront);
        if (backRenderer != null) backRenderer.gameObject.SetActive(faceDown);
        if (frontRenderer != null) frontRenderer.gameObject.SetActive(showFront);
        if (skillIconRenderer != null) skillIconRenderer.gameObject.SetActive(showFront && skillIconRenderer.sprite != null);
    }

    // 把某个技能图标叠到这张牌上（奖励关献祭后调用）
    public void ApplyStackedSkill(Sprite icon)
    {
        stackedSkillIcon = icon;
        if (skillIconRenderer != null)
        {
            skillIconRenderer.sprite = icon;
            skillIconRenderer.gameObject.SetActive(icon != null);
        }
    }

    // 翻面动画：压扁 → 切面 → 展开
    public System.Collections.IEnumerator FlipAnim()
    {
        Vector3 scale = transform.localScale;

        // ① 压扁
        float t = 0;
        while (t < flipDuration)
        {
            t += Time.deltaTime;
            transform.localScale = new Vector3(Mathf.Lerp(ScaleX, 0, t / flipDuration), scale.y, scale.z);
            yield return null;
        }

        // ② 切面
        SetFaceDown(!IsFaceDown);

        // ③ 展开
        t = 0;
        while (t < flipDuration)
        {
            t += Time.deltaTime;
            transform.localScale = new Vector3(Mathf.Lerp(0, ScaleX, t / flipDuration), scale.y, scale.z);
            yield return null;
        }
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
            // 打死：死亡动画接管
            target.CurrentPower = 0;
            target.Die();
        }
        else
        {
            // 没死：后仰 + 刷新战力
            target.PlayHitReaction(hitDir);
            target.RefreshDisplay();
        }
    }

    // 被打的反馈：后仰一下再回正（攻击方调用，没打死时）
    public void PlayHitReaction(Vector3 hitDir)
    {
        StartCoroutine(HitReactionRoutine(hitDir));
    }

    IEnumerator HitReactionRoutine(Vector3 hitDir)
    {
        Vector3 homePos = transform.position;
        Quaternion homeRot = transform.rotation;

        // ① 后仰：朝被打方向相反侧倾一下（朝向攻击者那一侧翘起）
        Vector3 pivot = homePos + hitDir * thrustPivotOffset;   // 背对攻击者那一侧当支点
        Vector3 axis = Vector3.Cross(Vector3.up, hitDir);
        yield return CardAnimator.ThrustOut(transform, pivot, axis, recoilAngle, 0.07f);

        // ② 回正
        yield return CardAnimator.MoveAndRotate(transform, homePos, homeRot, 0.14f);
    }

    // 通用攻击动作：前倾 + 半圆弧猛冲过去，再拉回原位。
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

        // ① 前倾 + 半圆弧猛冲（快），落在目标身上
        yield return CardAnimator.ArcWithTilt(transform, target.transform.position, arcHeight, tiltAngle, dir, rushDuration);

        // ② 命中：扣血
        int damage = CurrentPower;
        DealDamage(target, damage);
        Debug.Log("[战斗] " + CardName + " 打 " + target.CardName + " " + damage + " 点");

        // ③ 拉回原位
        yield return CardAnimator.MoveAndRotate(transform, homePos, homeRot, returnDuration);

        sortingGroup.sortingOrder = oldOrder;
    }

    // 打脸动画：对面没卡，前倾 + 半圆弧猛冲打向对方脸，再拉回原位
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

        // ① 前倾 + 半圆弧猛冲（快）
        yield return CardAnimator.ArcWithTilt(transform, reachPos, arcHeight, tiltAngle, dir, rushDuration);

        // ② 命中：打脸
        int damage = CurrentPower;
        if (IsPlayer)
        {
            GameManager.Instance.AddTrophy(Data);          // 收牌凑德州
            GameManager.Instance.TransferChips(damage, true);   // 敌人筹码转给我（打脸赢的）
        }
        else
        {
            GameManager.Instance.TransferChips(damage, false);  // 我的筹码转给敌人（被打脸输的）
        }
        Debug.Log("[战斗] " + CardName + " 打脸 " + damage + " 点");

        // ③ 拉回原位
        yield return CardAnimator.MoveAndRotate(transform, homePos, homeRot, returnDuration);

        sortingGroup.sortingOrder = oldOrder;
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

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
        float t = 0;
        Vector3 scale = transform.localScale;
        Quaternion rot = transform.localRotation;

        while (t < flipDuration)
        {
            t += Time.deltaTime;
            float p = t / flipDuration;
            transform.localScale = Vector3.Lerp(scale, Vector3.zero, p);
            transform.localRotation = Quaternion.Lerp(rot, Quaternion.Euler(0, 0, 90), p);
            yield return null;
        }

        Destroy(gameObject);
    }
}
