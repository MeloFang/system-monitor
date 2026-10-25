# GLOSSARY.md — system-monitor 术语表

| 术语 | 含义 |
|:---|:---|
| **EC** | Embedded Controller，嵌入式控制器。主板上的单片机，风扇转速、温度等由它上报；读写需经华硕驱动 |
| **ACPI WMI** | 华硕官方驱动 ASUS System Control Interface（ATKACPI）暴露的 Windows 管理接口，性能模式读写的合法通道 |
| **NVML** | NVIDIA Management Library，NVIDIA 官方库，读 GPU 温度/频率/功耗（TGP） |
| **LHM** | LibreHardwareMonitor，开源硬件监控库（NuGet: LibreHardwareMonitorLib），读 CPU/风扇/主板传感器 |
| **WinRing0** | LHM 内置的内核驱动，用于读取 CPU 寄存器级传感器；可能被杀软误报 |
| **性能模式五档** | Windows（交还系统电源计划）/ 安静 Silent / 性能 Performance(Balanced) / 增强 Turbo / 手动 Manual。前四档是 BIOS 预设，写 WMI 即生效；手动档在本项目里只可切换、不可编辑参数 |
| **PL1 / PL2** | CPU 长时/短时功耗上限。本项目只读不写 |
| **TGP** | Total Graphics Power，GPU 总功耗（瓦），经 NVML 读取 |
| **AC** | Armoury Crate，华硕官方控制软件。本项目不修改它、不读它的内部数据 |
| **G-Helper** | 开源项目 seerge/g-helper，华硕 WMI 接口的权威参考实现（性能模式/风扇曲线等） |
| **探针** | 第一步要跑的控制台小程序：枚举本机所有可读传感器和 WMI 模式接口，输出清单，验证可行性 |
