using System;
using System.Threading;
using UnityEditor;

// ============================================================
// 主线程调度辅助 — 将闭包投递到 Unity 主线程并阻塞等待结果
// ============================================================
internal static class McpMainThread
{
    public static T Invoke<T>(Func<T> action, TimeSpan? timeout = null)
    {
        T result = default;
        Exception exception = null;
        var evt = new ManualResetEventSlim(false);

        EditorApplication.delayCall += () =>
        {
            try { result = action(); }
            catch (Exception ex) { exception = ex; }
            finally { evt.Set(); }
        };

        if (!evt.Wait(timeout ?? TimeSpan.FromSeconds(30)))
            throw new TimeoutException("Unity 主线程在指定时间内未响应");

        if (exception != null)
            throw exception;

        return result;
    }
}
