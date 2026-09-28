# Git Visualizer

Visual Studio 2022 / 2026 向けの Git クライアント拡張機能(VSSDK / In-Proc)。

> 現在はスケルトンです。画面はダミー表示で、ボタンは押しても何もしません。
> Core のサービスは `NotImplementedException` を投げます(`// TODO:` を参照)。

## 構成

```
GitVisualizer.sln
├─ src/
│   ├─ GitVisualizer.Core/   net472。VS・WPF に依存しない(Models / Services / Infrastructure / Graph)
│   ├─ GitVisualizer.UI/     net472 WPF。ViewModel(CommunityToolkit.Mvvm)と View
│   ├─ GitVisualizer.Vsix/   VSIX 本体(Package / ToolWindow / コマンド / VS サービス実装)
│   └─ GitEditorHelper/      reword 用の補助 exe(VSIX に同梱)
└─ tests/
    └─ GitVisualizer.Core.Tests/  xUnit
```

依存関係: `Vsix → UI → Core`、`Tests → Core`。`Vsix → GitEditorHelper` は出力を同梱するだけでアセンブリ参照はしない。

- 対応 VS: `[17.0,19.0)`(VS 2022 / VS 2026)、amd64 / arm64
- git 操作は git.exe をプロセス起動する方式(LibGit2Sharp は使わない)
- サービスと ViewModel の組み立て(手動 DI)は `GitVisualizerPackage.InitializeToolWindowAsync` にある

## 必要なもの

- Visual Studio 2022 (17.x) または Visual Studio 2026 (18.x)
- ワークロード「Visual Studio 拡張機能の開発」
- git(PATH が通っていること)

## ビルド

VSIX プロジェクトは旧形式の csproj(VSSDK)なので、`dotnet build` ではなく **VS 付属の MSBuild** を使う。

```powershell
# 開発者 PowerShell for VS で
msbuild GitVisualizer.sln /restore /p:Configuration=Debug
```

生成物: `src\GitVisualizer.Vsix\bin\Debug\GitVisualizer.Vsix.vsix`

コマンドラインでビルドした場合、実験用インスタンスへの配置は行わない(`DeployExtension` は VS 内でのビルド時のみ有効)。

## テスト

```powershell
dotnet test tests\GitVisualizer.Core.Tests
```

VS では「テスト エクスプローラー」から実行できる。

## デバッグ(F5)

1. `GitVisualizer.sln` を VS 2022 または VS 2026 で開く
2. `GitVisualizer.Vsix` をスタートアップ プロジェクトに設定する
3. F5 を押す。ビルド後に拡張機能が実験用インスタンスに配置され、`devenv.exe /rootsuffix Exp` が起動する
   - 開いている VS と同じバージョンの実験用インスタンスが起動する(VS 2026 で開けば VS 2026 の Exp)
4. 実験用インスタンスで **表示 > その他のウィンドウ > Git Visualizer** を選ぶ
5. ツールウィンドウ(出力ウィンドウと同じタブグループ)にダミーデータが表示されることを確認する
6. **表示 > 出力** で「出力元の表示」を **Git Visualizer** にし、`Git Visualizer を初期化しました。` が出ていることを確認する
7. **ツール > テーマ** でダーク/ライトを切り替え、配色が追従することを確認する

### 実験用インスタンスをリセットしたいとき

拡張機能の登録が壊れたり、メニューが出なくなった場合は、次のどちらかでリセットする。

- スタートメニューの「Reset the Visual Studio 2022 Experimental Instance」(VS 2026 は同名の 2026 版)
- `%LOCALAPPDATA%\Microsoft\VisualStudio\17.0_xxxxxxxxExp`(VS 2026 は `18.0_...Exp`)を削除する

## 実装時のルール

- UI スレッドが必要な VS API は `JoinableTaskFactory.SwitchToMainThreadAsync` で切り替えてから呼ぶ(VSTHRD 警告を出さない)
- XAML の色は VS テーマのリソースキー(`EnvironmentColors` / `TreeViewColors` / `VsBrushes` / `VsResourceKeys`)に `DynamicResource` でバインドする。固定色は使わない
- UI の ViewModel / Abstractions から VS の型を参照しない(VS 依存は Vsix プロジェクトの `VsServices` に閉じ込める)
- 非同期メソッドの最後の引数は `CancellationToken ct = default`
