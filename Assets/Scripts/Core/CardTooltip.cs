using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
/// <summary>
/// 卡牌悬浮提示：全场景共享一个面板，鼠标悬到哪张卡就显示那张的 名字+描述。
/// </summary>
public class CardTooltip : MonoBehaviour
{
    public TextMeshPro tmp;      
    public Vector3 offset = new Vector3(0, 2f, 0);  

    void Start()
    {
        if (tmp != null) tmp.gameObject.SetActive(false);
    }

    public void Show(Card card)
    {
        if (tmp == null || card == null || card.Data == null) return;

        string desc = string.IsNullOrEmpty(card.Data.description) ? "" : "\n<size=80%>" + card.Data.description;
        tmp.text = "<b>" + card.Data.abilityName + "</b>" + desc;

        // 跟到卡上方，面向相机
        tmp.transform.position = card.transform.position + offset;
        if (Camera.main != null) tmp.transform.rotation = Camera.main.transform.rotation;
        tmp.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (tmp != null) tmp.gameObject.SetActive(false);
    }
}