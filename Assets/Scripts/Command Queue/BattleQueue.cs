using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局命令队列：一个做完再做下一个。
/// 不用手动挂场景（Singleton 找不到会自动建）。
///
/// 用法：
///   BattleQueue.Instance.Do(() => 某件同步的事());
///   BattleQueue.Instance.Wait(1.5f);                              // 等秒数
///   BattleQueue.Instance.Until(() => !hat.IsBusy, 5f);            // 等条件（带超时兜底）
///   BattleQueue.Instance.Run(某个老协程());                        // 老协程原样排队
///   BattleQueue.Instance.Enqueue(new 自定义Command());
/// </summary>
public class BattleQueue : Singleton<BattleQueue>
{
    private readonly Queue<IBattleCommand> queue = new Queue<IBattleCommand>();
    private bool executing;

    // 正在播 + 排队中都算没空。关卡清场前等它变 true
    public bool IsEmpty => !executing && queue.Count == 0;

    public void Enqueue(IBattleCommand command)
    {
        queue.Enqueue(command);
        TryNext();
    }

    // —— 便捷入队 ——
    public void Do(Action action)
    {
        Enqueue(new ActionCommand(action));
    }
    // public void Wait(float seconds) => Run(new WaitForSeconds(seconds)); //WaitForSeconds不能当IEnumerator作为参数

    public void Wait(float seconds)
    {
        Run(WaitRoutine(seconds));
    }

    private IEnumerator WaitRoutine(float seconds)
    {
        yield return new WaitForSeconds(seconds);
    }

    public void Until(Func<bool> condition, float timeout = 10f)
    {
        Run(UntilRoutine(condition, timeout));
    }

    // 老协程零改动进队列的入口
    public void Run(IEnumerator routine) => Enqueue(new CoroutineCommand(this, routine));

    private void TryNext()
    {
        if (executing || queue.Count == 0) return;

        executing = true;
        queue.Dequeue().Execute(OnCommandFinished);
    }

    private void OnCommandFinished()
    {
        executing = false;
        TryNext();
    }

    private IEnumerator UntilRoutine(Func<bool> condition, float timeout)
    {
        float timer = 0f;
        while (!condition())
        {
            timer += Time.deltaTime;
            if (timer >= timeout)
            {
                Debug.LogWarning("[BattleQueue] 等条件超时（" + timeout + " 秒），强制放行：" + condition);
                yield break;
            }
            yield return null;
        }
    }
}

// 同步的一行逻辑（扣血、发事件、改数据……）
public class ActionCommand : IBattleCommand
{
    private readonly Action action;
    public ActionCommand(Action action) { this.action = action; }

    public void Execute(Action onComplete)
    {
        action?.Invoke();
        onComplete();
    }
}

// 协程适配器：把任意老协程包成队列动作，协程跑完才回调
public class CoroutineCommand : IBattleCommand
{
    private readonly MonoBehaviour host;
    private readonly IEnumerator routine;
    private bool finished;   // 防止 onComplete 被调两次

    public CoroutineCommand(MonoBehaviour host, IEnumerator routine)
    {
        this.host = host;
        this.routine = routine;
    }

    public void Execute(Action onComplete)
    {
        host.StartCoroutine(Run(onComplete));
    }

    private IEnumerator Run(Action onComplete)
    {
        yield return host.StartCoroutine(routine);   // 等老协程真正结束

        if (finished) yield break;
        finished = true;
        onComplete();
    }
}