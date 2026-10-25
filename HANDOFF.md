# HANDOFF.md — system-monitor 续接文档

> 任何人/任何会话接手本项目，先读这份 + PRODUCT.md。每完成一个阶段必须更新本文。

## 这程序是什么

轻量 Windows 桌面软件，**纯监控**（2026-10-25 v3 起）：CPU/GPU/内存实时状态，1s 刷新，只读。

**明确不做**：性能模式切换（操作模式整套删除，代码在 git 历史里）、风扇（显示与控制都不要）、风扇曲线编辑、功耗编辑、灯效、GPU 模式切换、小程序。

## 技术路线（已定，勿推翻）

- C# / .NET 8 + WPF
- 采集：LibreHardwareMonitorLib + NVML
- 模式切换：华硕 ACPI WMI（官方驱动接口），参考 G-Helper 实现
- 全程不逆向、不修改 Armoury Crate；管理员权限运行

## 目标机型

ROG Strix（Ryzen 9 5900HX + RTX 3080 Laptop，2021 款）。先做这一台，不承诺通用兼容。

## 做到哪一步

- 2026-10-25：立项完成，PRODUCT.md 主人已确认。
- 2026-10-25：PLAN 步骤 0-4 完成。探针实测通过（G533QS：CPU/GPU/风扇/模式/TGP 全部可读；风扇原始值×100=RPM）。Core（采集+模式读写）+ WPF 面板 + 7 项单测全绿。
- 2026-10-25：二轮反馈「还是丑」+ 指截图删「操作模式」（主人称之为风扇调整）→ v3：改纯监控，Modes 目录删除，UI 参考 NZXT CAM/iCUE 改圆环仪表盘（StrokeDashArray + 自定义转换器）。
- **待真机验证**（主人做）：与 AC 并排对比数值。
- 2026-10-25 收尾：托盘（NotifyIcon + app.ico）、开机自启（HKCU Run 键菜单开关）、README、30 分钟稳定性长跑采样。界面经主人确认 OK。
- 下一步（可选）：把仓库推 GitHub（需主人先在 github.com/new 建仓库）；换机型适配（WMI 接口差异）。

## 实测要点（后续会话直接用）

- 机型实际为 **ASUS G533QS**（ROG Strix Scar 15 2021，5900HX + RTX 3080 Laptop）
- ATKACPI IOCTL = **0x0022240C**（不是 0x222810）；输入 [MethodID][参数长度][DeviceID]，输出 16B、偏移 0、减 0x10000
- 性能模式设备 ID 0x00120075；风扇 CPU 0x00110013 / GPU 0x00110014
- 风扇原始值 = RPM/100；NVML 读 TGP 正常；LHM 不覆盖本机风扇和 GPU 功耗
- dotnet 在 `C:\Program Files\dotnet\dotnet.exe`（bash 里需 export PATH）

## 怎么接手

1. 读 PRODUCT.md（做什么）、GLOSSARY.md（术语）、AGENTS.md（纪律与命令）、PLAN.md（步骤与当前状态）
2. 按 PLAN.md 当前未完成步骤继续，每步完成后更新 PLAN.md 状态 + 本文
3. 性能模式的 WMI 调用细节看 G-Helper 源码 `app/AsusACPI.cs` 和 `app/Mode/ModeControl.cs`（https://github.com/seerge/g-helper）

## 关键注意事项

- 写操作只有模式切换一处；EC 读取间隔 ≥500ms
- 切换模式前检测 Armoury Crate 服务，运行中会冲突
- 探针是第一步也是可行性闸门：风扇 RPM 读不到就要调整 PRODUCT.md 成功标准
