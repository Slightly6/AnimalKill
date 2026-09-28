using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
public class EnemyHandManager : MonoBehaviour
{
    [Header("排列设置")]
    public float cardSpacing = 1.5f;    // 牌与牌的水平间距（牌少时用这个）
    public float maxHandWidth = 12f;    // 手牌最大总宽度，牌多了自动挤进这个范围
    public float selectLift = 0.2f;     // 被点选的牌沿自己 Y 轴突出多少（小一点，别全露出来）
    public float maxAngle = 8f;         // 最边上的牌倾斜多少度
    public float yOffset = 0.2f;        // 越靠边越往下沉
    public float zStep = 0.03f;         // 牌离相机的 Z 级差：越靠上的牌离相机越近，点击命中和视觉顺序一致
    public Transform HandCenter;


    void Update()
    {
        Arrange(); 
    }

    public void Arrange()
    {
        int count = BattleView.Instance.enemyHandViews.Count;
        if (count == 0) return;


          Vector3 center = HandCenter.position;

        // 牌越多越密集：总宽度超过 maxHandWidth 就压缩间距
        float spacing = cardSpacing;
        if (count > 1)
        {
            float totalWidth = (count - 1) * cardSpacing;
            if (totalWidth > maxHandWidth)
                spacing = maxHandWidth / (count - 1);
        }

        for (int i = 0; i < count; i++)
        {
            Card card = BattleView.Instance.enemyHandViews[i];
            if (card == null) continue;

            float t = count == 1 ? 0 : (i / (float)(count - 1) - 0.5f) * 2f;   // -1 ~ 1
            float x = (i - (count - 1) / 2f) * spacing;
            float sink = Mathf.Abs(t) * yOffset;

            Vector3 pos = center+ HandCenter.right * x - HandCenter.up * sink;

            pos -= HandCenter.forward * (i * zStep);

            card.transform.position = pos;

            float angle = -t * maxAngle;
            card.transform.rotation = HandCenter.rotation * Quaternion.Euler(0, 0, angle);

            SortingGroup sg = card.sortingGroup;
            if (sg != null)
            {
                int order = 10 + i;
                sg.sortingOrder = order;
            }
        }
    }



}
