using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//数据层
//存储禁用功能随机对话
//
public enum SpeakTopic
{
    MapMustPickFirst,
    ActionDuringMap,
    WrongPhase_Item,
    DrawAlreadyUsed,
    HandFull,
    BellLocked,
    ShopNotHere,
    ShopFull,
    ChestLocked,
    NodeLocked,

    //战斗中敌人出牌操作显示
    Check,//过牌加筹码
    Call,//攻击
    Raise//加强

}

public static class NarratorLines
{
    public static readonly Dictionary<SpeakTopic, string[]> Lines = new()
    {
        { SpeakTopic.MapMustPickFirst, new[] { "先选你的路。", "地图还摊着呢，选一关。" } },
        { SpeakTopic.ActionDuringMap,  new[] { "现在？看着地图呢。", "等我把地图收起来。" } },
        { SpeakTopic.WrongPhase_Item,  new[] { "这时候用不了道具。", "等发牌，别急。" } },
        { SpeakTopic.DrawAlreadyUsed,  new[] { "这回合的牌抽过了。" } },
        { SpeakTopic.HandFull,         new[] { "手里攥不下了。" } },
        { SpeakTopic.BellLocked,       new[] { "钟还不能敲。", "先把你的牌出完。" } },
        { SpeakTopic.ShopNotHere,      new[] { "这儿没有东西卖给你。" } },
        { SpeakTopic.ShopFull,         new[] { "桌子放不下了，四样够多了。" } },
        { SpeakTopic.ChestLocked,      new[] { "（箱子锁着，现在不是碰它的时候。）" } },
        { SpeakTopic.NodeLocked,       new[] { "那条路现在走不通。" } },
        { SpeakTopic.Check,            new[] { "我选择过牌。" } },
        { SpeakTopic.Call,             new[] { "我想要出牌。" } },
        { SpeakTopic.Raise,            new[] { "我想要加强。" } },
    };

    public static string GetRandom(SpeakTopic topic)
    {
        if (!Lines.TryGetValue(topic, out var arr) || arr.Length == 0)
            return "";
        return arr[UnityEngine.Random.Range(0, arr.Length)];
    }
}