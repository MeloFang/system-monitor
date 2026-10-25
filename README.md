# system-monitor

轻量 Windows 硬件监控面板，最初从华硕 Armoury Crate 的「系统使用率」面板拆分而来。现已不依赖任何华硕驱动，可在任意 Windows 电脑上运行（详见下方「非华硕电脑能用吗」）。纯只读，不写任何硬件。

## 功能

- CPU：温度、使用率、频率（显示完整型号名）
- GPU：温度、使用率、核心/显存频率（显示完整型号名）
- 内存：RAM 使用率、已用/总容量、频率、存储占用
- 1 秒刷新，暗色面板，最小化到托盘，可选开机自启

**不做**：性能模式切换、风扇显示与控制、灯效、GPU 输出模式、远程/小程序。

## 运行

**嫌编译麻烦？** 直接到 [Releases](https://github.com/MeloFang/system-monitor/releases) 下载自包含版 `SystemMonitor.exe`（已内嵌 .NET 运行时，无需安装 .NET，双击即用）。想自行构建则往下看。

双击 `SystemMonitor.exe` 即可，无需安装。

- **首次会弹 UAC，必须点"是"**：温度等传感器读取需要管理员权限（LibreHardwareMonitor 的内核驱动）
- 自行构建默认是**框架依赖版**，需要 **.NET 8 桌面运行时**（Release 的自包含版则不需要）
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

**杀软报毒/拦截**：LibreHardwareMonitor 的 WinRing0 驱动常被误报，把 `SystemMonitor.exe` 加入白名单即可。

**某几项显示 `--`**：该传感器在本机型未被固件暴露，属正常降级，其余项不受影响。

**非华硕电脑能用吗**：能。本项目所有数据均来自 LibreHardwareMonitor / NVIDIA NVML / Windows WMI / .NET DriveInfo，与华硕驱动（ATKACPI）无关；当初拆自 Armoury Crate 的风扇读数与性能模式切换已被移除，因此不再绑定华硕硬件。仅当机器没有 NVIDIA 独显时，GPU 功耗一栏留空（其余 GPU 指标仍由 LibreHardwareMonitor 提供）。

**只有集显/核显的机器能用吗**：能跑，CPU/内存/存储都正常。GPU 一栏取决于集显品牌——LibreHardwareMonitor 只支持 NVIDIA（NVAPI）与 AMD（ADL）：AMD APU 的集显有可能被识别并显示部分指标；而 **Intel 核显（UHD/Iris）不被支持，GPU 一栏会显示 `--`、型号也显示 `--`**，属正常降级，不影响其他卡片。

**温度读不到**：确认已用管理员权限运行。

## 开发与构建

```bash
dotnet build src/SystemMonitor.sln
dotnet test
dotnet publish src/SystemMonitor -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

发布前先结束正在运行的 `SystemMonitor.exe`，否则单文件发布因文件占用失败。

项目文档：`PRODUCT.md`（范围）、`PLAN.md`（进度）、`AGENTS.md`（开发纪律）、`HANDOFF.md`（续接说明）。
