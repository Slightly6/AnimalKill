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

    Vector3 basePos;
    Vector3 baseScale;
    Coroutine hoverRoutine;
    bool locked;   // 购买飞走后置真，悬浮逻辑全部停用

    void Start()
    {
        targetPosition = DeckPile.Instance.transform;
        basePos = transform.position;   // 悬浮结束回落的位置
        baseScale = transform.localScale;
    }

    // 商店卡逻辑只在商店节点生效（手牌和商店卡共用 prefab，战斗中手牌上也挂着本脚本）
    bool InShop()
    {
        return GameProgress.currentNodeType == MapNodeType.Shop;
    }

    // 悬浮：上浮 + 微放大；移开：回落复位
    void OnMouseEnter()
    {
        if (locked || !InShop() || GameProgress.InputLocked) return;
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(HoverAnim(basePos + Vector3.up * hoverLift, hoverScale));
    }
    void OnMouseExit()
    {
        if (locked || !InShop()) return;
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(HoverAnim(basePos, 1f));
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
            yield return null;
        }
    }

    // 购买时停掉悬浮协程，避免它和飞走动画抢位置；scale 复位（FlyStraight 不管缩放）
    void StopHover()
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

        StopHover();
        DeckManager.Instance.AddToDeck(card.Data);
        Debug.Log("已购买卡牌：" + card.Data.name);

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
