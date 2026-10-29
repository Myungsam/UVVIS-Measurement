# UV-VIS meas

[한국어](README.md) | [English](README.en.md) | **日本語** | [简体中文](README.zh-CN.md)

[![Release](https://img.shields.io/github/v/release/Myungsam/UVVIS-Measurement)](https://github.com/Myungsam/UVVIS-Measurement/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Ocean Optics / Ocean Insight の分光器で **UV-VIS 吸光度と蛍光スペクトルを測定** する Windows 用プログラムです。
Dark・White の基準を取得して試料を入れると、すぐに吸光度スペクトルが表示され、測定値は CSV で保存されます。
無料で使えるオープンソース（MIT）です。

> プログラムの画面は **韓国語と英語** に対応しています（日本語表示はありません）。
> この文書では英語画面のボタン名をそのまま記載しています。

![吸光度測定の画面（英語表示）](docs/images/guide-en/08_analysis.png)

## 主な機能

- **吸光度・透過率・蛍光（生の強度）** の測定。Dark・White 基準の取得をステップ表示で案内
- スムージング、ベースライン補正、ピーク検出、カーソル位置の値、**Beer-Lambert によるモル吸光係数の計算**
- **吸光度の時間変化** の記録と 2D マップ。測定中にディスクへ逐次書き込むため、異常終了してもデータが残ります
- 分光器 2 台による光源ドリフトのリアルタイム補正（ダブルビーム）
- 測定条件付きの CSV 保存、保存したスペクトルの重ね表示、グラフの PNG 保存、A4 一枚の PDF 測定レポート
- 測定条件をファイルに保存し、同じ条件で繰り返し測定
- 韓国語 / 英語の画面、装置なしで練習できる **デモモード**

## インストール

### 必要なもの

| 項目 | 内容 |
|---|---|
| PC | Windows 10 / 11（.NET Framework 4.8 は Windows に標準搭載） |
| ドライバー | **Ocean Optics OmniDriver**。本プログラムには同梱されていないため、先にインストールしてください |
| 分光器 | OmniDriver が認識する Ocean Optics の分光器（USB 接続） |

### 手順

1. **OmniDriver をインストール** し、分光器を USB で接続します。
2. [最新リリース](https://github.com/Myungsam/UVVIS-Measurement/releases/latest) から `UV-VIS_meas-Setup-<バージョン>.exe` をダウンロードします。
3. セットアップを実行します。**「Windows によって PC が保護されました」** と表示されたら **詳細情報 → 実行** をクリックします。
   コード署名をしていないオープンソース版のため表示される警告です。
4. **Install for me only** を選ぶと管理者権限なしでインストールできます。ライセンスに同意し、**Next** で進めて完了します。
   （セットアップの画面は韓国語または英語です）
5. スタートメニューの **UV-VIS meas** から起動します。

ダウンロードしたファイルを確認するには、PowerShell で次のコマンドを実行し、リリースページの SHA-256 値と比べてください。

```powershell
Get-FileHash .\UV-VIS_meas-Setup-2.3.1.exe -Algorithm SHA256
```

画面付きの説明は [ユーザーガイド 第 2 章](docs/USER_GUIDE.ja.md#2-インストール) にあります。

### 更新とアンインストール

- **更新**：新しいセットアップをそのまま実行すると上書きインストールされます。設定は保持されます。
- **アンインストール**：Windows の設定 → アプリ、またはスタートメニューの **Uninstall UV-VIS meas**。測定データ（CSV）は削除されません。

## クイックスタート ― 吸光度測定

1. 分光器の表で、測定する分光器の **Use** にチェックを入れ、光源をつけます。
2. **Start measurement**（`F5`）をクリックします。
3. 光源のシャッターを閉じるか光路を遮り、**Capture Dark**（`Ctrl+D`）。
4. 溶媒だけを入れたキュベットをセットし、**Capture White**（`Ctrl+W`）。
5. 試料のキュベットに替えると **Absorption** のグラフに吸光度が表示されます。**Save spectrum**（`Ctrl+S`）で CSV を保存します。

蛍光測定、時間変化の測定、解析機能、保存形式は [ユーザーガイド](docs/USER_GUIDE.ja.md) で順に説明しています。

## 装置なしで試す（デモモード）

仮想の分光器 2 台ですべての機能を試せます。OmniDriver がなくても動作します。

```powershell
& "$env:LOCALAPPDATA\Programs\UV-VIS meas\UV-VIS_meas.exe" --demo
```

OmniDriver が見つからないときに表示されるダイアログで **No** を選んでもデモモードで起動します。

## ドキュメント

| 文書 | 内容 |
|---|---|
| [ユーザーガイド](docs/USER_GUIDE.ja.md) | インストール、吸光度・蛍光・時間変化の測定、保存、トラブルシューティングを画面付きで |
| [リファレンスマニュアル](docs/README.md) | 計算式の根拠、ハードウェア設定、保存形式、詳しいトラブルシューティング（`F1` で開く。韓国語のみ） |
| [変更履歴](CHANGELOG.md) | バージョンごとの変更（韓国語） |
| [サードパーティー通知](THIRD-PARTY-NOTICES.md) | OmniDriver など、本プログラムが使用するコンポーネント |

## 確認済みの環境

| 項目 | 内容 |
|---|---|
| 分光器 | Flame-S（USB2000+ 系）、OmniDriver 2.56 ― 実機で確認 |
| OS | Windows 11、x86 プログラム |
| 未確認 | 標準試料による吸光度値の比較、実際の蛍光測定、Flame-S 以外の機種、実際の 150 % 表示（[残作業](docs/TODO.md)、韓国語） |

ほかの Ocean Optics 機種も OmniDriver が認識すれば動作するように作られています。機種ごとの機能と設定範囲は実行時にドライバーから読み取ります。

## データと設定の保存場所

| 内容 | 場所 |
|---|---|
| 測定データ（CSV）、レポート | 画面の **Save path (folder)** で選んだ場所 |
| プログラムの設定 | `%LOCALAPPDATA%\UV-VIS_meas\settings.xml` |
| エラーログ | `%LOCALAPPDATA%\UV-VIS_meas\error.log`（問い合わせの際に添付してください） |

## 注意

測定結果の精度は、分光器の状態、光源、Dark・White 基準、測定条件によって変わります。
本プログラムは実験室での測定用であり、医療診断用には作られていません。

## ライセンス

[MIT ライセンス](LICENSE)で公開しています。Ocean Optics OmniDriver はこのライセンスに含まれず、同梱もしていません（[サードパーティー通知](THIRD-PARTY-NOTICES.md)）。

## 開発者向け

ソースからのビルド、テスト、インストーラーの作成、ソース構成は [開発者ガイド](docs/DEVELOPMENT.md)（韓国語）にあります。
