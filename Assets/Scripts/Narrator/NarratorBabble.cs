using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;


//表现层
public class NarratorBubble : Singleton<NarratorBubble>, IPointerClickHandler
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text speakerLabel;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private float typeSpeed = 0.03f;   // 每字间隔
    [SerializeField] private float holdDuration = 2.5f; // 显示完后停留

    private Coroutine _routine;
    private bool _isTyping;
    private string _fullText;

    private void Start()
    {
        EventBus.Subscribe<SayEvent>(OnSay);
    }
    private void OnDestroy()
    {
        EventBus.Unsubscribe<SayEvent>(OnSay);
    }

    private void OnSay(SayEvent e)
    {
        // 防骚扰 3：新台词顶掉旧的
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(PlayRoutine(e));
    }

    private IEnumerator PlayRoutine(SayEvent e)
    {
        // 立即整句显示
        speakerLabel.text = e.Speaker;
        _fullText = e.Text;

        group.alpha = 1f;
        bodyText.text = "";
        _isTyping = true;

        // 打字机
        for (int i = 0; i < _fullText.Length; i++)
        {
            // 说话期间点击可跳过打字
            if (!_isTyping) break;
            bodyText.text = _fullText.Substring(0, i + 1);
            yield return new WaitForSecondsRealtime(typeSpeed);
        }

        bodyText.text = _fullText;
        _isTyping = false;

        // 停留后淡出
        yield return new WaitForSecondsRealtime(holdDuration);
        yield return FadeOut(0.4f);

        _routine = null;
    }

    private IEnumerator FadeOut(float duration)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(1f, 0f, t / duration);
            yield return null;
        }
        group.alpha = 0f;
    }

    // 点击气泡：打字中→立即整句；已显示完→直接消失
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isTyping)
        {
            _isTyping = false;      // 打断打字循环，下一帧会补全
        }
        else if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
            group.alpha = 0f;
        }
    }
}