using System.Collections;
using UnityEngine;

public class Goods : MonoBehaviour
{
    [Header("道具数据（拖入对应 .asset）")]
    public ShopItemDataSO itemData;   // 这个商品买下后给玩家哪个道具

    public bool isFlying = false;

    public IEnumerator FlyToTarget(Vector3 targetPos)
    {
        isFlying = true;

        Vector3 startPos = transform.position;
        Vector3 upPos = startPos + Vector3.up * 7f;

        // 第一段：竖直浮上去
        float t = 0f;
        float upDuration = 0.3f;
        while (t < upDuration)
        {
            t += Time.deltaTime;
            float p = t / upDuration;
            transform.position = Vector3.Lerp(startPos, upPos, p);
            yield return null;
        }

        // 第二段：掉落到目标点
        t = 0f;
        float fallDuration = 0.7f;
        transform.position=new Vector3(targetPos.x,transform.position.y,targetPos.z);
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.AddForce(Vector3.down * 10f, ForceMode.Impulse);
        // while (t < fallDuration)
        // {
        //     t += Time.deltaTime;
        //     float p = t / fallDuration;
        //     transform.position = Vector3.Lerp(transform.position, targetPos, p);
        //     yield return null;
        // }
        // transform.position = targetPos;

        // 到位后返回地图
        yield return new WaitForSeconds(2f);

        string mapScene = "Map";
        if (MapManager.Instance != null) mapScene = MapManager.Instance.mapSceneName;
        FadeManager.Go(mapScene);
    }
}