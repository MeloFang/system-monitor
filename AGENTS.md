# AGENTS.md — system-monitor

> 项目上下文单一来源。新会话/新 Agent 先读本文件 + PRODUCT.md + GLOSSARY.md + HANDOFF.md 即可开工。

## 项目结构（现状）

```
system-monitor/
├── src/
│   ├── SystemMonitor/                 # WPF 主程序（导航壳）
│   │   ├── Views/                     # HomePage / MonitorView / EngineView
│   │   ├── Services/                  # EngineControlService / MetricsService
│   │   ├── Controls/                  # Sparkline / RingGauge（自绘）
│   │   ├── Hub.cs                     # 进程内服务枢纽 + 60s 历史 + Tick/Toast/Navigate
│   │   ├── MainWindow.xaml(.cs)       # 导航壳 + 顶栏 + toast + 托盘/自启/诊断
│   │   ├── GlobalUsings.cs            # WPF/WinForms 重名类型别名（勿删）
│   │   └── app.manifest               # requireAdministrator
│   ├── SystemMonitor.Core/
│   │   ├── Sensors/                   # SensorService（LHM+NVML，含设备级显存）/ SensorSnapshot
│   │   ├── Engines/                   # 纯逻辑：StrataLogParser/OllamaJson/CallHelpProvider/VramGate/EngineState
│   │   └── Diagnostics/AppLog.cs
│   ├── SystemMonitor.Probe/           # 控制台探针（历史可行性闸门）
│   └── SystemMonitor.Tests/           # xUnit，只跑纯逻辑（net8.0）
├── dist-release/SystemMonitor.exe     # 单文件发布产物
├── PRODUCT.md / PLAN.md / GLOSSARY.md / HANDOFF.md
```

## 构建与运行

```bash
dotnet build src/SystemMonitor.sln -c Release
dotnet test src/SystemMonitor.Tests -c Release
dotnet publish src/SystemMonitor -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
# 真机启动（隐藏控制台父进程会继承最小化→缩托盘，必须 Normal）：
powershell Start-Process -WindowStyle Normal <exe>
```

## 技术栈与关键依赖

- .NET 8 / C# 12 / WPF + WinForms（托盘）
- LibreHardwareMonitorLib；NVML 直接 P/Invoke（nvml.dll）
- 引擎：ollama.exe（:11434）/ strata.cmd（:8080）官方 CLI + HTTP API
- 权限：requireAdministrator；单实例 `Local\SystemMonitor-SingleInstance`

## 硬性纪律

1. **只走官方接口**：NVML + LHM + 引擎官方 CLI/HTTP。禁止逆向/读写 Armoury Crate、引擎进程内存。
2. **写操作只有一处**：EngineControlService 的引擎进程起停。不写硬件（无风扇/功耗/模式写入）。
3. **采集只读 + 重入锁**：LHM/NVML 非线程安全，Collect 并发会 native AV；MainWindow._collecting 锁勿删。
4. **清代理**：起引擎子进程前移除 HTTP(S)_PROXY/ALL_PROXY（含小写）。
5. **NVML 已修口径别回退**：nvmlClockType GRAPHICS=0/SM=1/MEM=2/VIDEO=3；<100MHz 判休眠噪声；负载<5% 判空闲。
6. **降级**：任何读数读不到 → null → 界面 "--"；引擎起失败明确报错不崩；硬件不可用不影响引擎控制。
7. **Strata 启动参数**：`launch --family X --model Y --data-dir E:\Strata-Models --no-tui --yes --port 8080`；停止 `launch --stop`。裸 gguf --model-path 会被拒。
8. 测试纪律：红在前；CI 只跑纯逻辑；引擎起停真机测。
9. 提交信息中文，先写 `.commit_msg.txt` 再 `git commit -F`（主人环境约定）。

## UI 真机实测要点

- PowerShell 5.1 读无 BOM 的 .ps1 按 ANSI → 中文脚本必须加 UTF-8 BOM。
- UIAutomation 找窗口要挑 ClassName=="Window"（同进程还有输入法顶层窗口）。
- 分段按钮（Ollama/Strata）UIA Name 为空，按按钮索引 3/4 调用；其余按钮有中文名。
- 截屏用 System.Drawing CopyFromScreen；抢前台用 SetWindowPos TOPMOST 临时置顶。

## 参考实现（读源码用，勿抄许可证不兼容的代码）

- LibreHardwareMonitor：https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
- 视觉原型：WorkBuddy `2026-10-05-18-56-27/推理面板-UI示意.html` + `推理控制面板方案.md` §十一
