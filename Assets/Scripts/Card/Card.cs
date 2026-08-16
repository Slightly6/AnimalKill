using UnityEngine;
using TMPro;
using System.Collections;
/// <summary>
/// 一张扑克牌动物卡。战力 = 攻 = 血，一个数值。
/// </summary>
public class Card : MonoBehaviour
{
    [Header("渲染（拖入）")]
    public SpriteRenderer backRenderer;      // 背面
    public TextMeshPro[] rankTexts;          // 正面花色点数文字

    // ---- 运行时状态 ----
    public CardDataSO Data;
    public int CurrentPower;// 当前力量
    public bool IsDead;
    public bool IsPlayer;
    public float flipDuration=0.3f;   // 翻面动画时长
    public float ScaleX=0.8f;       
    [System.NonSerialized] public bool IsFaceDown = true;   // 默认扣着（背面朝上），不在 Inspector 显示

    public string CardName { get { return Data.animalName; } }

    public void Init(CardDataSO data, bool isPlayer)
    {
        Data = data;
        IsPlayer = isPlayer;
        CurrentPower = Data.GetPower();
        IsDead = false;
        RefreshDisplay();
        SetFaceDown(IsFaceDown);
    }

    // 瞬间切换正反面（FlipAnim 中间那一步用）
    public void SetFaceDown(bool faceDown)
    {
        bool showFront = !faceDown;

        for (int i = 0; i < rankTexts.Length; i++)
            if (rankTexts[i] != null) rankTexts[i].gameObject.SetActive(showFront);
        if (backRenderer != null) backRenderer.gameObject.SetActive(faceDown);
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

        if (target.CurrentPower <= 0)
        {
            target.CurrentPower = 0;
            target.Die();
        }
        else
        {
            target.RefreshDisplay();
        }
    }

    // 通用攻击动作：冲过去打一下，再回原位（this 是出手方，target 是被打方）
    public IEnumerator StrikeAndReturn(Card target)
    {
        Vector3 homePos = transform.position;
        Quaternion homeRot = transform.rotation;
        Vector3 dir = target.transform.position - homePos;
        dir.y = 0;   // 只在桌面 XZ 平面冲锋
        dir.Normalize();
        Vector3 slamPos = target.transform.position - dir * 0.5f;

        // 冲锋时往前进方向侧倾一点，像真的扑过去
        Vector3 tiltAxis = Vector3.Cross(dir, Vector3.up);
        Quaternion lungeRot = Quaternion.AngleAxis(15f, tiltAxis) * homeRot;

        // ① 边旋转边冲过去（快）
        yield return CardAnimator.MoveAndRotate(transform, slamPos, lungeRot, 0.12f);

        // ② 命中：扣血
        int damage = CurrentPower;
        DealDamage(target, damage);
        Debug.Log("[战斗] " + CardName + " 打 " + target.CardName + " " + damage + " 点");

        // ③ 边旋转边回原位（慢）
        yield return CardAnimator.MoveAndRotate(transform, homePos, homeRot, 0.2f);
    }

    // 打脸动画：对面没卡，冲出去打对方脸，然后回来
    public IEnumerator FaceAnim()
    {
        Vector3 homePos = transform.position;
        Quaternion homeRot = transform.rotation;
        // 玩家朝 -Z（打向敌人远端），敌方朝 +Z（打向玩家）
        Vector3 dir = IsPlayer ? -Vector3.forward : Vector3.forward;
        Vector3 lungePos = homePos + dir * 1.0f;

        Vector3 tiltAxis = Vector3.Cross(dir, Vector3.up);
        Quaternion lungeRot = Quaternion.AngleAxis(15f, tiltAxis) * homeRot;

        // ① 边旋转边冲出去（快）
        yield return CardAnimator.MoveAndRotate(transform, lungePos, lungeRot, 0.12f);

        // ② 命中：打脸
        int damage = CurrentPower;
        if (IsPlayer)
        {
            GameManager.Instance.AddTrophy(Data);          // 收牌凑德州
            GameManager.Instance.EnemyLoseChips(damage);   // 扣敌人筹码
            GameManager.Instance.AddChips(damage);         // 玩家加 damage 筹码（打脸赢的）
        }
        else
        {
            GameManager.Instance.LoseChips(damage);        // 敌人扣玩家筹码
            GameManager.Instance.EnemyAddChips(damage);    // 敌人自己加上
        }
        Debug.Log("[战斗] " + CardName + " 打脸 " + damage + " 点");

        // ③ 边旋转边回原位（慢）
        yield return CardAnimator.MoveAndRotate(transform, homePos, homeRot, 0.2f);
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
