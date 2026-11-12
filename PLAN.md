# PLAN.md — system-monitor 实施计划

> 状态标记：`[ ]` 未开始 / `[~]` 进行中 / `[x]` 完成。每步带验收信号，做完更新状态并同步 HANDOFF.md。

## 历史阶段（纯监控 v1.0.17，已完成）

- [x] 探针可行性（G533QS 全传感器可读；NVML TGP）
- [x] 采集层 SensorService + 降级 + 单测
- [x] ~~模式控制层~~ 已删（主人决定纯监控）
- [x] WPF 圆环界面 v3 + 托盘/自启/诊断/单文件发布

## 引擎枢纽阶段（2026-11-12，方案见 WorkBuddy 推理控制面板方案.md §七）

- [x] **P1 导航壳 + 首页两卡 + MonitorView**：MainWindow 改 ContentControl 导航；HomePage 实时摘要；MonitorView 圆环三卡 + GPU 显存行 + 并排曲线。验收：三视图导航截屏 ✓
- [x] **P2 EngineControlService + Ollama pane**：起停/互斥/PID/清代理/日志/state.json；摘要条；顶栏分段/切换/停止+撤销；模型点选加载。验收：Ollama 启动→列表→加载 gpt-oss:20b→停止→撤销 全链路 ✓
- [x] **P3 Strata pane + 切换显存校验**：family/quant 列表 + launch；切换先停→VramGate(基线+500MB/10s)→起。验收：互斥 Ollama 停 ✓；Strata 起停闭环 ✓
- [x] **P4 调用助手 + 指标 + 曲线 + 美化**：CallHelpProvider 六段/引擎 + 复制；MetricsService（Strata engine.log 尾读 / Ollama done）；Sparkline/RingGauge 自绘；ROG 色板对齐原型。验收：剪贴板整串端点 ✓；889.6 tok/s 真实解析 ✓
- [x] **P5 NVML 显存 + 降级 + 文档口径**：nvmlDeviceGetMemoryInfo → GpuMemUsed/Total；GPU 卡显存行+渐变条+判据；降级 "--"；PRODUCT/AGENTS/HANDOFF/PLAN 同步。验收：14.6→0.2GB 回落 ✓
- [x] 崩溃修复：采集重入锁（LHM native AV）；Strata launch 参数改 family/model/data-dir
- [x] 单测 9/9（StrataLogParser/OllamaJson/VramGate/CallHelp/EngineState）

## 剩余

- [x] 单文件 exe 发布（最新 **release-r11/SystemMonitor.exe**：新 logo（红渐变圆角+白色脉冲）；旧 release*/dist-release 被系统句柄锁，重启后清理）
- [x] 数据核对：GPU/显存/频率对 nvidia-smi 一致；内存 LHM 口径同旧版；tok/s 小样本尖峰已加 ≥32 token 稳定阈值（单测 10/10）
- [x] 四轮反馈：竖排单列对齐预览（860 居中内容列；首页两卡/监控三卡上下堆叠；引擎面板→2×2 指标→曲线→助手；摘要条两列）
- [x] 三轮反馈：删首页/监控残线曲线、C 盘换可用内存、拖拽/缩放修复（实测通过）
- [x] 视觉与原型一模一样（2026-11-12 二轮：氛围辉光+44px 网格、顶栏 logo/INFERENCE HUB/窗口圆点、卡片顶部渐变线、环/曲线/点辉光、悬停上浮、入场/脉冲动效、bg0 指标格与代码块、滚动条配色；三视图截屏比对通过）
- [ ] 主人 30 分钟挂机长跑 + 验收下结论
- [ ] git 提交（待主人要求）
- [ ] 上下文占用指标（Strata telemetry 解析，尽力项）
- [ ] Strata pane 列表选中色随引擎色（当前共用 Ollama 青，小瑕疵）

## 已知风险与应对

| 风险 | 应对 |
|:---|:---|
| LHM/NVML 并发 AV | 采集重入锁（已修，勿删） |
| Strata CLI 拒裸 gguf | family/model/data-dir 参数（已修） |
| 沙盒代理 502 | 子进程清代理 + HttpClient UseProxy=false |
| 另一会话同时调引擎 | 真机测前先探端口 |
| WinRing0 被拦 | 降级 "--" + 诊断报告 |
