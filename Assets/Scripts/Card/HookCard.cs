using System.Collections;
using UnityEngine;

/// <summary>
/// 小丑实体：商店里待售的小丑挂卡，买后飞到钩子上，没买飞回牌堆消失。
/// </summary>
public class HookCard : MonoBehaviour, IShopFlyable
{
    public JokerSO Data { get; private set; }
    public MeshRenderer frontRenderer;

    [Header("悬浮反馈")]
    public float hoverLift = 0.4f;      // 上浮高度
    [SerializeField]
    private float hoverScale = 1.08f;    // 悬浮放大（待售小丑）
    [SerializeField]
    private float hookHoverScale = 1.08f; // 钩子上已购小丑的悬浮放大

    Vector3 baseScale;
    Vector3 homePos;     // 悬浮前的"家"位置（进入悬浮瞬间记录；钩子上的卡一直在摆，不能用 Start 时的固定值）
    Vector3 homeScale;
    bool hovering;
    bool flying;   // 购买飞向钩子途中，悬浮交互全禁（避免和飞行动画抢位置）
    Coroutine hoverRoutine;
    bool locked;   // 已购（挂在钩子上）：悬浮照常，只是不再飘骰子价格

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
        baseScale = transform.localScale;   // 购买瞬间复位 scale 用
    }

    // 悬浮：放大并保持"挂钩点不动"（卡自身局部顶部钉在原位，放大时绕挂钩往下长）
    Vector3 fixedHookPoint;   // 悬浮时锁定的挂钩点（卡局部顶部）世界坐标
    float localHalfHeight;    // 卡面网格局部半高（scale=1 下）
    void OnMouseEnter()
    {
        if (flying) return;
        if (!hovering)
        {
            homePos = transform.position;   // 记"家"：进入悬浮瞬间的位置/大小
            homeScale = transform.localScale;
            hovering = true;
            // 挂钩点 = 卡自身局部顶部（卡挂在钩子上时顶部贴着挂线）；记下它的世界坐标钉死
            MeshFilter mf = GetComponentInChildren<MeshFilter>();
            localHalfHeight = mf != null ? mf.sharedMesh.bounds.extents.y : 0f;
            fixedHookPoint = transform.TransformPoint(Vector3.up * localHalfHeight);
        }
        float scaleMul = locked ? hookHoverScale : hoverScale;
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(HoverAnim(homePos, homeScale * scaleMul));

        if (!locked && ShopManager.Instance != null && Data != null)
            ShopManager.Instance.ShowDiceCup(Data.price);   // 待售小丑才飘骰子价格；钩子上的不飘
    }
    void OnMouseOver()
    {
        if (Input.GetMouseButtonDown(1))
            PhoneDisplay.Instance.ShowJoker(Data);   // 小丑说明投到手机屏幕上（商店/战斗随时可看）
    }
    void OnMouseExit()
    {
        PhoneDisplay.Instance.SlideBack();   // 移开小丑卡，手机滑回起点
        if (ShopManager.Instance != null) ShopManager.Instance.HideDiceCup();   // 骰子缩回帽子里
        if (flying) return;
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        hoverRoutine = StartCoroutine(HoverAnim(homePos, homeScale));
        hovering = false;
    }
    // 相对动画：缩放从当前 Lerp 到目标；位置按当前缩放反推，使卡局部顶部始终钉在 fixedHookPoint
    IEnumerator HoverAnim(Vector3 toPos, Vector3 toScale)
    {
        Vector3 fromScale = transform.localScale;
        float t = 0f;
        const float dur = 0.15f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            Vector3 curScale = Vector3.Lerp(fromScale, toScale, p);
            transform.localScale = curScale;
            // 局部顶部到中心的世界偏移 = transform.up × (局部半高 × 当前缩放)
            // 中心位置 = 挂钩点 - 偏移，这样放大时挂钩点不动、卡绕它往下长
            Vector3 topOffsetWorld = transform.up * (localHalfHeight * curScale.y);
            transform.position = fixedHookPoint - topOffsetWorld;
            yield return null;
        }
    }
    // 购买时停掉悬浮协程，避免它和飞向钩子的动画抢位置；scale 复位
    void StopHover()
    {
        locked = true;
        flying = true;
        hovering = false;
        if (hoverRoutine != null) StopCoroutine(hoverRoutine);
        transform.localScale = baseScale;
        StartCoroutine(ClearFlyFlag());
    }
    // 飞行 0.4 秒 + 余量后恢复悬浮交互（已落定在钩子上）
    IEnumerator ClearFlyFlag()
    {
        yield return new WaitForSeconds(0.6f);
        flying = false;
    }

    void OnMouseDown()
    {
        if (GameProgress.InputLocked) return;
        if (GameProgress.currentNodeType != MapNodeType.Shop) return;
        if (Data == null) return;

        // 骰子不够：敌人开口说你付不起，卡留在原地（开挂=无限骰子，跳过判断）
        if (!GameProgress.cheatMode && GameProgress.dice < Data.price)
        {
            if (ShopManager.Instance != null)
                ShopManager.Instance.HintCantAfford(Data.price - GameProgress.dice);
            return;
        }

        StopHover();
        // 购买成立（栏没满）才扣钱并从商店列表移除；失败卡留在原地继续卖
        if (!ShopManager.Instance.BuyJoker(this)) return;

        if (!GameProgress.cheatMode)   // 开挂：不扣骰子
        {
            GameProgress.dice -= Data.price;
            if (TOPhat.Instance != null) TOPhat.Instance.RefreshCount();   // 帽子计数同步
            ShopManager.Instance.ShowDiceLine();   // 报数同步到新余额
        }
        Debug.Log("已购买小丑：" + Data.jokerName + "，花费骰子 " + Data.price);
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
