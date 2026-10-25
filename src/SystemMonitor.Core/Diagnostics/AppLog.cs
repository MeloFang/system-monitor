using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using SystemMonitor.Core.Sensors;

namespace SystemMonitor.Core.Diagnostics;

/// <summary>
/// 轻量异常/诊断收集器。目标：让"无反应/静默崩溃"变成"有日志、有诊断报告"。
/// - 运行日志：%LOCALAPPDATA%\SystemMonitor\logs\systemmonitor-YYYYMMDD.log
/// - 崩溃报告：同目录 crash-YYYYMMDD-HHMMSS.log（含环境快照 + 完整异常）
/// - 诊断报告：写到桌面，供朋友一键发回给开发者
/// 任何日志写入失败都被吞掉，绝不拖垮主程序。
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static string? _dir;
    private static readonly List<string> Recent = new();
    private const int RecentCapacity = 300;

    public static string LogDirectory
    {
        get
        {
            if (_dir is null)
            {
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                _dir = Path.Combine(baseDir, "SystemMonitor", "logs");
                try { Directory.CreateDirectory(_dir); }
                catch
                {
                    _dir = Path.Combine(Path.GetTempPath(), "SystemMonitor-logs");
                    try { Directory.CreateDirectory(_dir); } catch { }
                }
            }
            return _dir;
        }
    }

    private static string LogFile => Path.Combine(LogDirectory, $"systemmonitor-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);
    public static void Error(string message, Exception? ex) => Write("ERROR", message, ex);

    // ---------- 读数追踪（跑 3DMark / 压力测试时抓整条曲线）----------

    /// <summary>
    /// 详细读数追踪：先设环境变量 SYSMON_TRACE=1 再启动本程序即开启。
    /// 每次采集（约 1 秒）向 readings-*.csv 追加一行，跑完压力测试可直接用 Excel 看曲线。
    /// 未开启时所有调用立即返回，零开销。
    /// </summary>
    // 启动即追踪：可设环境变量 SYSMON_TRACE=1；也可运行时用托盘菜单 StartTrace() 开启
    private static bool _traceEnabled =
        (Environment.GetEnvironmentVariable("SYSMON_TRACE") ?? "").Trim().ToLowerInvariant() is "1" or "true" or "yes";

    private static string? _traceFile;

    /// <summary>是否正在追踪。</summary>
    public static bool IsTracing => _traceEnabled && _traceFile is not null;

    /// <summary>当前追踪文件路径；未开启时为 null。</summary>
    public static string? TraceFilePath => IsTracing ? _traceFile : null;

    /// <summary>开始追踪，返回 CSV 文件路径；失败返回 null。</summary>
    public static string? StartTrace()
    {
        try
        {
            _traceFile = Path.Combine(LogDirectory, $"readings-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            _traceEnabled = true;
            Info("读数追踪已开启 → " + _traceFile);
            return _traceFile;
        }
        catch { return null; }
    }

    /// <summary>停止追踪，返回结束前写入的文件路径。</summary>
    public static string? StopTrace()
    {
        var last = _traceFile;
        _traceEnabled = false;
        _traceFile = null;
        Info("读数追踪已停止（文件：" + last + "）");
        return last;
    }

    private const string CsvHeader =
        "Time,CpuClockMHz,CpuLoadPct,CpuTempC,GpuCoreMHz,GpuMemMHz,GpuLoadPct,GpuTempC,GpuPowerW,MemLoadPct,MemUsedGB,StoragePct";

    /// <summary>把一次采集快照写入 CSV。追踪未开启时直接返回。</summary>
    public static void AppendReading(SensorSnapshot snap)
    {
        if (!IsTracing) return;
        var file = _traceFile;
        if (file is null) return;

        try
        {
            var line = string.Join(",", new[]
            {
                DateTime.Now.ToString("HH:mm:ss"),
                Num(snap.CpuClock), Num(snap.CpuLoad), Num(snap.CpuTemp),
                Num(snap.GpuClock), Num(snap.GpuMemClock), Num(snap.GpuLoad),
                Num(snap.GpuTemp), Num(snap.GpuPower),
                Num(snap.MemLoad), Num(snap.MemUsed), Num(snap.StorageLoad)
            });

            lock (Gate)
            {
                bool needHeader = !File.Exists(file);
                using var sw = new StreamWriter(file, append: true, Encoding.UTF8);
                if (needHeader) sw.WriteLine(CsvHeader);
                sw.WriteLine(line);
            }
        }
        catch { /* 追踪失败绝不影响主功能 */ }
    }

    /// <summary>数值按不变文化格式化（小数点为 .，方便 Excel 打开）；null 写空。</summary>
    private static string Num(double? v)
        => v is null ? "" : v.Value.ToString("0.##", CultureInfo.InvariantCulture);

    private static void Write(string level, string message, Exception? ex)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ");
        sb.Append(level).Append(' ').Append(message);
        if (ex is not null)
        {
            sb.Append(" | ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
            var inner = ex.InnerException;
            int depth = 0;
            while (inner is not null && depth++ < 6)
            {
                sb.Append(" << ").Append(inner.GetType().Name).Append(": ").Append(inner.Message);
                inner = inner.InnerException;
            }
            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                sb.Append('\n').Append(ex.StackTrace);
        }
        var line = sb.ToString();
        lock (Gate)
        {
            try { File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8); }
            catch { /* 日志写不出也不能影响主程序 */ }
            Recent.Add(line);
            if (Recent.Count > RecentCapacity) Recent.RemoveAt(0);
        }
    }

    /// <summary>启动环境快照，用于远程诊断（朋友机器为啥起不来）。</summary>
    public static string EnvironmentSnapshot()
    {
        string admin = "未知";
        try
        {
            using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            var p = new System.Security.Principal.WindowsPrincipal(id);
            admin = p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator) ? "是" : "否";
        }
        catch { }

        // 版本从程序集读（csproj 的 <Version>），避免日志里写死旧版本号
        string ver = "未知";
        try
        {
            var asmVer = typeof(AppLog).Assembly.GetName().Version;
            if (asmVer is not null) ver = $"{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}";
        }
        catch { }

        var sb = new StringBuilder();
        sb.AppendLine($"==== SystemMonitor 诊断快照 v{ver} ====");
        sb.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("OS: " + RuntimeInformation.OSDescription);
        sb.AppendLine("OS版本(Environment.OSVersion): " + Environment.OSVersion.Version);
        sb.AppendLine("64位进程: " + Environment.Is64BitProcess);
        sb.AppendLine(".NET: " + RuntimeInformation.FrameworkDescription);
        sb.AppendLine("管理员权限: " + admin);
        sb.AppendLine("机器名: " + Environment.MachineName);
        sb.AppendLine("日志目录: " + LogDirectory);
        return sb.ToString();
    }

    /// <summary>崩溃时写独立 crash-*.log，返回路径。</summary>
    public static string? WriteCrash(Exception ex)
    {
        try
        {
            var path = Path.Combine(LogDirectory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            var sb = new StringBuilder();
            sb.Append(EnvironmentSnapshot());
            sb.AppendLine().AppendLine("==== 未处理异常 ====").AppendLine(ex.ToString());
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }
        catch { return null; }
    }

    /// <summary>导出完整诊断报告（环境 + 传感器状态 + 最近日志）到桌面，返回路径。</summary>
    public static string? WriteDiagnosticReport(string? sensorStatus)
    {
        try
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(dir)) dir = Path.GetTempPath();
            var path = Path.Combine(dir, $"SystemMonitor-诊断报告-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var sb = new StringBuilder();
            sb.Append(EnvironmentSnapshot());
            if (!string.IsNullOrEmpty(sensorStatus))
                sb.AppendLine().AppendLine("==== 传感器/驱动状态 ====").AppendLine(sensorStatus);
            sb.AppendLine().AppendLine("==== 最近日志 ====");
            lock (Gate) { foreach (var l in Recent) sb.AppendLine(l); }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }
        catch { return null; }
    }
}
