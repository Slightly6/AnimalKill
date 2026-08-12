using UnityEngine;
using TMPro;
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
    public int CurrentPower;// 当前战力
    public bool IsDead;
    public bool IsPlayer;
    public float flipDuration=0.3f;   // 翻面动画时长
    [System.NonSerialized] public bool IsFaceDown = true;   // 默认扣着（背面朝上），不在 Inspector 显示

    public string CardName { get { return Data.animalName; } }
    public int Power { get { return Data.GetPower(); } }

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
            transform.localScale = new Vector3(Mathf.Lerp(1, 0, t / flipDuration), scale.y, scale.z);
            yield return null;
        }

        // ② 切面
        SetFaceDown(!IsFaceDown);

        // ③ 展开
        t = 0;
        while (t < flipDuration)
        {
            t += Time.deltaTime;
            transform.localScale = new Vector3(Mathf.Lerp(0, 1, t / flipDuration), scale.y, scale.z);
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

    // 战力数值 → 点数文字  例: 13→K  8→8  3→3
    string PowerToRankString(int power)
    {
        if (power >= 14) return "A";
        if (power >= 13) return "K";
        if (power >= 12) return "Q";
        if (power >= 11) return "J";
        if (power <= 2) return "2";
        return power.ToString();
    }

    // ========== 战斗 ==========

    // 互殴：双方各受对方当前战力伤害
    public void Fight(Card other)
    {
        if (IsDead || other.IsDead) return;

        int myDamage = CurrentPower;
        int theirDamage = other.CurrentPower;

        other.CurrentPower -= myDamage;
        CurrentPower -= theirDamage;

        EventBus.Publish(new CardAttackedEvent
        {
            attacker = this,
            target = other,
            damage = myDamage
        });

        Debug.Log("[战斗] " + CardName + "(" + myDamage + ") ⇄ " +
                  other.CardName + "(" + theirDamage + ")");

        // 对方死了？
        if (other.CurrentPower <= 0)
        {
            other.CurrentPower = 0;
            other.Die();
        }
        else
        {
            other.RefreshDisplay();
        }

        // 自己死了？
        if (CurrentPower <= 0)
        {
            CurrentPower = 0;
            Die();
        }
        else
        {
            RefreshDisplay();
            StartCoroutine(HitFlash());
        }
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

    System.Collections.IEnumerator HitFlash()
    {
        // 暂时没底框可闪，先空着，后续加了 frame 再补
        yield break;
    }

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
