# AGENTS.md — asus-monitor

> 项目上下文单一来源。新会话/新 Agent 先读本文件 + PRODUCT.md + GLOSSARY.md + HANDOFF.md 即可开工。

## 项目结构（规划）

```
asus-monitor/
├── src/
│   ├── AsusMonitor/              # WPF 主程序
│   │   ├── Sensors/              # 采集层：LHM + NVML 封装，1s 轮询
│   │   ├── Modes/                # 模式控制层：ACPI WMI 五档读写
│   │   ├── Ui/                   # 主面板（暗色，仿 AC 布局）
│   │   └── app.manifest          # requireAdministrator
│   ├── AsusMonitor.Probe/        # 控制台探针：枚举传感器与 WMI 接口（第一步先做这个）
│   └── AsusMonitor.Tests/        # 单元测试
├── docs/
│   └── adr/                      # 架构决策记录
├── PRODUCT.md / PLAN.md / GLOSSARY.md / HANDOFF.md / AGENTS.md
```

## 构建与运行

```bash
dotnet build src/AsusMonitor.sln
dotnet run --project src/AsusMonitor.Probe      # 探针（需管理员）
dotnet test src/AsusMonitor.Tests
dotnet publish src/AsusMonitor -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## 技术栈与关键依赖

- .NET 8 / C# 12 / WPF
- LibreHardwareMonitorLib（传感器采集）
- NVML：经 NvAPIWrapper 或 P/Invoke 调用 nvml.dll
- 模式切换：WMI（System.Management）调华硕 ACPI 接口；参考实现 G-Helper `app/AsusACPI.cs`、`app/Mode/`
- 权限：manifest 声明 requireAdministrator；探针与主程序都必须提权运行

## 硬性纪律

1. **只走官方接口**：ACPI WMI（华硕驱动）+ NVML + LHM。禁止逆向/读写 Armoury Crate 进程、内存、named pipe、配置文件。
2. **写操作只有一处**：性能模式五档切换。不做风扇曲线、功耗写入（见 PRODUCT.md 非目标）。
3. **采集只读**：SensorService 不允许出现任何写硬件的调用。
4. **EC 读取间隔 ≥ 500ms**，UI 刷新 1s。
5. **与 AC 冲突检测**：启动时检测 Armoury Crate 服务状态，运行中只做监控、切换前弹提示。
6. 测试纪律：红在前；模式切换类测试需真机，CI 里只跑纯逻辑测试。
7. 提交信息中文，先写 `.commit_msg.txt` 再 `git commit -F`（主人环境约定）。

## 参考实现（读源码用，勿抄许可证不兼容的代码）

- G-Helper：https://github.com/seerge/g-helper（重点：`AsusACPI.cs`、`Mode/ModeControl.cs`）
- LibreHardwareMonitor：https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
