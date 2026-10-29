# UV-VIS meas

[한국어](README.md) | [English](README.en.md) | [日本語](README.ja.md) | **简体中文**

[![Release](https://img.shields.io/github/v/release/Myungsam/UVVIS-Measurement)](https://github.com/Myungsam/UVVIS-Measurement/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

一款配合 Ocean Optics / Ocean Insight 光谱仪 **测量紫外-可见吸光度和荧光光谱** 的 Windows 程序。
采集暗背景（Dark）和参比（White）后放入样品，吸光度光谱会立即显示，测量数据保存为 CSV。
本软件免费、开源（MIT）。

> 程序界面支持 **韩语和英语**（没有中文界面）。本文中的按钮名称均按英文界面原样书写。

![吸光度测量界面（英文界面）](docs/images/guide-en/08_analysis.png)

## 主要功能

- **吸光度、透射率、荧光（原始强度）** 测量，用步骤指示引导采集暗背景和参比
- 平滑、基线校正、寻峰、光标读数，以及 **Beer-Lambert 摩尔吸光系数计算**
- 记录 **吸光度随时间的变化** 并显示二维图；测量过程中逐行写入磁盘，程序异常退出也不会丢失数据
- 使用两台光谱仪实时校正光源漂移（双光束）
- 保存含测量条件的 CSV、叠加显示已保存的光谱、图像保存为 PNG、单页 A4 PDF 测量报告
- 将测量条件保存为方法文件，以相同条件重复测量
- 韩语 / 英语界面，以及无需硬件即可练习的 **演示模式**

## 安装

### 准备

| 项目 | 说明 |
|---|---|
| 电脑 | Windows 10 / 11（Windows 已自带 .NET Framework 4.8） |
| 驱动 | **Ocean Optics OmniDriver**。本程序不附带该驱动，请先安装 |
| 光谱仪 | 能被 OmniDriver 识别的 Ocean Optics 光谱仪，USB 连接 |

### 步骤

1. **安装 OmniDriver**，并用 USB 连接光谱仪。
2. 从 [最新版本](https://github.com/Myungsam/UVVIS-Measurement/releases/latest) 下载 `UV-VIS_meas-Setup-<版本>.exe`。
3. 运行安装程序。如果出现 **“Windows 已保护你的电脑”**，请点击 **更多信息 → 仍要运行**。
   这是因为本开源版本没有代码签名。
4. 选择 **Install for me only** 即可在无管理员权限的情况下安装。接受许可协议，一直点击 **Next** 直到完成。
   （安装界面为韩语或英语）
5. 从开始菜单启动 **UV-VIS meas**。

如需校验下载的文件，请在 PowerShell 中运行下面的命令，并与发布页面上的 SHA-256 值比较：

```powershell
Get-FileHash .\UV-VIS_meas-Setup-2.3.1.exe -Algorithm SHA256
```

带截图的说明见 [用户指南第 2 章](docs/USER_GUIDE.zh-CN.md#2-安装)。

### 更新与卸载

- **更新**：直接运行新版本的安装程序即可覆盖安装，设置会保留。
- **卸载**：Windows 设置 → 应用，或开始菜单中的 **Uninstall UV-VIS meas**。测量数据（CSV）不会被删除。

## 快速开始 —— 吸光度测量

1. 在光谱仪列表中勾选要测量的光谱仪的 **Use**，并打开光源。
2. 点击 **Start measurement**（`F5`）。
3. 关闭光源快门或挡住光路，点击 **Capture Dark**（`Ctrl+D`）。
4. 放入只装有溶剂的比色皿，点击 **Capture White**（`Ctrl+W`）。
5. 换成样品比色皿，**Absorption** 图中就会显示吸光度。点击 **Save spectrum**（`Ctrl+S`）保存为 CSV。

荧光测量、随时间变化的测量、分析功能和保存格式，请按顺序阅读 [用户指南](docs/USER_GUIDE.zh-CN.md)。

## 无需硬件即可体验（演示模式）

两台虚拟光谱仪可以让你练习所有功能，即使没有安装 OmniDriver 也能运行。

```powershell
& "$env:LOCALAPPDATA\Programs\UV-VIS meas\UV-VIS_meas.exe" --demo
```

找不到 OmniDriver 时弹出的对话框中选择 **No**，也会以演示模式启动。

## 文档

| 文档 | 内容 |
|---|---|
| [用户指南](docs/USER_GUIDE.zh-CN.md) | 安装，吸光度 / 荧光 / 随时间变化的测量，保存与常见问题，附截图逐步说明 |
| [参考手册](docs/README.md) | 计算依据、硬件设置、文件格式、详细的问题排查（按 `F1` 打开，仅韩语） |
| [更新记录](CHANGELOG.md) | 各版本的变更（韩语） |
| [第三方声明](THIRD-PARTY-NOTICES.md) | 本程序使用的组件，如 OmniDriver |

## 已验证的环境

| 项目 | 说明 |
|---|---|
| 光谱仪 | Flame-S（USB2000+ 系列）、OmniDriver 2.56 —— 已用实机确认 |
| 操作系统 | Windows 11，x86 程序 |
| 尚未验证 | 用标准样品比对吸光度、实际荧光测量、Flame-S 以外的型号、实际 150 % 缩放的显示器（[待办事项](docs/TODO.md)，韩语） |

其他 Ocean Optics 型号只要能被 OmniDriver 识别，也应可以使用；各设备的功能和允许范围在运行时从驱动读取。

## 数据和设置的保存位置

| 内容 | 位置 |
|---|---|
| 测量数据（CSV）、报告 | 程序中 **Save path (folder)** 选定的文件夹 |
| 程序设置 | `%LOCALAPPDATA%\UV-VIS_meas\settings.xml` |
| 错误日志 | `%LOCALAPPDATA%\UV-VIS_meas\error.log`（反馈问题时请附上此文件） |

## 注意事项

测量结果的准确性取决于光谱仪状态、光源、暗背景 / 参比以及测量条件。
本程序用于实验室测量，并非为医疗诊断设计。

## 许可证

以 [MIT 许可证](LICENSE) 发布。Ocean Optics OmniDriver 不在此许可证范围内，也不随本程序分发（见 [第三方声明](THIRD-PARTY-NOTICES.md)）。

## 开发者

从源码构建、测试、制作安装程序以及源码结构，请参阅 [开发者指南](docs/DEVELOPMENT.md)（韩语）。
