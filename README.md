# system-monitor

Windows 桌面**硬件监控 + 本地模型引擎控制**枢纽。WPF / .NET 8 单文件 exe，硬件只读采集，引擎走官方 CLI 与 HTTP 接口。

> 当前版本 **v1.1.0 · 推理控制枢纽**：从「纯监控面板」扩展为三视图桌面枢纽（首页 / 硬件监控 / 模型引擎）。

## 功能

### 首页
- 两个入口卡片：**硬件监控** / **模型引擎**
- 实时摘要：CPU / GPU 温度、GPU 负载、显存占用、当前引擎与模型、生成速度、完整端点

### 硬件监控
- **CPU**：温度、使用率、各核平均频率、电压、完整型号名
- **GPU**：温度、使用率、核心 / 显存频率、功耗 TGP、**设备级显存 used / total**、完整型号名
- **内存**：占用率、已用 / 总量、频率、可用内存
- 1 秒采集，圆环仪表 + CPU/GPU 并排曲线；读数缺失显示 `--`，不影响其他项

### 模型引擎（Ollama / Strata 合一）
- **启停与切换**：分段启动 / 停止，互斥切换（起一个前先停另一个，释放显存），停止后可撤销
- **模型管理**：Ollama 列出本地已拉取模型，点选即加载；Strata 从落地目录（默认 `E:\Strata-Models`）选 family + quant 后 `launch`
- **实时指标**：生成速度 tok/s（最近 60s 曲线）、输入速度、缓存命中、上下文占用（Strata 解析 engine.log；Ollama 取请求 done 对象，部分指标尽力而为）
- **调用助手**：当前引擎端点、opencode / Claude Code 配置、curl / Python 示例，一键复制
- 显存占用按**设备级**口径显示（含全部进程与桌面占用），切换引擎时以「基线 + 500MB / 10s」作为释放判据

**不做**：模型拉取（新增模型仍走 `ollama pull` / hf-mirror + `ollama create`）、风扇显示与控制、性能模式切换、灯效、GPU 输出模式、小程序。

## 运行

**不想编译？** 直接下载最新版 **v1.1.0 · 推理控制枢纽**：

- 发布页（含更新说明）：https://github.com/MeloFang/system-monitor/releases/tag/v1.1.0
- 直链下载：https://github.com/MeloFang/system-monitor/releases/download/v1.1.0/SystemMonitor.exe
- 以后有新版看这里：https://github.com/MeloFang/system-monitor/releases/latest

自包含单文件（约 65.5 MB），已内嵌 .NET 运行时，无需安装 .NET，双击即用。想自行构建则往下看。

双击 `SystemMonitor.exe` 即可，无需安装。

- **系统要求**：Windows 10 / 11 x64
- **首次会弹 UAC，建议点"是"**：温度等传感器读取需要管理员权限（LibreHardwareMonitor 的内核驱动）；不授权也能启动，温度类读数显示 `--`，引擎控制与 NVML 指标不受影响
- **引擎控制前提**：本机已安装 Ollama（`:11434`）或 Strata（`:8080`），否则对应引擎显示"未安装 / 起不来"
- 自行构建默认是**框架依赖版**，需要 **.NET 8 桌面运行时**（Release 的自包含版则不需要）
- 点右上角 `—` 最小化到托盘；点 `✕` 也是缩到托盘（继续后台采集），要彻底退出用托盘菜单的"退出"
- 默认窗口是 420×680 紧凑悬浮窗，托盘菜单可切换"悬浮置顶"

## 托盘菜单

- **显示面板** —— 从托盘恢复窗口
- **悬浮置顶** —— 窗口保持在其他程序之上
- **开机自启** —— 勾选后写入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- **记录读数到 CSV（压力测试用）** —— 每秒一行，方便画曲线复盘
- **导出诊断报告** —— 汇总环境、初始化状态与日志，便于反馈问题
- **打开日志目录**
- **退出** —— 真正结束进程

## 数据来源

| 数据 | 来源 |
|:---|:---|
| CPU 温度 / 频率 / 负载 / 电压 | LibreHardwareMonitor |
| GPU 温度 / 频率 / 负载 | LibreHardwareMonitor（NVIDIA 卡经 NVAPI） |
| GPU 功耗 TGP | NVIDIA NVML |
| GPU 显存 used / total | NVIDIA NVML `nvmlDeviceGetMemoryInfo`（设备级，含全部进程） |
| 内存已用 / 总量 / 频率 | WMI（`Win32_PhysicalMemory` 容量之和）+ LibreHardwareMonitor |
| 可用内存 | 总量 − 已用（LHM 口径） |
| 引擎状态 / tok/s / 模型列表 | Ollama HTTP API、Strata CLI 与 `engine.log` 解析 |
| 引擎进程 | 官方 CLI 子进程（`ollama serve` / `strata launch`） |

全程走官方接口与开源库，**不读取、不修改 Armoury Crate 的任何进程或文件**；硬件侧只读，唯一的写操作是引擎进程起停。

## 与 Armoury Crate 的关系

- 硬件只读不写，两者可以同时运行，不会抢控制权
- 本项目不提供性能模式 / 风扇控制，因此不存在与 AC 抢设置的问题
- 若你不想要 AC 的后台服务，可以用华硕官方卸载工具清理（保留系统控制接口驱动即可）

## 常见问题

**杀软报毒/拦截**：LibreHardwareMonitor 的 WinRing0 驱动常被误报，把 `SystemMonitor.exe` 加入白名单即可。

**某几项显示 `--`**：该传感器在本机型未被固件暴露，或未以管理员运行，属正常降级，其余项不受影响。

**非华硕电脑能用吗**：能。数据全部来自 LibreHardwareMonitor / NVIDIA NVML / Windows WMI，与华硕驱动（ATKACPI）无关。

**只有集显/核显的机器能用吗**：能跑，CPU / 内存正常。GPU 一栏取决于品牌——NVIDIA（NVAPI）与 AMD（ADL）可读；**Intel 核显（UHD/Iris）不被支持，GPU 显示 `--`**，不影响其他模块。没有 NVIDIA 卡时，功耗与显存两项为空。

**温度读不到**：确认已用管理员权限运行（右键 → 以管理员身份运行）。

**引擎起不来**：先确认端口 8080 / 11434 没被其他实例占用（另一个会话可能正在跑引擎）；Strata 必须用 `--family/--model/--data-dir` 启动，裸 gguf `--model-path` 会被拒；程序启动子进程前已清空代理环境变量。

**显存数字和任务管理器对不上**：面板显示的是**设备级**显存（所有进程 + 桌面占用），不是单个模型的占用，属预期差异。

**tok/s 偶尔为空**：Ollama 没有原生 live metrics，仅在加载请求的 done 对象里能取到；Strata 从 `engine.log` 解析，稳定样本阈值 ≥32 token 才采纳。

## 开发与构建

```bash
dotnet build src/SystemMonitor.sln -c Release
dotnet test src/SystemMonitor.Tests -c Release
dotnet publish src/SystemMonitor -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

发布前先结束正在运行的 `SystemMonitor.exe`，否则单文件发布因文件占用失败；被系统句柄锁住的旧 `release*/` 目录不要覆盖，输出到新目录。

单元测试当前 10/10（纯逻辑：格式化、日志解析、模型列表解析、显存判据）。

项目文档：`PRODUCT.md`（范围）、`PLAN.md`（进度）、`AGENTS.md`（开发纪律）、`HANDOFF.md`（续接说明）。
