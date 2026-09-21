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
}