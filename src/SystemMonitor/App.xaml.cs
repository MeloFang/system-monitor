using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SystemMonitor.Core.Diagnostics;

namespace SystemMonitor;

/// <summary>
/// 单实例 WPF 应用 + 全局异常收集
/// </summary>
public partial class App : System.Windows.Application
{
    private static readonly string MutexName = "Global\\SystemMonitor-SingleInstance";
    private static Mutex? _mutex;

    public App()
    {
        // ========== 单实例检查（放在最前面，防止多开）==========
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // 已有实例在运行 → 找到旧窗口并激活，然后退出
            AppLog.Info("检测到已有实例，尝试激活旧窗口...");
            ActivateExistingInstance();
            Environment.Exit(0);
            return;
        }

        // ========== 全局异常收集 ==========
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        AppLog.Info("==== SystemMonitor 启动 ====");
        AppLog.Info(AppLog.EnvironmentSnapshot().Replace(Environment.NewLine, " | "));
    }

    /// <summary>
    /// 通过枚举进程找到旧实例并激活其主窗口
    /// </summary>
    private static void ActivateExistingInstance()
    {
        try
        {
            var currentPid = Environment.ProcessId;
            var processes = System.Diagnostics.Process.GetProcessesByName("SystemMonitor");
            foreach (var p in processes)
            {
                if (p.Id == currentPid) continue;

                // 找到旧进程，尝试激活其窗口
                AppLog.Info($"发现旧实例 (PID={p.Id})，发送激活消息");

                // 简单做法：直接弹窗告知用户
                System.Windows.MessageBox.Show(
                    "SystemMonitor 已经在运行中。\n\n旧实例可能最小化在系统托盘，请检查右下角。",
                    "SystemMonitor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                break;
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("激活旧实例失败", ex);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("未处理的 UI 线程异常", e.Exception);
        var crashPath = AppLog.WriteCrash(e.Exception);
        e.Handled = true; // 不让进程直接退，先让用户看清错误
        try
        {
            System.Windows.MessageBox.Show(
                "程序出现未处理的错误，已被记录。\n\n" + e.Exception.Message +
                "\n\n日志目录：\n" + AppLog.LogDirectory +
                (crashPath is null ? "" : "\n崩溃报告：\n" + crashPath) +
                "\n\n（托盘菜单「导出诊断报告」可把报告发回给开发者）",
                "SystemMonitor", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch { }
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            AppLog.Error("未处理的异常（后台线程，进程可能终止）", ex);
            AppLog.WriteCrash(ex);
        }
    }

    private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLog.Error("未观测的后台 Task 异常", e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
