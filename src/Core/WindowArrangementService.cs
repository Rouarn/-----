using System;
using System.Threading;
using System.Threading.Tasks;
using OnmyojiTools.Diagnostics;
using OnmyojiTools.Interop;

namespace OnmyojiTools.Core
{
    /// <summary>窗口网格排列：先探测客户端可接受尺寸，再逐窗口定位。</summary>
    internal sealed class WindowArrangementService : IWindowArranger
    {
        private const int ProbeDelayMilliseconds = 400;
        private const int ProbeHeightStep = 40;
        private const int PlacementDelayMilliseconds = 300;

        private readonly IGameWindowService _windows;

        public WindowArrangementService(IGameWindowService windows)
        {
            _windows = windows;
        }

        public async Task<ArrangeResult> ArrangeAsync(int columns, AppLogger logger, CancellationToken cancellationToken)
        {
            ArrangeResult result = new ArrangeResult();
            columns = InputValidation.Clamp(columns, InputValidation.MinColumns, InputValidation.MaxColumns);
            result.Columns = columns;

            try
            {
                var windows = _windows.GetGameWindows();
                result.WindowCount = windows.Count;
                if (windows.Count == 0)
                {
                    result.Error = "未识别到任何阴阳师窗口，请先启动游戏并刷新。";
                    logger.Log(result.Error);
                    return result;
                }

                NativeMethods.RECT workArea;
                if (!NativeMethods.TryGetWorkArea(out workArea) || workArea.IsEmpty)
                {
                    workArea = new NativeMethods.RECT
                    {
                        Left = 0,
                        Top = 0,
                        Right = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN)),
                        Bottom = Math.Max(1, NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN))
                    };
                    logger.Log("警告：读取系统工作区失败，已回退到整屏尺寸。");
                }

                GridLayout layout = GridLayout.Create(windows.Count, columns, workArea);
                logger.Log(string.Format("开始排列 {0} 个窗口，每行 {1} 个，工作区 {2}×{3}。",
                    windows.Count, columns, workArea.Width, workArea.Height));
                logger.Log("正在探测客户端接受的窗口尺寸...");

                ProbeResult probe = await ProbeAsync(windows[0].Handle, layout, cancellationToken).ConfigureAwait(false);
                if (probe == null)
                {
                    result.Canceled = true;
                    logger.Log("排列已取消。");
                    return result;
                }

                result.ProbeWidth = probe.Width;
                result.ProbeHeight = probe.Height;
                logger.Log(string.Format("客户端接受尺寸：{0}×{1}。", probe.Width, probe.Height));

                for (int i = 0; i < windows.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    GameWindowInfo window = windows[i];
                    int row = i / columns;
                    int column = i % columns;

                    NativeMethods.ShowWindow(window.Handle, NativeMethods.SW_RESTORE);
                    NativeMethods.SetWindowPos(window.Handle, IntPtr.Zero,
                        layout.WorkArea.Left, layout.WorkArea.Top, probe.Width, probe.Height,
                        NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                    await Task.Delay(PlacementDelayMilliseconds, cancellationToken).ConfigureAwait(false);

                    NativeMethods.RECT actual;
                    if (!NativeMethods.GetWindowRect(window.Handle, out actual) || actual.IsEmpty)
                    {
                        result.Failed++;
                        logger.Log(string.Format("[{0}/{1}] 无法读取窗口尺寸，已跳过。", i + 1, windows.Count));
                        continue;
                    }

                    int x = layout.CellX(column, actual.Width);
                    int y = layout.CellY(row, actual.Height);
                    bool moved = NativeMethods.SetWindowPos(window.Handle, IntPtr.Zero, x, y, actual.Width, actual.Height,
                        NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);

                    if (moved)
                    {
                        result.Moved++;
                        logger.Log(string.Format("[{0}/{1}] {2} -> ({3}, {4}) {5}×{6}",
                            i + 1, windows.Count, window.Title, x, y, actual.Width, actual.Height));
                    }
                    else
                    {
                        result.Failed++;
                        logger.Log(string.Format("[{0}/{1}] 定位失败：{2}", i + 1, windows.Count, window.Title));
                    }
                }

                logger.Log(string.Format("排列完成：成功 {0} 个，失败 {1} 个。", result.Moved, result.Failed));
            }
            catch (OperationCanceledException)
            {
                result.Canceled = true;
                logger.Log("排列已取消。");
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                logger.LogError("排列失败", ex);
            }

            return result;
        }

        private async Task<ProbeResult> ProbeAsync(IntPtr window, GridLayout layout, CancellationToken cancellationToken)
        {
            NativeMethods.ShowWindow(window, NativeMethods.SW_RESTORE);
            int height = layout.StartHeight;

            while (height >= GridLayout.MinimumProbeHeight)
            {
                cancellationToken.ThrowIfCancellationRequested();

                NativeMethods.SetWindowPos(window, IntPtr.Zero,
                    layout.WorkArea.Left, layout.WorkArea.Top, layout.TargetWidth, height,
                    NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
                await Task.Delay(ProbeDelayMilliseconds, cancellationToken).ConfigureAwait(false);

                NativeMethods.RECT actual;
                if (NativeMethods.GetWindowRect(window, out actual))
                {
                    if (actual.Width <= layout.TargetWidth + 4)
                        return new ProbeResult { Width = actual.Width, Height = actual.Height };
                }

                height -= ProbeHeightStep;
            }

            NativeMethods.RECT fallback;
            if (NativeMethods.GetWindowRect(window, out fallback) && !fallback.IsEmpty)
                return new ProbeResult { Width = fallback.Width, Height = fallback.Height };

            return new ProbeResult { Width = layout.TargetWidth, Height = GridLayout.MinimumProbeHeight };
        }

        private sealed class ProbeResult
        {
            public int Width { get; set; }
            public int Height { get; set; }
        }
    }
}
