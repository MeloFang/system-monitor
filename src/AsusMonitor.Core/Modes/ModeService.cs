using System.ServiceProcess;

namespace AsusMonitor.Core.Modes;

/// <summary>
/// 性能模式读写。写操作是本软件唯一的硬件写入口（AGENTS.md 纪律 #2）。
/// Windows 档语义：ACPI 层写 Balanced，档位显示由调用方记忆（会话状态）。
/// </summary>
public sealed class ModeService : IDisposable
{
    private readonly AsusAcpi _acpi = new();

    public bool IsAcpiAvailable => _acpi.IsConnected;

    /// <summary>读当前档位；读失败或不识别返回 null。</summary>
    public PerformanceMode? GetCurrent()
    {
        int? raw = _acpi.DeviceGet(AsusAcpi.DevicePerformanceMode);
        return raw is null ? null : PerformanceModeMap.FromRaw(raw.Value);
    }

    /// <summary>切换档位；返回写入后读回的档位（校验），失败返回 null。</summary>
    public PerformanceMode? Set(PerformanceMode mode)
    {
        if (!_acpi.DeviceSet(AsusAcpi.DevicePerformanceMode, PerformanceModeMap.ToRaw(mode)))
            return null;
        Thread.Sleep(200); // 等固件生效再读回
        return GetCurrent();
    }

    /// <summary>Armoury Crate 服务是否正在运行（运行中切换模式会被它重置）。</summary>
    public static bool IsArmouryCrateRunning()
    {
        try
        {
            using var sc = new ServiceController("ArmouryCrateControlInterface");
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false; // 服务不存在 = AC 未装/已卸载
        }
    }

    public void Dispose() => _acpi.Dispose();
}
