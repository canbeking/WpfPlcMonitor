# 工业级 WPF 与 S7-1200 PLC 智能监控系统

[![WPF](https://img.shields.io/badge/Framework-WPF-orange.svg)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![SqlSugar](https://img.shields.io/badge/ORM-SqlSugar-green.svg)](https://www.donet5.com/)
[![S7.Net](https://img.shields.io/badge/PLC-S7.Net-lightgrey.svg)](https://github.com/S7NetPlus/s7netplus)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

一个面向工业现场的现代化 **HMI（人机界面）上位机监控系统**。本项目完整打通了从底层西门子 S7-1200 PLC 循环逻辑、TCP/IP 高性能通信、多线程安全 UI 刷新、SQLite 数据持久化与解耦回放，到边缘触发报警与日志管理的全套工业级闭环。

---

## 系统架构与数据流

系统的核心设计旨在解决工业现场常见的高频读写冲突、UI 卡顿、数据覆盖等痛点。

---

## 核心功能亮点与解决方案

### 1. 工业通信与多线程安全
* **无阻塞采集**：采用独立的后台循环（`StartPlcLoop`）进行高频 PLC 数据轮询，彻底避免了同步读写导致的 UI 线程冻结。
* **跨线程 UI 刷新**：严格遵循 WPF 线程规范，使用 `Dispatcher.Invoke` 安全地将后台采集的底层数据实时映射到视图层。
* **健壮的连接机制**：具备完善的异常捕获与重连设计，适应工业现场不稳定的网络环境。

### 2. 状态解耦与历史数据回放
* **痛点解决**：在开发过程中针对“PLC 循环逻辑覆盖用户历史查看”的典型问题进行了深度优化。本项目通过引入全局状态管理标志位（`isHistoricalView`），**完美解耦了实时监控流与历史回放流**，确保历史数据检索时不会被后台定时采集强行刷新覆盖。

### 3. 边缘触发报警与防刷屏机制
* **精细化控制**：摒弃了简单的电平持续报警，采用**边缘触发（Edge Trigger）**原理。
* **逻辑实现**：仅在“正常 $\to$ 越限”的交变瞬间触发一次弹窗通知与数据库持久化，有效防止了 PLC 每个扫描周期重复写入造成的日志爆炸和 UI 弹窗刷屏灾难。

### 4. 高性能轻量级持久化
* 引入 **SqlSugar ORM** 框架，结合 **SQLite** 实现零配置、自动化的 CodeFirst 数据表初始化（`DeviceDataLog` 与 `AlarmLog`）。

---

## 项目目录结构

```text
WpfPlcMonitor/
├── README.md                 # 项目核心文档
├── Documentation/            # 说明文档
│   ├── Screenshots/          # 运行截图
│   └── Demo.mp4              # 演示视频
├── WPF/                      # 上位机源码工程
└── PLC/                      # 下位机程序
```
## 快速开始 (Quick Start)

### 1. 环境要求
* **上位机**：Visual Studio 2022 及以上，支持 .NET Framework / .NET Core。
* **下位机**：西门子 TIA Portal (博途 V17 及以上) 或 PLCSIM 仿真运行环境。

### 2. 编译与运行
1. 克隆仓库到本地：
   ```bash
   git clone https://github.com/canbeking/WpfPlcMonitor.git
   ```
2. 用 Visual Studio 打开 WPF/WpfPlcMonitor.sln。

3. 检查 MainWindow.xaml.cs 中的 PLC IP 地址配置。

4. 点击 F5 编译并启动。


### 3. 视频演示
<video src="./Documentation/Demo.mp4" controls="controls" width="100%"></video>