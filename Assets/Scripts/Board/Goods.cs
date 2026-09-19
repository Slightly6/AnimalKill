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
        ShopItemDataSO data = itemData;   // 先存住：下面要销毁 Goods 组件

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
        transform.position=new Vector3(targetPos.x,transform.position.y,targetPos.z);
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.AddForce(Vector3.down * 10f, ForceMode.Impulse);

        // 落地后它就是桌上道具：销毁 Goods 组件（防 GoodsManager 再把它当商品点一次），
        // 加 TableItem 并注入数据。协程由 GoodsManager 启动，销毁本组件不会中断。
        Destroy(GetComponent<Goods>());
        TableItem tableItem = gameObject.AddComponent<TableItem>();
        tableItem.Setup(data);

        // 到位停一下让玩家看清掉落，然后卷轴重新掉下来选下一关（不切场景）
        yield return new WaitForSeconds(2f);
        if (MapManager.Instance != null)
            MapManager.Instance.FinishNonBattleNode();
    }
}