using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PotArea : MonoBehaviour
{
    private const int StacksPerPattern = 5;

    [Header("Coin layout")]
    public float coinHeight = 0.02f;
    public int coinsPerStack = 10;

    [Header("Five-stack pattern")]
    public float patternSpacing = 0.55f;
    public int groupsPerRow = 3;
    public float groupSpacing = 1.8f;
    public float groupRowSpacing = 1.8f;

    [Header("Before rearrange")]
    public float scatterRange = 0.2f;
    public ChipPool pool;

    private void Awake()
    {
        if (pool == null)
            pool = FindObjectOfType<ChipPool>();   // 场景里只有一个 ChipPool，自动捡
    }
    private readonly List<GameObject> potCoins =
        new List<GameObject>();

    public void ReceiveCoin(GameObject coin)
    {
        if (coin == null)
            return;

        if (!potCoins.Contains(coin))
            potCoins.Add(coin);

        coin.transform.SetParent(transform, true);

        Rigidbody rb = coin.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 确保这枚筹码可以被单独点击
        PotCoinClick click = coin.GetComponent<PotCoinClick>();

        if (click == null)
            click = coin.AddComponent<PotCoinClick>();

        click.Initialize(this);
    }

    public void OnCoinClicked(GameObject clickedCoin)
    {
        if (clickedCoin == null)
            return;

        if (!potCoins.Contains(clickedCoin))
            return;

        Rearrange();
    }

    // 算某枚筹码在摞里的准确落点（和 Rearrange 同一套公式，掉落动画直接往这里飞，落地不瞬移）
    public Vector3 GetCoinWorldPos(GameObject coin)
    {
        int i = potCoins.IndexOf(coin);
        if (i < 0) return transform.position;

        int stackIndex = i / coinsPerStack;
        int level = i % coinsPerStack;
        int groupIndex = stackIndex / StacksPerPattern;
        int slotIndex = stackIndex % StacksPerPattern;

        return transform.TransformPoint(GetLocalPosition(groupIndex, slotIndex, level));
    }

    public void Rearrange()
    {
        coinsPerStack = Mathf.Max(1, coinsPerStack);
        groupsPerRow = Mathf.Max(1, groupsPerRow);

        for (int i = 0; i < potCoins.Count; i++)
        {
            GameObject coin = potCoins[i];

            if (coin == null)
                continue;

            int stackIndex = i / coinsPerStack;
            int level = i % coinsPerStack;

            int groupIndex = stackIndex / StacksPerPattern;
            int slotIndex = stackIndex % StacksPerPattern;

            Vector3 localPosition =
                GetLocalPosition(groupIndex, slotIndex, level);

            Quaternion localRotation = Quaternion.identity;

            Rigidbody rb = coin.GetComponent<Rigidbody>();

            coin.transform.SetParent(transform, false);

            if (rb != null)
            {
                rb.isKinematic = true;

                rb.position = transform.TransformPoint(localPosition);
                rb.rotation = transform.rotation;
            }
            else
            {
                coin.transform.localPosition = localPosition;
                coin.transform.localRotation = localRotation;
            }
        }
    }

    private Vector3 GetLocalPosition(
        int groupIndex,
        int slotIndex,
        int level)
    {
        Vector3 slotOffset = GetSlotOffset(slotIndex);

        int groupColumn = groupIndex % groupsPerRow;
        int groupRow = groupIndex / groupsPerRow;

        Vector3 groupOffset = new Vector3(
            groupColumn * groupSpacing,
            0f,
            groupRow * groupRowSpacing
        );

        return groupOffset
             + slotOffset
             + Vector3.up * (level * coinHeight);
    }

    private Vector3 GetSlotOffset(int slotIndex)
    {
        float s = patternSpacing;

        switch (slotIndex)
        {
            case 0:
                return new Vector3(-s, 0f, -s);

            case 1:
                return new Vector3(s, 0f, -s);

            case 2:
                return Vector3.zero;

            case 3:
                return new Vector3(-s, 0f, s);

            case 4:
                return new Vector3(s, 0f, s);

            default:
                return Vector3.zero;
        }
    }

    [Header("离场飞走（关卡结束清主池）")]
    public float clearFlyDuration = 0.45f;   // 单枚飞出去的用时
    public float clearFlyInterval = 0.015f;  // 逐枚出发间隔
    public float clearFlyApex = 0.5f;        // 途中拱起的高度

    // 关卡结束：主池不原地收回对象池，逐枚沿弧线飞出屏幕，飞到了再还池
    public IEnumerator ClearCoinsFly(Transform flyTarget)
    {
        if (potCoins.Count == 0) yield break;

        List<GameObject> leaving = new List<GameObject>(potCoins);
        potCoins.Clear();   // 先清账：这里开始谁也不再往这摞里算

        // 没配飞出点就往下飞：至少有个方向，不会原地消失
        Vector3 to = flyTarget != null
            ? flyTarget.position
            : transform.position + Vector3.down * 10f;

        List<Coroutine> flights = new List<Coroutine>();
        foreach (GameObject coin in leaving)
        {
            if (coin == null) continue;
            flights.Add(StartCoroutine(FlyAwayOne(coin, to)));
            if (clearFlyInterval > 0f)
                yield return new WaitForSeconds(clearFlyInterval);
        }

        foreach (Coroutine f in flights)
            if (f != null) yield return f;
    }

    private IEnumerator FlyAwayOne(GameObject coin, Vector3 to)
    {
        coin.transform.SetParent(null, true);   // 离池：主池清场不再带着它

        Rigidbody rb = coin.GetComponent<Rigidbody>();
        Vector3 startPos = rb != null ? rb.position : coin.transform.position;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        float dur = Mathf.Max(0.05f, clearFlyDuration);
        float t = 0f;
        while (t < dur && coin != null)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            float k = 1f - (1f - p) * (1f - p);   // 先快后慢：甩出去，再被拖住

            Vector3 pos = Vector3.Lerp(startPos, to, k);
            pos.y += Mathf.Sin(p * Mathf.PI) * clearFlyApex;

            if (rb != null) rb.position = pos;
            else coin.transform.position = pos;
            yield return null;
        }

        if (coin == null) yield break;

        if (rb != null) rb.detectCollisions = true;   // 回池前还原：下一关复用这颗币还要靠碰撞叠摞
        if (pool != null) pool.Return(coin);
        else coin.SetActive(false);
    }

    public int GetCoinCount()
    {
        return potCoins.Count;
    }
}