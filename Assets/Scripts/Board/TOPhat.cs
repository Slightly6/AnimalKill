using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TOPhat : Singleton<TOPhat>
{
    public float radius = 0.5f;
    public float speed = 90f;
    public Vector3 CenterOffset = new Vector3(0, 0.5f, 0);
    public bool IsBusy { get; private set; }=false;
    public bool sai=false;
    public GameObject nodePrefab;      // Inspector 里拖 Prefab 进来

    // Update is called once per frame
    void Update()
    {
        if (sai)
        {
            StartCoroutine(PlayCollect(10));
            sai = false;
        }
    }
    public IEnumerator PlayCollect(int gold)
    {
        IsBusy = true;

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

        while (gold > 0)
        {
            GameObject dice = Instantiate(nodePrefab, CenterOffset + new Vector3(0, 10, 0), Quaternion.identity);
            Rigidbody rb = dice.GetComponent<Rigidbody>();
            if (rb != null) rb.velocity = Vector3.down * 17f;
            gold--;
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
        }
    }
}
