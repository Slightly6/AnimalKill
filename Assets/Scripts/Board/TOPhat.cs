using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TOPhat : Singleton<TOPhat>
{
    public float radius = 0.5f;
    public float speed = 90f;
    public Vector3 CenterOffset = new Vector3(0, 0.5f, 0);
    public bool IsBusy { get; private set; }=false;
    public bool sai=false;
    public GameObject nodePrefab;      // Inspector 里拖 Prefab 进来

    [Header("帽子计数显示")]
    public TextMeshPro countText;      // 帽子上的数字（子物体，不拖自动找）
    private int diceCollected;         // 本次已收的骰子数
    private int startDice;             // 本轮收集前的骰子总数
    private Coroutine punchRoutine;//

    void Start()
    {
        // 平时常驻显示当前拥有的骰子总数
        if (countText == null) countText = GetComponentInChildren<TextMeshPro>();
        if (countText != null) countText.text = GameProgress.dice.ToString();
    }

    // 骰子总数变了（商店消费等）让帽子数字跟着刷新
    public void RefreshCount()
    {
        if (countText == null) countText = GetComponentInChildren<TextMeshPro>();
        if (countText != null) countText.text = GameProgress.dice.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        if (sai)
        {
            StartCoroutine(PlayCollect(10));
            sai = false;
        }
    }
    public IEnumerator PlayCollect(int diceCount)
    {
        IsBusy = true;

        // 从收之前的总数开始，每进一颗 +1，收完正好是最新总数
        // （SettlePot 在演出前已把 GameProgress.dice 加好，所以要减回去当起点）
        startDice = Mathf.Max(0, GameProgress.dice - diceCount);
        diceCollected = 0;
        if (countText == null) countText = GetComponentInChildren<TextMeshPro>();
        if (countText != null) countText.text = startDice.ToString();

        // 去程：180° → 90°（从 (-1,0) 到 (0,1)）
        float startAngle = 180f;
        float goTarget   = 90f;
        float current    = startAngle;

        while (current > goTarget)          // 递减
        {
            current -= speed * Time.deltaTime;
            current = Mathf.Max(current, goTarget);
            SetCircularPosition(current);
            yield return null;
        }

        while (diceCount > 0)
        {
            GameObject dice = Instantiate(nodePrefab, CenterOffset + new Vector3(0, 10, 0), Quaternion.identity);
            Rigidbody rb = dice.GetComponent<Rigidbody>();
            if (rb != null) rb.velocity = Vector3.down * 17f;
            diceCount--;
            yield return new WaitForSeconds(0.1f);
        }
        yield return new WaitForSeconds(1f);

        // 回程：90° → 180°（回到 (-1,0)）
        while (current < startAngle)        // 递增
        {
            current += speed * Time.deltaTime;
            current = Mathf.Min(current, startAngle);
            SetCircularPosition(current);
            yield return null;
        }

        IsBusy = false;
    }
    void SetCircularPosition(float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        float y = Mathf.Cos(rad) * radius;//-1-0
        float z = Mathf.Sin(rad) * radius;//0-1

        transform.position = CenterOffset + new Vector3(0, z, y);
    }
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Dice"))
        {
            Destroy(other.gameObject);
            diceCollected++;
            if (countText != null)
            {
                countText.text = (startDice + diceCollected).ToString();
                if (punchRoutine != null) StopCoroutine(punchRoutine);
                // punchRoutine = StartCoroutine(PunchCountText());
            }
        }
    }

    // 数字弹一下（1 → 1.3 → 1），配合"骰子进帽"的节奏
    IEnumerator PunchCountText()
    {
        Transform t = countText.transform;
        Vector3 baseScale = t.localScale;
        float dur = 0.2f, time = 0f;
        while (time < dur)
        {
            time += Time.deltaTime;
            float p = Mathf.Clamp01(time / dur);
            float s = 1f + Mathf.Sin(p * Mathf.PI) * 0.3f;
            t.localScale = baseScale * s;
            yield return null;
        }
        t.localScale = baseScale;
    }
}
