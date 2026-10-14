using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ShopCard : MonoBehaviour, IShopFlyable
{
    public Transform targetPosition;

    [Header("悬浮反馈")]
    public float hoverLift = 0.4f;      // 上浮高度
    public float hoverScale = 1.08f;    // 悬浮放大

    [Header("购买价格")]
    public float priceLabelHeight = 1.2f;   // 价格标签悬浮在卡上方多高

    Vector3 basePos;
    Vector3 baseScale;
    Coroutine hoverRoutine;
    Coroutine shakeRoutine;
    bool locked;   // 购买飞走后置真，悬浮逻辑全部停用

    GameObject priceLabel;                    // 悬浮价格标签（懒创建）
    TMPro.TextMeshPro priceTmp;

    void Start()
    {
        targetPosition = DeckPile.Instance.transform;
        basePos = transform.position;   // 悬浮结束回落的位置
        baseScale = transform.localScale;
    }

    // 商店卡逻辑只在商店节点生效（手牌和商店卡共用 prefab，战斗中手牌上也挂着本脚本）
    // 例外：强化三选一激活时（EnhanceManager.Choosing）也放行悬浮，但价格标签仍只在真商店显示
    bool InShop()
    {
        return GameProgress.currentNodeType == MapNodeType.Shop || EnhanceManager.Choosing;
    }

    // 外部同步悬浮的"家"位置（强化卡是飞到桌位的，Start 时记的起飞点不对）
    public void SyncBasePos()
    {
        basePos = transform.position;
    }

    // 悬浮：上浮 + 微放大 + 显示价格；移开：回落复位 + 隐藏价格
    void OnMouseEnter()
    {
        if (locked || !InShop() || GameProgress.InputLocked) return;
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(HoverAnim(basePos + Vector3.up * hoverLift, hoverScale));
        if (GameProgress.currentNodeType == MapNodeType.Shop) ShowPriceLabel();   // 价格只在真商店弹，强化界面不弹
    }
    void OnMouseExit()
    {
        if (locked || !InShop()) return;
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(HoverAnim(basePos, 1f));
        HidePriceLabel();
    }
    IEnumerator HoverAnim(Vector3 toPos, float toScale)
    {
        Vector3 fromPos = transform.position;
        Vector3 fromScale = transform.localScale;
        float t = 0f;
        const float dur = 0.15f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.position = Vector3.Lerp(fromPos, toPos, p);
            transform.localScale = Vector3.Lerp(fromScale, baseScale * toScale, p);
            // 价格标签一直贴在卡上方
            if (priceLabel != null && priceLabel.activeSelf)
                priceLabel.transform.position = transform.position + Vector3.up * priceLabelHeight;
            yield return null;
        }
    }

    // 显示这张卡的售价（世界空间 TMP，面向相机，懒创建）
    void ShowPriceLabel()
    {
        Card card = GetComponent<Card>();
        if (card == null || card.Data == null) return;

        if (priceLabel == null)
        {
            priceLabel = new GameObject("PriceLabel");
            priceTmp = priceLabel.AddComponent<TMPro.TextMeshPro>();
            priceTmp.fontSize = 5f;
            priceTmp.fontStyle = TMPro.FontStyles.Bold;
            priceTmp.alignment = TMPro.TextAlignmentOptions.Center;
            priceTmp.sortingOrder = 50;
            priceTmp.color = new Color(1f, 0.85f, 0.3f);   // 金色，和骰子/结算数字呼应
            priceTmp.transform.localScale = Vector3.one*2;
        
        }
        if (Camera.main != null)
            priceLabel.transform.rotation = Camera.main.transform.rotation;
        priceTmp.text = card.Data.price.ToString();
        priceLabel.transform.position = transform.position + Vector3.up * priceLabelHeight;
        priceLabel.SetActive(true);
    }
    void HidePriceLabel()
    {
        if (priceLabel != null) priceLabel.SetActive(false);
    }

    // 骰子不够：卡左右晃一下表示拒绝
    IEnumerator ShakeAnim()
    {
        Vector3 baseP = transform.position;
        float t = 0f;
        const float dur = 0.35f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float p = Mathf.Clamp01(t);
            // sin 摆 3 个来回，幅度逐渐衰减
            float off = Mathf.Sin(p * Mathf.PI * 6f) * (1f - p) * 0.15f;
            transform.position = baseP + Vector3.right * off;
            yield return null;
        }
        transform.position = baseP;
    }

    // 购买/飞走时停掉悬浮协程，避免它和飞走动画抢位置；scale 复位（FlyStraight 不管缩放）
    public void StopHover()
    {
        locked = true;
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        transform.localScale = baseScale;
    }

    public void OnClick()
    {
        if (!InShop()) return;   // 非商店节点不触发购买（配合 CardDifference 的分流，双保险）

        Card card = GetComponent<Card>();
        if (card == null || card.Data == null) return;

        // 骰子不够：左右晃一下拒绝，不购买
        if (GameProgress.dice < card.Data.price)
        {
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(ShakeAnim());
            return;
        }

        GameProgress.dice -= card.Data.price;   // 扣骰子
        if (TOPhat.Instance != null) TOPhat.Instance.RefreshCount();   // 帽子计数同步

        HidePriceLabel();
        StopHover();
        DeckManager.Instance.AddToDeck(card.Data);
        Debug.Log("已购买卡牌：" + card.Data.name + "，花费骰子 " + card.Data.price);

        StartCoroutine(FlyStraight(card.transform, targetPosition.position, 0.5f));
        ShopManager.Instance.shopCards.Remove(card.transform);
        ShopManager.Instance.CardsPool.Remove(card.Data);

    }
    public IEnumerator FlyStraight(Transform card, Vector3 targetPos, float duration = 0.2f)
    {
        Vector3 startPos = card.position;
        Quaternion startRot = card.rotation;
        Quaternion endRot = Quaternion.Euler(-90f, 90f, -90f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float p = Mathf.Clamp01(t);

            // 位置：直线插值
            card.position = Vector3.Lerp(startPos, targetPos, p);

            // 旋转：翻 180°
            card.rotation = Quaternion.Slerp(startRot, endRot, p);

            yield return null;
        }

        card.position = targetPos;
        card.rotation = endRot;
        Destroy(card.gameObject);
    }
}
