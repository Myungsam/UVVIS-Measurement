# UV-VIS meas 用户指南 (v2.3.1)

[한국어](USER_GUIDE.md) | [English](USER_GUIDE.en.md) | [日本語](USER_GUIDE.ja.md) | **简体中文**

本指南从安装开始，按顺序说明如何使用 Ocean Optics / Ocean Insight 光谱仪测量 **吸光度** 和 **荧光**。

> 本指南中的截图和数值全部来自 **演示模式（虚拟光谱仪）**，均为 **虚拟数据**。
> 没有硬件也可以用 `UV-VIS_meas.exe --demo` 打开相同的界面，跟着操作。
>
> 程序界面支持 **韩语和英语**（没有中文界面）。本指南使用 **英文界面**，按钮和标签页名称按英文原样用 **粗体** 书写。
> 程序内部结构、计算公式的依据和硬件设置的详细说明见 [参考手册](README.md)（仅韩语）。

## 目录

1. [准备](#1-准备)
2. [安装](#2-安装)
3. [界面布局](#3-界面布局)
4. [确定测量条件](#4-确定测量条件)
5. [测量吸光度](#5-测量吸光度)
6. [测量荧光](#6-测量荧光)
7. [测量随时间的变化](#7-测量随时间的变化)
8. [测量方法文件](#8-测量方法文件)
9. [常见问题](#9-常见问题)

---

## 1. 准备

| 项目 | 说明 |
|---|---|
| 电脑 | Windows 10 / 11。Windows 已自带 .NET Framework 4.8 |
| 驱动 | **Ocean Optics OmniDriver**（x86）。本程序不附带该驱动，请先安装 |
| 光谱仪 | 能被 OmniDriver 识别的 Ocean Optics 光谱仪（Flame-S、Ocean FX 等），USB 连接 |
| 光源 | 吸光度：覆盖测量波段的光源（卤素灯、氘灯、LED 等）。荧光：激发光源 |
| 样品容器 | 比色皿（通常光程 10 mm）和溶解样品所用的溶剂 |

---

## 2. 安装

### 2.1 安装 OmniDriver

先运行从 Ocean Optics 获得的 OmniDriver 安装程序。安装完成后，通常在
`C:\Program Files (x86)\Ocean Optics\OmniDriverSPAM\OOI_HOME` 之类的文件夹中会有 `NETOmniDriver-NET40.dll`。
用 USB 连接光谱仪，并确认 Windows 能识别该设备。

### 2.2 下载安装文件

从 [GitHub Releases](https://github.com/Myungsam/UVVIS-Measurement/releases/latest) 下载 `UV-VIS_meas-Setup-2.3.1.exe`。
可以在 PowerShell 中校验文件，结果应与发布页面上的 SHA-256 值一致。

```powershell
Get-FileHash .\UV-VIS_meas-Setup-2.3.1.exe -Algorithm SHA256
```

### 2.3 安装向导

运行安装程序。如果出现 **“Windows 已保护你的电脑”**，请点击 **更多信息 → 仍要运行**。
这是因为本开源版本没有代码签名。
安装向导支持韩语和英语。在中文 Windows 上会先弹出语言选择窗口，请选择 **English**。下面是英文界面。

**① 安装模式** —— 推荐选择无需管理员权限的 **Install for me only**。
（第一个对话框的 **取消** 按钮由 Windows 绘制，因此显示为系统语言）

![安装模式](images/guide-en/i01_mode.png)

**② 许可协议** —— MIT 许可证。选择 **I accept the agreement**，然后点击 **Next**。

![许可协议](images/guide-en/i02_license.png)

**③ 安装位置** —— 保持默认值即可（为当前用户安装时为 `%LOCALAPPDATA%\Programs\UV-VIS meas`）。
图中的路径是为本指南修改的示例。

![安装位置](images/guide-en/i03_dir.png)

**④ 附加任务** —— 需要桌面快捷方式时勾选。开始菜单中总会创建快捷方式。

![附加任务](images/guide-en/i04_tasks.png)

**⑤ 准备安装** → **Install**

![准备安装](images/guide-en/i05_ready.png)

**⑥ 完成** —— 可以在这里打开简要使用说明或直接启动程序。

![完成](images/guide-en/i06_finish.png)

> 如果没有安装 OmniDriver，安装程序会在最后提示。安装本身仍会完成，因此可以稍后再安装 OmniDriver。

### 2.4 首次启动

- 从开始菜单启动 **UV-VIS meas**。
- 如果程序无法自动找到 OmniDriver，会弹出文件夹选择窗口。选择包含 `NETOmniDriver-NET40.dll` 的 `OOI_HOME` 文件夹后，
  以后不会再询问。在该窗口中选择 **No** 则以演示模式启动。
- 程序 **同一时间只能运行一个**。已在运行时再次启动，会把已打开的窗口切换到前台
  （因为一台光谱仪只能被一个程序打开）。

### 2.5 演示模式（无需硬件即可练习）

在开始菜单快捷方式的属性中，在“目标”末尾加上 ` --demo`，或按如下方式运行：

```powershell
& "$env:LOCALAPPDATA\Programs\UV-VIS meas\UV-VIS_meas.exe" --demo
```

会打开两台虚拟光谱仪。`[0]` 看到的是透过样品的光，`[1]` 直接看光源。
`[0]` 的样品在 **420 nm 和 520 nm 处有吸收带**，吸收强度以 **90 秒为周期** 增强和减弱。
窗口标题会显示 `[DEMO MODE - simulated spectrometers]`。

### 2.6 更新与卸载

- **更新**：直接运行新版本的安装程序，会覆盖安装到同一文件夹，设置会保留。
- **卸载**：Windows 设置 → 应用，或开始菜单中的 **Uninstall UV-VIS meas**。卸载时会询问是否同时删除保存的设置和错误日志。
  **测量数据不会被删除**（它们位于你选择的保存文件夹中）。

---

## 3. 界面布局

![启动后的界面](images/guide-en/01_start.png)

| 位置 | 内容 |
|---|---|
| 菜单 / 工具栏（顶部） | 开始 / 停止测量、Dark、White、保存、记录。常用操作有快捷键（见下表） |
| 步骤指示（左上） | 显示 **Start → Dark → White → Measure / Save** 的顺序。蓝色数字是下一步，绿色勾表示已完成。点击步骤也会执行对应操作 |
| Set parameters | 最小测量间隔、平均次数（**Average**）、触发模式 |
| 光谱仪列表 | 已连接的光谱仪。测量在 **Use** 中勾选的那一台。积分时间和触发延迟按行分别设置 |
| Capture Dark / Capture White | 把当前显示的光谱保存为暗背景或参比 |
| Save datasets | 样品名、浓度、比色皿厚度、保存文件夹 |
| 图表标签页（右侧） | Absorbance/Fluorescence、Sample / Dark / White Ref.、Timed Absorption Series、Timed Absorption Series (2D plot)、Hardware Settings |
| 状态栏（底部） | 设备信息、进度、每秒帧数、累计数据 |

| 快捷键 | 操作 | 快捷键 | 操作 |
|---|---|---|---|
| `F5` | 开始 / 停止测量 | `Ctrl+S` | 保存光谱 |
| `Ctrl+D` | Capture Dark | `Ctrl+Shift+S` | 保存随时间变化的光谱 |
| `Ctrl+W` | Capture White | `Ctrl+R` | 开始 / 停止记录随时间变化的数据 |
| `Ctrl+1` / `Ctrl+2` | Absorption / Fluorescence 图 | `Ctrl+3` | Sample / Dark / White Ref. |
| `Ctrl+4` / `Ctrl+5` | 随时间变化 / 二维图 | `Ctrl+6` | Hardware Settings |
| `Ctrl+P` | 将图表保存为 PNG | `Ctrl+E` | PDF 测量报告 |
| `Ctrl+O` | 叠加显示已保存的光谱 | `Ctrl+M` | 载入测量方法 |
| `F9` | 显示 / 隐藏设置面板 | `F1` | 打开参考手册 |

语言在 **Tools → Language / 언어** 中选择（韩语或英语）。

---

## 4. 确定测量条件

测量前先调好信号强度，可以大大降低结果中的噪声。

1. 在光谱仪列表中，只勾选要测量的那台光谱仪的 **Use**。
2. 打开光源，放入空比色皿（或溶剂），点击 **Start measurement**（`F5`）。
3. 在 **Sample / Dark / White Ref.** 标签页（`Ctrl+3`）查看信号的最大值。
4. 调整列表中的 **Integration time**，使最大值约为满量程（Flame-S 为 65535 counts）的 **50–85 %**。
   - 状态栏出现 **`SATURATED`（饱和）** 时，请缩短积分时间。饱和区域的吸光度不可信。
   - 信号太弱时，延长积分时间或增加 **Average**。平均次数增加到 4 倍，噪声约减半。
5. **Set Min Interval** 是界面刷新间隔的下限。长时间测量随时间的变化时，加大间隔可以减小文件。

> 改变积分时间或平均次数后，**需要重新采集 Dark 和 White**，因为信号强度已经变了。

---

## 5. 测量吸光度

吸光度表示样品吸收了多少光。程序用三条光谱计算：

```
A = -log10( (S - D) / (W - D) )

S：放入样品时的信号          D：挡住光时的信号（Dark）
W：放入空比色皿 / 溶剂时的信号（White，参比）
```

顺序是 **Start → Dark → White → 样品**。Dark 和 White 保存的是点击那一刻 **屏幕上显示的光谱**，
所以必须先开始测量。

### 5.1 开始测量

点击 **Start measurement**（`F5`），步骤 ① 出现绿色勾，光谱持续刷新。
在 **Sample / Dark / White Ref.** 标签页可以看到原始入射光。

![测量中的原始光谱](images/guide-en/02_raw_running.png)

### 5.2 Capture Dark —— 挡住光

关闭光源快门，或用不透光的物体挡住光路。待曲线降到基线附近并变平后，点击 **Capture Dark**（`Ctrl+D`）。

- 关灯再开灯需要等待光源输出重新稳定，**使用快门更方便**。
- Dark 会同时扣除光谱仪的基线（电子偏置）和环境光，因此室内照明应与实际测量时保持一致。

> 在 **演示模式** 中，虚拟光源的快门连接在 GPIO 0 上。在 **Hardware Settings** 标签页（`Ctrl+6`）中，
> 把光谱仪 [0] 的 **GPIO 0 → Direction 设为 `Output`，Output 设为 `HIGH`**，光就被挡住了。
>
> ![在演示模式中关闭快门（GPIO 0 = Output / HIGH）](images/guide-en/03_shutter_closed.png)

光被挡住后，图中只剩带噪声的基线（演示中约 1000 counts）。

![挡住光的状态](images/guide-en/04_dark_signal.png)

点击 **Capture Dark** 后，红线（Dark）被保存，步骤指示进入 ③ White。
状态栏会显示保存了哪台光谱仪的光谱及其平均值。

![采集 Dark 之后](images/guide-en/05_after_dark.png)

### 5.3 Capture White —— 空比色皿（溶剂）

重新打开快门，放入 **只装有溶解样品所用溶剂的比色皿**（或空比色皿）。信号稳定后点击 **Capture White**（`Ctrl+W`）。
绿线（White 参比）被保存，步骤指示进入 ④。

![采集 White 之后](images/guide-en/06_after_white.png)

> White 一定要用 **与样品相同的溶剂、相同的比色皿** 采集。这样溶剂和比色皿的吸收与反射会被抵消，
> 只留下样品本身的吸光度。

### 5.4 测量样品

把样品放入比色皿，**Absorption** 标签页（`Ctrl+1`）会立即显示吸光度光谱。
下图是演示样品吸收最强的时刻，可以看到 420 nm 和 520 nm 的吸收带。

![吸光度光谱](images/guide-en/07_absorbance.png)

- 吸光度 0 表示透过的光与 White 相同；**1 OD 表示只有 10 % 的光透过**。
- 吸光度在 **约 0.1–1.5 OD** 范围内最准确。更高时请稀释样品，透过的光太少会使噪声变大。
- 图中空缺的波长是无法计算数值的地方（几乎没有光，`S − D` 或 `W − D` 小于等于 0），是有意留空的。

### 5.5 分析栏 —— 平滑、峰、光标、浓度

吸光度图上方的工具栏可以改变显示方式。**这些处理只作用于显示，保存的 CSV 始终是测量原值。**

![平滑、峰标记和光标读数](images/guide-en/08_analysis.png)

| 控件 | 用法 |
|---|---|
| 显示方式 | Absorbance (OD) / Transmittance (%T) / Raw counts |
| Smooth | Savitzky-Golay 平滑。**window (px)** 越大越平滑，但窄峰会变低。一般用 9–21 px |
| Peaks | 用红色标记和波长标出峰。如果像图中那样 **连噪声峰也被标出，请提高 min. height**（例如 0.020 → 0.1） |
| 基线校正 | 基线倾斜或抬高时使用：`Baseline: zero at`（一个无吸收的波长）、`Baseline: line between`（两点连线）、`Baseline: rubber band` |
| Open spectrum... / Clear overlays | 把已保存的 CSV 以虚线叠加显示进行比较（`Ctrl+O`） |
| 读数行 | 鼠标放在图上时，显示 **光标处的值**、**最高的峰** 和 **摩尔吸光系数** |

**摩尔吸光系数（Beer-Lambert）** —— 在 *Save datasets* 中填写 **Concentration** 和 **Cell Thickness**，程序会在光标处计算 `ε = A / (c · l)`。
用图中的虚拟数据计算如下：

```
A = 0.7314 OD (520 nm),  c = 50 uM = 5.0×10⁻⁵ mol/L,  l = 10 mm = 1 cm
ε = 0.7314 / (5.0×10⁻⁵ × 1) = 1.463×10⁴ L/(mol·cm)
```

浓度和厚度可以连同单位一起输入：`50 uM`、`0.05`（只写数字表示 mM）、`1.5 M` / `10 mm`、`1 cm`、`10`（只写数字表示 mm）。

要查看 **透射率（%T）**，在显示方式中选择 **Transmittance (%T)**。`%T = 100 × 10^-A`。

![透射率显示](images/guide-en/09_transmittance.png)

### 5.6 保存

点击 **Save spectrum**（`Ctrl+S`），会在保存文件夹中生成如下名称的 CSV：

```
<样品名>_spectrum_<日期>_<时间>.csv        例：Demo-dye_spectrum_20261029_082548.csv
```

文件开头的 `#` 行记录测量条件（设备、积分时间、平均次数、触发延迟、参比状态、是否饱和），
下面是各波长的数据。可以直接用 Excel 打开。虚拟数据的一部分如下：

```
# Sample,Demo-dye
# Concentration (mM),50 uM
# Cell thickness,10 mm
# Integration (ms),1.000
# Scans to average,4
# Absorbance,A = -log10((S - D) / (W - D))
# Reference state,[0]* Dark: OK  White: OK
# Saturated,no
Wavelength (nm),Sample (counts),Dark (counts),White reference (counts),Absorbance (OD)
420.0191,1081.2946,993.20897,1176.4824,0.31819431
520.0903,1121.5532,990.75193,1681.4119,0.72265239
550.0337,1279.2316,991.72321,1894.2847,0.49682633
```

`Sample`、`Dark`、`White` 列也会一起保存，因此可以自己重新计算吸光度进行核对。
以 520 nm 这一行为例：`(1121.55 − 990.75) / (1681.41 − 990.75) = 0.1894`，`−log10(0.1894) = 0.7227 OD`，与保存的值一致。

> CSV 的列名和 `#` 行始终用英文记录，以便其他程序读取。

### 5.7 图表图片与测量报告

- **File → Export plot as image**（`Ctrl+P`）：把当前图表保存为 PNG。
- **File → Export measurement report**（`Ctrl+E`）：生成一页 A4 PDF，包含样品信息、测量条件、峰列表和图表。

![测量报告示例（单页 PDF）](images/guide-en/13_report_page.png)

---

## 6. 测量荧光

荧光是样品受激发后 **以其他（更长）波长发出的光**。与吸光度不同，**不需要 White 参比**，
也不用公式计算，直接看 **强度（counts）**。

### 6.1 装置布置

- 激发光与光谱仪光纤通常成 **90°** 布置，避免激发光直接进入光谱仪。
- 在激发侧放带通滤光片、在光谱仪侧放长通滤光片，可以减少散射的激发光。
- 荧光较弱，积分时间常常比吸光度 **长得多**（数百 ms 以上）。请按第 4 章确认 **没有饱和**。

### 6.2 测量步骤

1. 如果之前在测吸光度，先用 **Clear Dark / White** 清除原有参比。
2. 确定积分时间和平均次数（第 4 章），然后点击 **Start measurement**（`F5`）。
3. **Capture Dark**（`Ctrl+D`）：建议 **在只装溶剂的比色皿上照射激发光的状态下** 采集。这样会连同基线一起扣除溶剂的散射
   （包括拉曼散射）和环境光。只想扣除基线时，挡住激发光再采集。
4. 换成样品比色皿，查看 **Fluorescence** 标签页（`Ctrl+2`）。
5. **Save spectrum**（`Ctrl+S`）。不采集 White。此时 CSV 只保存 `Wavelength (nm)`、`Sample (counts)`、`Dark (counts)` 三列。

![Fluorescence 标签页（原始 counts）](images/guide-en/10_fluorescence.png)

> 上图是演示模式的界面。虚拟光谱仪只模拟透射光，所以显示的是 **光源光谱，而不是真实的荧光光谱**。
> 它只用于说明界面布局和操作方法。

### 6.3 Fluorescence 标签页显示的值

Fluorescence 标签页直接绘制光谱仪读出的 **原始 counts**。**没有扣除 Dark**，所以能看到基线（偏置）。
如果只需要荧光信号，请在保存的 CSV 中计算 `Sample − Dark`。下表是用于说明的虚构数值。

| Wavelength (nm) | Sample (counts) | Dark (counts) | 荧光 = Sample − Dark |
|---|---|---|---|
| 560 | 1,410 | 1,002 | 408 |
| 580 | 3,950 | 1,004 | 2,946 |
| 600 | 6,820 | 1,001 | **5,819**（最大） |
| 620 | 4,710 | 998 | 3,712 |
| 650 | 1,880 | 1,003 | 877 |

在 Excel 中，若 `Sample` 在 B 列、`Dark` 在 C 列，在新列中输入 `=B2-C2` 并向下填充即可。

**查找发射峰波长** —— 在 **Absorption** 标签页的分析栏中把显示方式改为 **Raw counts**，并勾选 **Smooth** 和 **Peaks**，
就会标出荧光光谱的峰波长。读数行中的 `Peak … nm` 是最高的峰。
峰是根据相对周围的高度差寻找的，基线恒定时结果不变。不过 **min. height 最大只能设为 10**，
所以在 counts 显示下噪声峰也可能被标出。加大平滑的 window 可以减少噪声峰。

> 比较不同日期或条件下的荧光强度时，需要保持积分时间、平均次数、激发光强度和光纤位置不变。
> counts 是相对值，不同仪器之间的绝对比较需要另外校准。

---

## 7. 测量随时间的变化

记录吸光度随时间的变化，用于反应动力学或稳定性测试。请先按第 5 章采集 Dark 和 White。

1. 在 **Timed Absorption Series** 标签页（`Ctrl+4`）输入 **Trace wavelength (nm)** 并点击 **Add**。可以同时跟踪多个波长。
2. 点击 **Record timed datasets**（`Ctrl+R`）开始记录。按钮会变成 **Recording - click to stop**。
3. 跟踪波长处的吸光度随时间绘出。在下面的虚拟数据中，可以看到样品的吸收随时间变化。

![吸光度随时间的变化](images/guide-en/11_timed.png)

4. **Timed Absorption Series (2D plot)** 标签页（`Ctrl+5`）以横轴为时间、纵轴为波长、颜色为吸光度显示。
   取消 **Auto scale** 即可自行设定 Min / Max (OD)。

![吸光度随时间变化的二维图](images/guide-en/12_heatmap.png)

5. 再次点击即停止记录。用 **Save timed spectra**（`Ctrl+Shift+S`）保存全部数据。

**测量中写入磁盘（数据保护）** —— 记录时，每一行数据在存入内存的同时会 **立即** 写入下面两个文件，
即使程序异常退出，之前的数据也会保留。

```
<样品名>_timed_live_<日期>_<时间>_absorbance.csv     行 = 测量时间，列 = 各波长的吸光度
<样品名>_timed_live_<日期>_<时间>_counts.csv         行 = 测量时间，列 = 各波长的原始 counts
```

内存中只保留最近的 5000 条记录并丢弃最旧的，但磁盘文件会保留全部数据。
**Save timed spectra** 以相同格式保存内存中的数据（`..._timed_<日期>_<时间>_absorbance.csv`）。

```
Elapsed (s),Timestamp (UTC),178.8000,179.1376,179.4752,...
116.8430,2026-10-28T23:25:54.7510601Z,0.051050305,0.026433554,-0.03545706,...
117.1022,2026-10-28T23:25:55.0103759Z,0.085171446,0.033534959,-0.024552224,...
```

> 光源亮度随时间漂移时，吸光度也会随之变化。用第二台光谱仪同时监测光源并进行校正的
> **实时参比校正**，见 [参考手册 §6](README.md#6-흡광도-계산과-dark--white-reference)（韩语）。

---

## 8. 测量方法文件

以相同条件重复测量时使用的文件（`.uvmethod`）。

- **File → Save measurement method**：保存最小测量间隔、平均次数、各光谱仪的积分时间 / 触发延迟 / 角色、跟踪波长以及显示和处理设置。
- **File → Load measurement method**（`Ctrl+M`）：重新应用保存的条件。光谱仪按 **序列号** 匹配。
- **不包含 Dark / White。** 参比只对当天的光源和溶剂有效，载入方法后也请 **重新采集**。

---

## 9. 常见问题

| 现象 | 检查项目 |
|---|---|
| 找不到光谱仪 | USB 连接、OmniDriver 安装。确认没有其他软件（OceanView 等）占用设备 |
| 再次启动没有出现新窗口 | 正常现象，已运行的窗口会切换到前台 |
| “Windows 已保护你的电脑” | 因为发布版本未签名。**更多信息 → 仍要运行** |
| 吸光度全是噪声 | 光源是否打开？Dark 是否在挡光时采集？White 是否用空比色皿采集？ |
| 状态栏出现 `SATURATED` | 缩短积分时间，并重新采集 Dark 和 White |
| 点击 Capture Dark / White 没有反应 | 请先开始测量，程序保存的是屏幕上显示的光谱 |
| 标出的峰太多 | 打开平滑或提高 min. height |
| 测量中光信号中断 | 检查光源和光纤接头。程序不控制光源 |
| Fluorescence 标签页的基线不为 0 | 正常现象，该页显示未扣除 Dark 的原始 counts（6.3） |

更多现象和原因见 [参考手册 §10](README.md#10-문제-해결)（韩语）。
出错时会记录在 `%LOCALAPPDATA%\UV-VIS_meas\error.log` 中，反馈问题时请附上此文件。
