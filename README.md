# asus-monitor

轻量 Windows 硬件监控面板，替代 Armoury Crate 的「系统使用率」面板。纯只读，不写任何硬件。

## 功能

- CPU：温度、使用率、频率（显示完整型号名）
- GPU：温度、使用率、核心/显存频率（显示完整型号名）
- 内存：RAM 使用率、已用/总容量、频率、存储占用
- 1 秒刷新，暗色面板，最小化到托盘，可选开机自启

**不做**：性能模式切换、风扇显示与控制、灯效、GPU 输出模式、远程/小程序。

## 运行

双击 `AsusMonitor.exe` 即可，无需安装。

- **首次会弹 UAC，必须点"是"**：温度等传感器读取需要管理员权限（LibreHardwareMonitor 的内核驱动）
- 需要 **.NET 8 运行时**（本机已装）
- 点右上角 `—` 最小化到托盘；点 `✕` 也是缩到托盘（继续后台采集），要彻底退出用托盘菜单的"退出"

## 托盘菜单

- 显示面板 —— 从托盘恢复窗口
- 开机自启 —— 勾选后写入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- 退出 —— 真正结束进程

## 数据来源

| 数据 | 来源 |
|:---|:---|
| CPU 温度/频率/负载/电压 | LibreHardwareMonitor |
| GPU 温度/频率/负载 | LibreHardwareMonitor（NVIDIA 卡经 NVAPI） |
| GPU 功耗 TGP | NVIDIA NVML |
| 内存已用/总量/频率 | WMI（Win32_PhysicalMemory 容量之和）+ LibreHardwareMonitor |
| 存储占用 | DriveInfo（C 盘） |

全程走官方接口与开源库，**不读取、不修改 Armoury Crate 的任何进程或文件**。

## 与 Armoury Crate 的关系

- 只读不写，两者可以同时运行，不会抢控制权
- 若你不想要 AC 的后台服务，可以用华硕官方卸载工具清理（保留系统控制接口驱动即可）

## 常见问题

**杀软报毒/拦截**：LibreHardwareMonitor 的 WinRing0 驱动常被误报，把 `AsusMonitor.exe` 加入白名单即可。

**某几项显示 `--`**：该传感器在本机型未被固件暴露，属正常降级，其余项不受影响。

**温度读不到**：确认已用管理员权限运行。

## 开发与构建

```bash
dotnet build src/AsusMonitor.sln
dotnet test
dotnet publish src/AsusMonitor -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

发布前先结束正在运行的 `AsusMonitor.exe`，否则单文件发布因文件占用失败。

项目文档：`PRODUCT.md`（范围）、`PLAN.md`（进度）、`AGENTS.md`（开发纪律）、`HANDOFF.md`（续接说明）。
