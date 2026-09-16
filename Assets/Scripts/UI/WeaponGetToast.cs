using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 获得武器提示：挂在 Canvas 下的提示文本（TextMeshProUGUI）上。
/// 监听 WeaponGetEvent，弹「获得 XXX」，几秒后自己消失。
/// </summary>
public class WeaponGetToast : MonoBehaviour
{
    [Header("提示文本（本脚本挂的 TMP 文本）")]
    public TextMeshProUGUI toastText;

    [Header("显示几秒")]
    public float showSeconds = 3f;

    private Coroutine hideRoutine;

    void Start()
    {
        if (toastText == null) toastText = GetComponent<TextMeshProUGUI>();
        if (toastText != null) toastText.gameObject.SetActive(false);   // 一开始藏着
        EventBus.Subscribe<WeaponGetEvent>(OnWeaponGet);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<WeaponGetEvent>(OnWeaponGet);
    }

    void OnWeaponGet(WeaponGetEvent e)
    {
        if (toastText == null) return;

        toastText.text = "获得 " + e.weaponName;
        toastText.gameObject.SetActive(true);

        // 重新计时（连续获得时从最后一次开始算）
        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfter(showSeconds));
    }

    IEnumerator HideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        toastText.gameObject.SetActive(false);
    }
}
