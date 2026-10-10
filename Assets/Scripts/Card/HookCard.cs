using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 小丑实体：商店里待售的小丑挂卡，买后飞到钩子上，没买飞回牌堆消失。
/// </summary>
public class HookCard : MonoBehaviour, IShopFlyable
{
    public JokerSO Data { get; private set; }
    public MeshRenderer frontRenderer;

    [Header("悬浮反馈")]
    public float hoverLift = 0.4f;      // 上浮高度
    public float hoverScale = 1.08f;    // 悬浮放大
     [Header("悬浮提示")]
    public GameObject tooltip; 
    Vector3 basePos;
    Vector3 baseScale;
    Coroutine hoverRoutine;
    bool locked;   // 购买飞走后置真，悬浮逻辑全部停用

    public void Init(JokerSO so)
    {
        Data = so;
        if (Data != null && Data.faceTexture != null && frontRenderer != null)
        {
            MaterialPropertyBlock faceBlock = new MaterialPropertyBlock();
            frontRenderer.GetPropertyBlock(faceBlock);
            faceBlock.SetTexture("_MainTex", Data.faceTexture);
            frontRenderer.SetPropertyBlock(faceBlock);
        }
    }

    void Start()
    {
        basePos = transform.position;   // 悬浮结束回落的位置
        baseScale = transform.localScale;
    }

    // 悬浮：上浮 + 微放大；移开：回落复位
    void OnMouseEnter()
    {
        if (locked || GameProgress.InputLocked) return;
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(HoverAnim(basePos + Vector3.up * hoverLift, hoverScale));
    }
    void OnMouseOver()
    {
        if (Input.GetMouseButtonDown(1))
        {
            if (Data == null || tooltip == null) return;
            string desc = string.IsNullOrEmpty(Data.description) ? "" : "\n<size=80%>" + Data.description;
            tooltip.GetComponentInChildren<TextMeshPro>().text = "<b>" + Data.jokerName + "</b>" + desc;
            tooltip.SetActive(true);
        }
    }
    void OnMouseExit()
    {
        if (tooltip != null) tooltip.SetActive(false);
        if (locked) return;
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
    // 购买时停掉悬浮协程，避免它和飞向钩子的动画抢位置；scale 复位
    void StopHover()
    {
        locked = true;
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        transform.localScale = baseScale;
    }

    void OnMouseDown()
    {
        if (GameProgress.InputLocked) return;
        if (GameProgress.currentNodeType != MapNodeType.Shop) return;
        if (Data == null) return;

        StopHover();
        // 购买成立（栏没满）才从商店列表移除；失败卡留在原地继续卖
        if (ShopManager.Instance.BuyJoker(this))
            ShopManager.Instance.jokerPool.Remove(Data);
    }

    // 飞回牌堆（没买时 MapManager 调）
    public IEnumerator FlyStraight(Transform card, Vector3 targetPos, float duration = 0.5f)
    {
        Vector3 startPos = card.position;
        Quaternion startRot = card.rotation;
        Quaternion endRot = Quaternion.Euler(-90f, 90f, -90f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float p = Mathf.Clamp01(t);
            card.position = Vector3.Lerp(startPos, targetPos, p);
            card.rotation = Quaternion.Slerp(startRot, endRot, p);
            yield return null;
        }

        card.position = targetPos;
        card.rotation = endRot;
        Destroy(card.gameObject);
    }
}
