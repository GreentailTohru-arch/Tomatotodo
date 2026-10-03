namespace Tomatotodo_Windows.Accounts;

/// <summary>A kernel mutex survives competing processes, but is released on process exit.
/// A dedicated owner thread keeps mutex ownership valid across async continuations.</summary>
public sealed class DeviceAccountLease(string name = @"Global\Tomatotodo.AccountSession.v1") : IDisposable
{
    private readonly object _gate = new();
    private ManualResetEventSlim? _release;
    private Thread? _owner;

    public void Acquire()
    {
        lock (_gate)
        {
            if (_owner is not null) return;
            using var ready = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            Exception? error = null;
            var owner = new Thread(() =>
            {
                var acquired = false;
                try
                {
                    using var mutex = new Mutex(false, name);
                    try { acquired = mutex.WaitOne(0); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u6B64\u8BBE\u5907\u5DF2\u6709\u5728\u7EBF\u8D26\u6237\uFF0C\u8BF7\u5148\u5728\u53E6\u4E00\u4E2A\u7A97\u53E3\u9000\u51FA\u767B\u5F55\u3002"));
                    ready.Set();
                    try { release.Wait(); }
                    finally { mutex.ReleaseMutex(); }
                }
                catch (Exception ex) { error = ex; ready.Set(); }
            }) { IsBackground = true, Name = "Account session lease" };
            owner.Start(); ready.Wait();
            if (error is not null)
            {
                owner.Join(); release.Dispose();
                throw new InvalidOperationException(global::Tomatotodo_Windows.Data.UiText.T("\u6B64\u8BBE\u5907\u5DF2\u6709\u5728\u7EBF\u8D26\u6237\uFF0C\u6216\u65E0\u6CD5\u53D6\u5F97\u8BBE\u5907\u4F1A\u8BDD\u9501\u3002\u8BF7\u5148\u9000\u51FA\u5176\u4ED6\u7A97\u53E3\u4E2D\u7684\u8D26\u6237\u3002"), error);
            }
            _release = release; _owner = owner;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _release?.Set(); _owner?.Join(); _release?.Dispose();
            _release = null; _owner = null;
        }
    }
}
