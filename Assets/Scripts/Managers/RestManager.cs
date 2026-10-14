using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 休息节点管理：左右各放一个可点击选项，点完回地图。
/// </summary>
public class RestManager : Singleton<RestManager>
{
    [Header("预制体（Inspector 里拖）")]
    public GameObject chipPrefab;      // 左侧：筹码堆
    public GameObject handPrefab;      // 右侧：扑克手牌

    [Header("生成位置（相对桌面中心）")]
    public Vector3 chipPos = new Vector3(-3f, 2f, -1f);
    public Vector3 handPos = new Vector3( 3f, 2f, -1f);
    public Vector3 restRotation = new Vector3(0f, 0f, 0f);

    [Header("掉落动画")]
    public float dropHeight = 2f;     // 物品从多高处掉下来
    public float dropDuration = 0.4f; // 掉落时长

    [Header("骰子投掷参数")]
    public float throwUpForce = 5f;      // 骰子被扔上去的上升力度
    public float throwTorque = 5f;       // 骰子旋转力度
    public float throwDuration = 0.3f;   // 扔上去的动画时长
    public float landWaitTime = 1.5f;   // 等骰子落地的时间

    [Header("骰子结果展示")]
    public GameObject resultTextPrefab;  // 飘字预制体（拖一个 TextMeshPro 世界空间 Text）

    // 骰子规则：掷到 N（1~9）→ 对应牌型伤害永久+1；掷到 10 → 全部牌型+1

    [Header("胳膊")]
    public Arm arm;

    // 当前场景里的实例（用于清理和点击判断）
    [System.NonSerialized] public GameObject chipInstance;
    [System.NonSerialized] public GameObject handInstance;

    bool consumed = false;   // 点过一个后锁死，防止连点

    /// <summary>
    /// 胳膊伸出 + 两个物品从上方掉落到手心位置
    /// </summary>
    public void SpawnRestOptions()
    {
        consumed = false;
        Despawn();   // 先清旧的

        if (arm == null) arm = FindObjectOfType<Arm>();

        // 胳膊伸出去接（目标位 = 物品落点）
        if (arm != null)
        {
            // 临时把 arm 的 target 设到 chipPos/handPos
            // 如果 arm 上拖了 target1/target2 就用拖好的；没拖就用 chipPos/handPos
            // 这里不直接改 arm 的字段，而是让 Arm 自己的 target 控制
            arm.Reach();
        }

        // 生成物品在目标位正上方，掉落到手心
        if (chipPrefab != null)
        {
            chipInstance = Instantiate(chipPrefab, chipPos + Vector3.up * dropHeight, Quaternion.Euler(restRotation));
            EnsureCollider(chipInstance);
            StartCoroutine(DropTo(chipInstance.transform, chipPos));
        }
        if (handPrefab != null)
        {
            handInstance = Instantiate(handPrefab, handPos + Vector3.up * dropHeight, Quaternion.Euler(restRotation));
            EnsureCollider(handInstance);
            StartCoroutine(DropTo(handInstance.transform, handPos));
        }
    }

    // 物品从上方 ease-in 掉落到目标位（模拟重力加速）
    IEnumerator DropTo(Transform item, Vector3 targetPos)
    {
        Vector3 startPos = item.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dropDuration;
            float p = Mathf.Clamp01(t);
            float eased = p * p;   // ease-in：越掉越快
            item.position = Vector3.Lerp(startPos, targetPos, eased);
            yield return null;
        }
        item.position = targetPos;
    }

    /// <summary>
    /// 清理场景中的休息选项 + 胳膊缩回
    /// </summary>
    public void Despawn()
    {
        if (arm == null) arm = FindObjectOfType<Arm>();
        if (arm != null) arm.ReachBack();

        if (chipInstance != null) { Destroy(chipInstance); chipInstance = null; }
        if (handInstance != null) { Destroy(handInstance); handInstance = null; }
    }

    void Update()
    {
        if (consumed) return;
        if (GameProgress.currentNodeType != MapNodeType.Rest) return;
        if (GameProgress.InputLocked) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        Transform clicked = hit.transform;

        // 点的是筹码？
        if (chipInstance != null && IsChildOrSelf(clicked, chipInstance.transform))
        {
            consumed = true;
            if (handInstance != null) { Destroy(handInstance); handInstance = null; }   // 没选的直接销毁
            GameManager.Instance.RestHeal();                 // 回 30% 筹码 + 30% 边池
            
            MapManager.Instance.FinishNonBattleNode();       // 回地图
            return;
        }

        // 点的是骰子？
        if (handInstance != null && IsChildOrSelf(clicked, handInstance.transform))
        {
            consumed = true;
            if (chipInstance != null) { Destroy(chipInstance); chipInstance = null; }   // 没选的直接销毁
            StartCoroutine(ThrowDiceRoutine());
            return;
        }
    }

    // 确保物体（含子物体）有 Collider，否则 Raycast 打不到
    static void EnsureCollider(GameObject go)
    {
        if (go == null) return;
        if (go.GetComponentInChildren<Collider>() != null) return;
        go.AddComponent<BoxCollider>();
    }

    static bool IsChildOrSelf(Transform child, Transform parent)
    {
        if (child == parent) return true;
        return child.IsChildOf(parent);
    }

    /// <summary>
    /// 骰子投掷流程：
    /// 1. 手收回（像掷骰子甩手）
    /// 2. 骰子 ease-out 向上飞 + 翻滚
    /// 3. 骰子 isKinematic=false 自由落体
    /// 4. 等落地
    /// 5. 读点数 → 牌型增加（TODO: 自己写）
    /// </summary>
    IEnumerator ThrowDiceRoutine()
    {
        if (handInstance == null) yield break;
        Transform dice = handInstance.transform;

        // 确保 Rigidbody
        Rigidbody rb = dice.GetComponent<Rigidbody>();
        if (rb == null) rb = dice.gameObject.AddComponent<Rigidbody>();

        // ===== 阶段1：手收回 + 骰子被扔上去（ease-out 减速上升）=====
        if (arm != null) arm.ReachBack();

        Vector3 startPos = dice.position;
        Vector3 peakPos = startPos + Vector3.up * throwUpForce;  // 扔上去的最高点
        Vector3 randomSpin = Random.insideUnitSphere * 360f * throwTorque;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / throwDuration;
            float p = Mathf.Clamp01(t);
            float eased = 1f - (1f - p) * (1f - p);   // ease-out：开始快后面慢
            dice.position = Vector3.Lerp(startPos, peakPos, eased);
            dice.Rotate(randomSpin * Time.deltaTime / throwDuration);  // 翻滚
            yield return null;
        }

        // ===== 阶段2：骰子自由落体 =====
        rb.isKinematic = false;
        rb.velocity = Vector3.up * 1f;   // 给一点残余上升速度，衔接更自然
        rb.angularVelocity = Random.insideUnitSphere * throwTorque;

        // ===== 阶段3：等骰子落地 =====
        float wait = 0f;
        while (wait < landWaitTime)
        {
            wait += Time.deltaTime;
            // 速度够小 = 已落地
            if (rb.velocity.sqrMagnitude < 0.1f && rb.angularVelocity.sqrMagnitude < 0.1f)
                break;
            yield return null;
        }
        yield return new WaitForSeconds(3f);
        // ===== 阶段4：读点数 → 显示加成 → 应用加成 =====
        int faceValue = GetDiceFaceUp(dice);

        Debug.Log($"[休息] 骰子点数={faceValue}，伤害+1");

        // 10点：所有牌型全加；1-9点：只加对应牌型
        if (faceValue == 10)
        {
            // 计分板的牌型轮没法显示"全部牌型"，10点保留骰子头上的飘字
            ShowDiceResult(dice.position, faceValue);
            PokerResolver.AddBonusToAll(1);
            Debug.Log("[休息] 10点！所有牌型永久：伤害+1");
        }
        else
        {
            HandType ht = PokerResolver.DiceToHandType(faceValue);
            int damageBefore = PokerResolver.GetHandDamage(ht);   // 加之前的当前伤害
            PokerResolver.AddBonus(ht, 1);
            Debug.Log($"[休息] {faceValue}点！{PokerResolver.HandName(ht)}永久：伤害+1");

            // 计分板演出：滚到当前牌型+伤害 → 停一秒 → 数字 +1；没配板子就退回飘字
            ScoreBoard3D board = BattleView.Instance != null ? BattleView.Instance.scoreBoard : null;
            if (board != null)
                yield return board.PlayDiceResult(ht, damageBefore, 1f);
            else
                ShowDiceResult(dice.position, faceValue);
        }

        // 等待 5 秒让玩家看清结果
        yield return new WaitForSeconds(3f);

        MapManager.Instance.FinishNonBattleNode();
    }

    // 读十面骰子朝上的面：骰子预制体每个面放一个空子物体命名 Face_1~Face_10
    // 落地后找 Y 最高的那个 = 朝上的面
    int GetDiceFaceUp(Transform dice)
    {
        float maxY = float.MinValue;
        int bestFace = -1;
        for (int i = 0; i < dice.childCount; i++)
        {
            Transform child = dice.GetChild(i);
            if (!child.name.StartsWith("Face_")) continue;
            // 子物体在世界空间的 Y 坐标
            float y = child.position.y;
            if (y > maxY)
            {
                maxY = y;
                string numStr = child.name.Substring(5);   // "Face_3" → "3"
                int.TryParse(numStr, out bestFace);
            }
        }
        return bestFace;   // 1~10
    }

    // 骰子头上飘字显示结果
    void ShowDiceResult(Vector3 dicePos, int faceValue)
    {
        StartCoroutine(ShowDiceResultRoutine(dicePos, faceValue));
    }

    IEnumerator ShowDiceResultRoutine(Vector3 dicePos, int faceValue)
    {
        HandType ht = PokerResolver.DiceToHandType(faceValue);
        string text = faceValue == 10
            ? "<size=150%>10点 全牌型</size>\n伤害 +1"
            : $"<size=150%>{faceValue}点 {PokerResolver.HandName(ht)}</size>\n伤害 +1";

        GameObject go = new GameObject("DiceResultText");
        Camera cam = Camera.main;
        if (cam != null) go.transform.rotation = cam.transform.rotation;

        var tmp = go.AddComponent<TMPro.TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 2.5f;
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.sortingOrder = 60;
        tmp.color = faceValue == 10 ? new Color(1f, 0.85f, 0.3f) : Color.white;   // 10点金色，其他白色

        go.transform.position = dicePos + Vector3.up * 1.5f + new Vector3(0, 0, 0.8f);
        go.transform.localScale = Vector3.one * 2;

        // PopIn
        yield return PopIn(go.transform, 0.18f);
        yield return new WaitForSeconds(5f);
        // PopOut
        yield return PopOut(go.transform, 0.3f);
        Destroy(go);
    }

    IEnumerator PopIn(Transform t, float dur)
    {
        Vector3 target = t.localScale;
        t.localScale = Vector3.zero;
        float time = 0f;
        while (time < dur)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / dur);
            t.localScale = Vector3.Lerp(Vector3.zero, target, Mathf.SmoothStep(0f, 1f, p));
            yield return null;
        }
        t.localScale = target;
    }

    IEnumerator PopOut(Transform t, float dur)
    {
        Vector3 start = t.localScale;
        float time = 0f;
        while (time < dur)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / dur);
            t.localScale = Vector3.Lerp(start, Vector3.zero, p * p);
            yield return null;
        }
        t.localScale = Vector3.zero;
    }
}
