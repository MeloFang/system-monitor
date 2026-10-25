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
    // 用 Local\ 前缀：只需当前用户会话可见，不需要 SeCreateGlobalPrivilege 特权。
    // （旧的 Global\ 前缀在非特权上下文创建会抛 UnauthorizedAccessException，导致启动即崩。）
    private static readonly string MutexName = "Local\\SystemMonitor-SingleInstance";
    private static Mutex? _mutex;

    public App()
    {
        // ========== 单实例检查（放在最前面，防止多开）==========
        // 注意：单实例是"锦上添花"，任何失败都必须降级为"照常启动"，绝不能阻止程序打开
        try
        {
            bool createdNew;
            _mutex = new Mutex(false, MutexName, out createdNew);

            if (!createdNew)
            {
                // Mutex 已存在 → 已有实例在运行
                AppLog.Info("检测到已有实例，弹窗提示后退出...");

                // 同步弹窗（MessageBox 是 Win32 弹窗，构造函数阶段即可显示）
                System.Windows.MessageBox.Show(
                    "SystemMonitor 已经在运行中。\n\n请检查系统托盘（右下角），或关闭旧实例后重新启动。",
                    "SystemMonitor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // 不持有 Mutex，关闭本实例（真正的退出，避免开第二个窗口）
                _mutex.Dispose();
                _mutex = null;
                Shutdown();
                return;
            }

            // 成功创建 Mutex
            AppLog.Info("单实例 Mutex 创建成功（本实例为主实例）");
        }
        catch (Exception ex)
        {
            // Mutex 失败（权限不足等）→ 降级：不做单实例检查，照常启动
            _mutex = null;
            AppLog.Warn("单实例 Mutex 不可用，已降级为允许多开（不影响主功能）", ex);
        }

        // ========== 全局异常收集 ==========
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        AppLog.Info("==== SystemMonitor 启动 ====");
        AppLog.Info(AppLog.EnvironmentSnapshot().Replace(Environment.NewLine, " | "));

        // 若用环境变量 SYSMON_TRACE=1 启动，这里提示 CSV 路径（也可用托盘菜单随时开关）
        if (AppLog.IsTracing && AppLog.TraceFilePath is { } tracePath)
            AppLog.Info("读数追踪已开启（SYSMON_TRACE）→ " + tracePath);
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
        try { _mutex?.ReleaseMutex(); _mutex?.Dispose(); } catch { }
        base.OnExit(e);
    }
}
