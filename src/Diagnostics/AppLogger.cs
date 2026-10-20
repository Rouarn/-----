using System;

namespace OnmyojiTools.Diagnostics
{
    /// <summary>线程安全的界面日志入口；UI 未就绪时先缓冲，挂接后按顺序回放。</summary>
    internal sealed class AppLogger
    {
        private readonly object _gate = new object();
        private readonly System.Collections.Generic.List<string> _pending = new System.Collections.Generic.List<string>();
        private Action<string> _sink;
        private bool _accepting = true;

        public void Attach(Action<string> sink)
        {
            if (sink == null) return;
            string[] flush;
            lock (_gate)
            {
                _sink = sink;
                flush = _pending.ToArray();
                _pending.Clear();
            }
            for (int i = 0; i < flush.Length; i++) SafeInvoke(sink, flush[i]);
        }

        public void Detach()
        {
            lock (_gate)
            {
                _sink = null;
                _accepting = false;
                _pending.Clear();
            }
        }

        public void Log(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Action<string> sink;
            lock (_gate)
            {
                sink = _sink;
                if (sink == null)
                {
                    if (_accepting && _pending.Count < 300) _pending.Add(message);
                    return;
                }
            }
            SafeInvoke(sink, message);
        }

        public void LogError(string context, Exception exception)
        {
            if (exception == null)
            {
                Log(context);
                return;
            }
            Log(context + "：" + exception.Message);
        }

        private static void SafeInvoke(Action<string> sink, string message)
        {
            try { sink(message); }
            catch { }
        }
    }
}
