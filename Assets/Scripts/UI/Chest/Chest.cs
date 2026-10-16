using UnityEngine;
using System.Collections;

/// <summary>
/// 关卡结束的新宝箱：开盖 → 按 EnhanceManager 喂进来的效果，用 effect.bellPrefab 生成对应外观的铃铛进箱 →
/// 玩家点一张牌，卡获得该效果和图标，铃铛销毁。也能合上（Close）。
///
/// 不做任何随机！谁要开箱子必须先自己抽好效果再喂进来（EnhanceManager 就是唯一的随机源），
/// 避免"开箱冒出的铃铛效果"和"点牌要加的效果"对不上。
///
/// 效果资产（AbilityEffectSO）里拖好 bellPrefab（3D 铃铛）和 bellIcon（卡牌上的效果小图标）。
///
/// 场景搭建：箱子根物体挂本脚本；lid 拖 Blender 拆出来的「盖」那半（pivot 必须在铰链上）；
/// bellAnchor 拖箱子里一个空物体当生成点。
/// </summary>
public class Chest : MonoBehaviour
{
    [Header("盖子（拖 Blender 拆出来的「盖」那半，pivot 必须在铰链上）")]
    public Transform lid;
    public Vector3 openEuler = new Vector3(110f, 0f, 0f);   // 打开时盖子相对合上状态转多少度（一般绕 X）
    public float openDuration = 0.8f;
    public float closeDuration = 0.6f;

    [Header("铃铛生成点（箱子里面的空物体）")]
    public Transform bellAnchor;

    [Header("从天上掉下来（落稳了再开盖）")]
    public float fallHeight = 3f;        // 从多高掉下来
    public float fallDuration = 0.5f;    // 下落时长
    public float bounceHeight = 0.15f;   // 落地小弹跳高度，0 = 不弹
    public float bounceDuration = 0.18f; // 单次弹跳时长

    Quaternion closedRot;              // 合上时的盖子本地旋转（Awake 记录）
    AbilityEffectSO currentEffect;     // 当前箱里铃铛对应的效果（由 EnhanceManager 喂进来）
    GameObject currentBell;            // 箱子里当前这颗铃铛
    Coroutine lidRoutine;

    public bool IsOpen { get; private set; }
    public AbilityEffectSO CurrentEffect => currentEffect;

    void Awake()
    {
        if (lid != null) closedRot = lid.localRotation;
    }

    /// <summary>
    /// 开箱：效果必须由外部先抽好传进来，箱子不做随机。
    /// effect.bellPrefab 拖到效果资产里，箱子按它摆铃铛。
    /// </summary>
    public void Open(AbilityEffectSO effect)
    {
        if (IsOpen) return;
        if (effect == null) { Debug.LogWarning("[宝箱] Open 收到空效果，不生成铃铛"); return; }
        currentEffect = effect;
        IsOpen = true;
        PlayLid(closedRot * Quaternion.Euler(openEuler), openDuration, SpawnBell);
    }

    /// <summary>
    /// 从天上掉下来开箱：铃铛先作为 bellAnchor 的子物体生成（合着盖一起掉），
    /// 落稳后自动开盖露出铃铛。效果同样由外部先抽好喂进来。
    /// </summary>
    public void DropIn(AbilityEffectSO effect, System.Action onOpened = null)
    {
        if (effect == null) { Debug.LogWarning("[宝箱] DropIn 收到空效果，不生成铃铛"); return; }
        currentEffect = effect;
        IsOpen = false;
        SpawnBell();                                     // 提前生成，挂在 bellAnchor 下跟着箱子一起掉
        transform.position += Vector3.up * fallHeight;   // 抬到天上
        StartCoroutine(DropRoutine(onOpened));
    }

    // 掉落 → 小弹跳 → 开盖 → 通知外部（切相机等）
    IEnumerator DropRoutine(System.Action onOpened)
    {
        Vector3 start = transform.position;
        Vector3 target = start - Vector3.up * fallHeight;

        // 下落：二次缓动 = 越掉越快
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, fallDuration);
            float p = Mathf.Clamp01(t);
            transform.position = Vector3.Lerp(start, target, p * p);
            yield return null;
        }
        transform.position = target;

        // 落地小弹跳：弹起一点再落下
        if (bounceHeight > 0f)
        {
            Vector3 apex = target + Vector3.up * bounceHeight;
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, bounceDuration);
                float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 2f);   // 前快后慢
                transform.position = Vector3.Lerp(target, apex, p);
                yield return null;
            }
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, bounceDuration);
                float p = Mathf.Clamp01(t);
                transform.position = Vector3.Lerp(apex, target, p * p);
                yield return null;
            }
        }
        transform.position = target;

        // 落稳了开盖（铃铛已经在了，不用再生成），开完回调通知外部
        IsOpen = true;
        PlayLid(closedRot * Quaternion.Euler(openEuler), openDuration, onOpened);
    }

    // 合上：只盖回去，不动铃铛（铃铛由点牌 ConsumeBell 负责销毁）
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        PlayLid(closedRot, closeDuration, null);
    }

    // 按 currentEffect 生成对应外观的铃铛（currentEffect 已由 Open 外部传入）
    void SpawnBell()
    {
        ConsumeBell();

        if (currentEffect == null || currentEffect.bellPrefab == null)
        {
            Debug.LogWarning("[宝箱] 效果 " + (currentEffect != null ? currentEffect.name : "null") + " 没拖 bellPrefab");
            return;
        }
        if (bellAnchor == null)
        {
            Debug.LogWarning("[宝箱] 没拖 bellAnchor（铃铛生成点）");
            return;
        }

        currentBell = Instantiate(currentEffect.bellPrefab, bellAnchor);   // 挂在生成点下，箱子动铃铛跟着动
        currentBell.transform.localPosition = Vector3.zero;
        currentBell.transform.localRotation = Quaternion.identity;
        currentBell.SetActive(true);   // 预制体根物体可能是未激活状态
        // 给这颗实例挂上效果信息组件（右键查说明用；预制体里不用提前挂）
        BellReward reward = currentBell.GetComponent<BellReward>();
        if (reward == null) reward = currentBell.AddComponent<BellReward>();
        reward.effect = currentEffect;
        EnsureCollider(currentBell);
    }

    // 销毁箱里的铃铛（点完牌后调）
    public void ConsumeBell()
    {
        if (currentBell != null) Destroy(currentBell);
        currentBell = null;
    }

    /// <summary>
    /// 飞回去收场（点完牌后调）：从当前位置飞回天上（原路，回到掉下来时的高度），飞完自毁。
    /// </summary>
    public void FlyAway(float duration)
    {
        StartCoroutine(FlyRoutine(duration));
    }

    IEnumerator FlyRoutine(float duration)
    {
        Vector3 start = transform.position;
        Vector3 target = start + Vector3.up * fallHeight;   // 原路飞回天上
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duration);
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.position = Vector3.Lerp(start, target, p);
            yield return null;
        }
        Destroy(gameObject);
    }

    // 盖子转到目标角度，转完执行回调（可为空）
    void PlayLid(Quaternion target, float duration, System.Action onDone)
    {
        if (lid == null) { if (onDone != null) onDone(); return; }
        if (lidRoutine != null) StopCoroutine(lidRoutine);
        lidRoutine = StartCoroutine(RotateLid(target, duration, onDone));
    }

    IEnumerator RotateLid(Quaternion target, float duration, System.Action onDone)
    {
        Quaternion start = lid.localRotation;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duration);
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 2f);   // 前快后慢
            lid.localRotation = Quaternion.Slerp(start, target, ease);
            yield return null;
        }
        lid.localRotation = target;
        lidRoutine = null;
        if (onDone != null) onDone();
    }

    // 确保铃哨被鼠标点得到（右键查说明用）
    static void EnsureCollider(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponentInChildren<Collider>() != null) return;
        go.AddComponent<BoxCollider>();
    }
}

/// <summary>
/// 铃铛实例上的效果信息组件：生成宝箱铃铛时 Chest 动态 AddComponent 并赋值 effect。
/// 右键点铃铛 = 效果说明投到手机屏幕上。
/// 预制体里不用提前挂，Chest.SpawnBell 会自动补。
/// </summary>
public class BellReward : MonoBehaviour
{
    public AbilityEffectSO effect;   // 这颗铃铛给的卡牌效果

    void OnMouseOver()
    {
        if (Input.GetMouseButtonDown(1) && effect != null && PhoneDisplay.Instance != null)
            PhoneDisplay.Instance.ShowEffect(effect);
    }

    void OnMouseExit()
    {
        if (PhoneDisplay.Instance != null) PhoneDisplay.Instance.SlideBack();
    }
}
