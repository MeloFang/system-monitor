// SystemMonitor.Probe —— 可行性探针
// 职责：枚举本机可读传感器（LibreHardwareMonitor）+ 经华硕 ATKACPI 驱动读当前性能模式。
// 只读，不写任何硬件。需要管理员权限运行。

using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using LibreHardwareMonitor.Hardware;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("=== asus-monitor 硬件探针 ===");
Console.WriteLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
Console.WriteLine($"OS:   {Environment.OSVersion}");
Console.WriteLine($"机器: {Environment.MachineName}");
Console.WriteLine();

// ---------- 0. 权限检查 ----------
bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
    .IsInRole(WindowsBuiltInRole.Administrator);
Console.WriteLine($"管理员权限: {(isAdmin ? "是" : "否（传感器/EC 读取可能失败，请用管理员权限重跑）")}");
Console.WriteLine();

// ---------- 1. 华硕接口：性能模式 + 风扇转速（ATKACPI / ACPI WMI）----------
ProbeAsusAcpi();

// ---------- 2. NVML：GPU 功耗（TGP）----------
ProbeNvmlPower();

// ---------- 2. LibreHardwareMonitor 传感器枚举 ----------
Console.WriteLine();
Console.WriteLine("----- LibreHardwareMonitor 传感器清单 -----");

var computer = new Computer
{
    IsCpuEnabled = true,
    IsGpuEnabled = true,
    IsMotherboardEnabled = true,
    IsMemoryEnabled = true,
    IsStorageEnabled = true,
    IsControllerEnabled = true,
    IsPsuEnabled = false,
    IsBatteryEnabled = true,
    IsNetworkEnabled = false,
};

try
{
    computer.Open();
}
catch (Exception ex)
{
    Console.WriteLine($"[LHM] 打开失败: {ex.Message}");
    return 1;
}

try
{
    for (int round = 1; round <= 10; round++)
    {
        Console.WriteLine();
        Console.WriteLine($"===== 第 {round}/10 轮  {DateTime.Now:HH:mm:ss} =====");
        foreach (IHardware hw in computer.Hardware)
            PrintHardware(hw, 0);

        if (round < 10)
            Thread.Sleep(1000);
    }
}
finally
{
    computer.Close();
}

Console.WriteLine();
Console.WriteLine("探针结束。");
return 0;

// ---------- 实现 ----------

static void PrintHardware(IHardware hw, int indent)
{
    try
    {
        hw.Update();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{new string(' ', indent)}[{hw.HardwareType}] {hw.Name}  <更新失败: {ex.Message}>");
        return;
    }

    string pad = new string(' ', indent);
    Console.WriteLine($"{pad}[{hw.HardwareType}] {hw.Name}");

    foreach (ISensor sensor in hw.Sensors)
    {
        if (sensor.Value is null) continue;
        // 只打印监控面板关心的类型，降噪
        switch (sensor.SensorType)
        {
            case SensorType.Temperature:
            case SensorType.Load:
            case SensorType.Clock:
            case SensorType.Power:
            case SensorType.Fan:
            case SensorType.Voltage:
            case SensorType.Data:
            case SensorType.SmallData:
            case SensorType.Level:
                Console.WriteLine($"{pad}    {sensor.SensorType,-12} {sensor.Name,-40} = {sensor.Value}");
                break;
        }
    }

    foreach (IHardware sub in hw.SubHardware)
        PrintHardware(sub, indent + 2);
}

static void ProbeAsusAcpi()
{
    Console.WriteLine("----- 华硕接口（ATKACPI）-----");

    // 与 G-Helper（app/AsusACPI.cs）一致的常量：
    //   IOCTL = 0x0022240C；DSTS = 读设备状态
    //   输入缓冲: [MethodID(4)][参数长度(4)][DeviceID(4)]
    //   输出缓冲: 16 字节，返回值在偏移 0，需减去 0x10000 标志位
    const uint DEVID_PERFORMANCE_MODE = 0x00120075;
    const uint DEVID_CPU_FAN = 0x00110013;
    const uint DEVID_GPU_FAN = 0x00110014;

    using var handle = OpenAtkAcpi();
    if (handle is null || handle.IsInvalid)
    {
        Console.WriteLine("[ATKACPI] 打开设备失败。华硕系统控制接口驱动可能未安装。");
        return;
    }

    // 性能模式
    int? mode = DeviceGet(handle, DEVID_PERFORMANCE_MODE);
    if (mode is null)
    {
        Console.WriteLine("[ATKACPI] 性能模式读取失败。");
    }
    else
    {
        string modeName = mode.Value switch
        {
            0 => "性能 (Balanced)",
            1 => "增强 (Turbo)",
            2 => "安静 (Silent)",
            3 => "全速 (FullSpeed)",
            4 => "手动 (Manual)",
            _ => "未知",
        };
        Console.WriteLine($"[ATKACPI] 性能模式 = {mode}  →  {modeName}");
    }

    // 风扇转速（返回值单位随机型：多数为 RPM，部分机型为 RPM/100，两种解读都打印）
    foreach (var (name, devId) in new (string, uint)[] { ("CPU 风扇", DEVID_CPU_FAN), ("GPU 风扇", DEVID_GPU_FAN) })
    {
        int? fan = DeviceGet(handle, devId);
        if (fan is null)
            Console.WriteLine($"[ATKACPI] {name}读取失败。");
        else
            Console.WriteLine($"[ATKACPI] {name}原始值 = {fan}  →  解读A: {fan} RPM / 解读B: {fan * 100} RPM");
    }
}

static Microsoft.Win32.SafeHandles.SafeFileHandle? OpenAtkAcpi()
{
    var handle = CreateFile(
        @"\\.\ATKACPI",
        0xC0000000, // GENERIC_READ | GENERIC_WRITE
        0x00000003, // FILE_SHARE_READ | FILE_SHARE_WRITE
        IntPtr.Zero,
        3,          // OPEN_EXISTING
        0,
        IntPtr.Zero);
    return handle.IsInvalid ? null : handle;
}

static int? DeviceGet(Microsoft.Win32.SafeHandles.SafeFileHandle handle, uint deviceId)
{
    const uint IOCTL_ATKACPI = 0x0022240C;
    const uint DSTS = 0x53545344; // "DSTS"

    byte[] input = new byte[12];
    BitConverter.GetBytes(DSTS).CopyTo(input, 0);
    BitConverter.GetBytes(4u).CopyTo(input, 4); // 参数长度 = 4
    BitConverter.GetBytes(deviceId).CopyTo(input, 8);
    byte[] output = new byte[16];

    bool ok = DeviceIoControl(handle, IOCTL_ATKACPI, input, input.Length, output, output.Length, out _, IntPtr.Zero);
    if (!ok) return null;
    return BitConverter.ToInt32(output, 0) - 0x10000;
}

static void ProbeNvmlPower()
{
    Console.WriteLine();
    Console.WriteLine("----- NVML：GPU 功耗 -----");
    try
    {
        int rc = NvmlInit();
        if (rc != 0) { Console.WriteLine($"[NVML] 初始化失败 rc={rc}（nvml.dll 未找到？）"); return; }
        try
        {
            rc = NvmlGetHandle(0, out IntPtr dev);
            if (rc != 0) { Console.WriteLine($"[NVML] 取设备句柄失败 rc={rc}"); return; }
            rc = NvmlGetPower(dev, out uint milliwatts);
            Console.WriteLine(rc == 0
                ? $"[NVML] GPU 功耗 = {milliwatts / 1000.0:F1} W"
                : $"[NVML] 读功耗失败 rc={rc}");
        }
        finally { NvmlShutdown(); }
    }
    catch (DllNotFoundException)
    {
        Console.WriteLine("[NVML] nvml.dll 未找到。");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[NVML] 异常: {ex.Message}");
    }
}

[DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
static extern int NvmlInit();
[DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
static extern int NvmlGetHandle(uint index, out IntPtr device);
[DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetPowerUsage")]
static extern int NvmlGetPower(IntPtr device, out uint powerMilliwatts);
[DllImport("nvml.dll", EntryPoint = "nvmlShutdown")]
static extern int NvmlShutdown();

[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(
    string lpFileName, uint dwDesiredAccess, uint dwShareMode,
    IntPtr lpSecurityAttributes, uint dwCreationDisposition,
    uint dwFlagsAndAttributes, IntPtr hTemplateFile);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool DeviceIoControl(
    Microsoft.Win32.SafeHandles.SafeFileHandle hDevice, uint dwIoControlCode,
    byte[] lpInBuffer, int nInBufferSize,
    byte[] lpOutBuffer, int nOutBufferSize,
    out int lpBytesReturned, IntPtr lpOverlapped);
