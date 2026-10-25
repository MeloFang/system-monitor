using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AsusMonitor.Core.Modes;

/// <summary>
/// 华硕 ATKACPI 驱动通信（官方 ASUS System Control Interface）。
/// 常量与缓冲区布局与开源项目 G-Helper（app/AsusACPI.cs）一致：
///   输入: [MethodID(4)][参数长度(4)][DeviceID(4)][值(4, 仅写入)]
///   输出: 16 字节，结果在偏移 0，需减去 0x10000 标志位
/// 只读用 DSTS，写入用 DEVS。
/// </summary>
public sealed class AsusAcpi : IDisposable
{
    public const uint DevicePerformanceMode = 0x00120075;

    private const uint IoctlAtkacpi = 0x0022240C;
    private const uint MethodDsts = 0x53545344; // "DSTS"
    private const uint MethodDevs = 0x53564544; // "DEVS"

    private readonly SafeFileHandle? _handle;

    public bool IsConnected => _handle is { IsInvalid: false };

    public AsusAcpi()
    {
        var h = CreateFile(
            @"\\.\ATKACPI",
            0xC0000000, // GENERIC_READ | GENERIC_WRITE
            0x00000003, // FILE_SHARE_READ | FILE_SHARE_WRITE
            IntPtr.Zero,
            3,          // OPEN_EXISTING
            0,
            IntPtr.Zero);
        _handle = h.IsInvalid ? null : h;
    }

    /// <summary>读设备状态；失败返回 null。</summary>
    public int? DeviceGet(uint deviceId)
    {
        if (!IsConnected) return null;

        byte[] input = new byte[12];
        BitConverter.GetBytes(MethodDsts).CopyTo(input, 0);
        BitConverter.GetBytes(4u).CopyTo(input, 4);
        BitConverter.GetBytes(deviceId).CopyTo(input, 8);
        byte[] output = new byte[16];

        bool ok = DeviceIoControl(_handle!, IoctlAtkacpi, input, input.Length, output, output.Length, out _, IntPtr.Zero);
        return ok ? BitConverter.ToInt32(output, 0) - 0x10000 : null;
    }

    /// <summary>写设备状态；返回是否成功。</summary>
    public bool DeviceSet(uint deviceId, uint value)
    {
        if (!IsConnected) return false;

        byte[] input = new byte[16];
        BitConverter.GetBytes(MethodDevs).CopyTo(input, 0);
        BitConverter.GetBytes(8u).CopyTo(input, 4);      // DeviceID + 值 两个参数
        BitConverter.GetBytes(deviceId).CopyTo(input, 8);
        BitConverter.GetBytes(value).CopyTo(input, 12);
        byte[] output = new byte[16];

        return DeviceIoControl(_handle!, IoctlAtkacpi, input, input.Length, output, output.Length, out _, IntPtr.Zero);
    }

    public void Dispose() => _handle?.Dispose();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        byte[] lpInBuffer, int nInBufferSize,
        byte[] lpOutBuffer, int nOutBufferSize,
        out int lpBytesReturned, IntPtr lpOverlapped);
}
