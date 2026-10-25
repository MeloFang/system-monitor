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
        // 尝试打开已存在的 Mutex
        bool createdNew;
        _mutex = new Mutex(false, MutexName, out createdNew);
        
        if (!createdNew)
        {
            // Mutex 已存在 → 已有实例在运行
            AppLog.Info("检测到已有实例，弹窗提示...");
            
            // 延迟弹窗，确保 UI 初始化完成
            Dispatcher.BeginInvoke(new Action(() =>
            {
                System.Windows.MessageBox.Show(
                    "SystemMonitor 已经在运行中。\n\n请检查系统托盘（右下角），或关闭旧实例后重新启动。",
                    "SystemMonitor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }), System.Windows.Threading.DispatcherPriority.Background);
            
            // 不持有 Mutex，直接退出
            _mutex.Dispose();
            _mutex = null;
            return;
        }

        // 成功创建 Mutex → 拥有它
        _mutex.ReleaseMutex();

        // ========== 全局异常收集 ==========
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        AppLog.Info("==== SystemMonitor 启动 ====");
        AppLog.Info(AppLog.EnvironmentSnapshot().Replace(Environment.NewLine, " | "));
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
