using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//逻辑层
//根据禁用功能提供防骚扰操作，3秒冷却等
//发布事件，表现层通过订阅实现不用对话
//

public static class Narrator
{
    // 事件：气泡 UI 订阅这个

    private const float TopicCooldown = 3f;
    private static readonly Dictionary<SpeakTopic, float> _lastSayTime = new();
    private static string _currentSpeaker = "引路人：";

    public static void SetSpeaker(string speaker) => _currentSpeaker = speaker;

    public static void Say(SpeakTopic topic)
    {
        // 防骚扰 1：同话题 3 秒冷却
        if (_lastSayTime.TryGetValue(topic, out float last))
        {
            if (Time.unscaledTime - last < TopicCooldown)
                return;
        }
        _lastSayTime[topic] = Time.unscaledTime;

        string text = NarratorLines.GetRandom(topic);
        if (string.IsNullOrEmpty(text)) return;

        // 防骚扰 2：新台词直接顶掉旧的，不排队
        EventBus.Publish(new SayEvent
        {
            Speaker = _currentSpeaker,
            Text = text,
            Topic = topic
        });
    }

    // 常驻台词：跳过冷却、不自动淡出（敌人意图用——亮到敌人行动才消失）
    public static void SaySticky(SpeakTopic topic)
    {
        string text = NarratorLines.GetRandom(topic);
        if (string.IsNullOrEmpty(text)) return;

        EventBus.Publish(new SayEvent
        {
            Speaker = _currentSpeaker,
            Text = text,
            Topic = topic,
            Sticky = true
        });
    }

    // 常驻台词，直接给文本：需要拼接动态内容时用（比如"你持有的骰子数量为 12"）
    public static void SayStickyText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        EventBus.Publish(new SayEvent
        {
            Speaker = _currentSpeaker,
            Text = text,
            Sticky = true
        });
    }

    // 强制收掉气泡（敌人开始行动时调）
    public static void Hide()
    {
        EventBus.Publish(new NarratorHideEvent());
    }
}