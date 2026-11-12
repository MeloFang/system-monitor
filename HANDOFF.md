# HANDOFF.md — system-monitor 续接文档

> 任何人/任何会话接手本项目，先读这份 + PRODUCT.md。每完成一个阶段必须更新本文。

## 这程序是什么

Windows 桌面 exe（WPF，管理员，单实例，托盘）：**硬件监控 + 本地模型引擎控制枢纽**。
首页两卡 → 硬件监控视图（圆环三卡 + GPU 显存行 + CPU/GPU 并排曲线）/ 模型引擎视图（Ollama/Strata 合一：摘要条、分段启停、切换互斥、停止+撤销、模型加载、指标、调用助手）。

**明确不做**：模型拉取、风扇/性能模式/灯效（历史已删）、Python/浏览器/HTTP 代理（网页方案作废）。

## 做到哪一步

- 2026-10-25：纯监控 v1.0.17 发布（托盘/自启/诊断/圆环界面）。
- 2026-11-11：方案反转拍板（exe 为主界面），方案文档 + 交互原型定稿（WorkBuddy `推理控制面板方案.md` / `推理面板-UI示意.html`）。
- **2026-11-12：P1–P5 实施完成，真机实测通过**：
  - 导航三视图、首页实时摘要 ✓（截屏验证）
  - NVML 设备级显存 ✓（14.6GB→0.2GB 回落可见）
  - Ollama 启动/模型列表(10 项)/点选加载 gpt-oss:20b（/api/ps 读回）/停止/撤销 ✓
  - 切换互斥（起 Strata 前 Ollama 已停）✓；Strata launch/stop 闭环 ✓
  - 复制按钮 → 剪贴板整串端点 ✓；Strata engine.log 指标解析 ✓
  - 单测 10/10 绿（r10 增加 tok/s 稳定样本阈值用例）；编译 0 错误
  - 修复：1s Timer 重入并发 Collect 致 LHM native AV 崩溃 → 重入锁
- **未提交 git**（主人未要求提交，HEAD 仍是 10-25 的 `40f2577` v1.0.17）。最新单文件 exe 在 **release-r11/SystemMonitor.exe**（65.5 MB，11-12 11:05:52，当前运行中 PID 4840）；`release/`、`dist-release/` 是旧的 9.2 MB 框架依赖版，且被系统句柄长锁，**勿覆盖/删除**，发新包一律新建 `release-r12/`。
- 视觉迭代包 r3–r11 与各轮反馈记录见方案 md §十二；`.gitignore` 已忽略 `release*/`，这些包不再计入未跟踪项。
- 2026-11-12 二轮视觉对齐：WPF 与原型 `推理面板-UI示意.html` 逐字对齐（色板/氛围背景/顶栏/辉光/动效/滚动条），三视图截屏比对通过。
- 待主人：30 分钟挂机长跑、验收下结论。

## 怎么接手

1. 读 PRODUCT.md / AGENTS.md / GLOSSARY.md / PLAN.md。
2. 构建/测试/发布命令见 AGENTS.md；真机启动必须 `Start-Process -WindowStyle Normal`。
3. UI 自动化脚本在 `C:\Users\Administrator\AppData\Local\Temp\sm-nav.ps1 / sm-e2e.ps1 / sm-e2e2.ps1 / sm-strata.ps1`（需 UTF-8 BOM；UIA 窗口挑 ClassName=Window）。
4. 引擎状态文件：`%LOCALAPPDATA%\SystemMonitor\logs\engine-state.json`；引擎子进程日志同目录 ollama-serve.log / strata-launch.log。

## 关键注意事项（踩过的坑）

- LHM/NVML 非线程安全 → 采集重入锁勿删。
- Strata 只认 `--family/--model/--data-dir`；裸 gguf `--model-path` 被拒。
- 起子进程清代理；HttpClient 用 UseProxy=false。
- PS 5.1 无 BOM 脚本中文乱码；隐藏控制台父进程启动 GUI 会被 Windows 降级最小化→缩托盘「消失」；自动化启动用 `Start-Process explorer.exe <exe>` 拿前台，或 EnumWindows 找 -25600 隐藏句柄恢复（强恢后 WPF 不重绘会黑屏，须正常启动）。
- 另一个 WorkBuddy 会话可能同时在调 Strata；真机测引擎前先探端口 8080/11434，避免互斥冲突。
- 上下文占用指标暂 "--"（Strata telemetry 未解析）；Ollama tok/s 仅加载请求 done 对象可得（无原生 live metrics）。

## 目标机型

ASUS G533QS（5900HX + RTX 3080 Laptop 16GB + 32GB）。先做这一台，不承诺通用兼容。
