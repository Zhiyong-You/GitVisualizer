using System;

namespace GitVisualizer.Vsix;

/// <summary>GitVisualizerPackage.vsct の Symbols と一致させる GUID / コマンド ID。変更時は両方を直すこと。</summary>
internal static class PackageIds
{
    /// <summary>パッケージの GUID(vsct: guidGitVisualizerPackage)。</summary>
    public const string PackageGuidString = "e7b7fe4d-20e4-4d97-90ed-c4e89ae7d32e";

    /// <summary>コマンドセットの GUID 文字列(vsct: guidGitVisualizerCmdSet)。</summary>
    public const string CommandSetGuidString = "8307c1f9-fb2c-4dcb-aac7-ed2faf09a6e6";

    /// <summary>ツールウィンドウの GUID。</summary>
    public const string ToolWindowGuidString = "ccc588b0-2eb5-41e7-96d0-53e29a4317c4";

    /// <summary>出力ウィンドウの「Git Visualizer」ペインの GUID。</summary>
    public const string OutputPaneGuidString = "b66e93de-dabe-4dc6-892b-cb416897ca11";

    /// <summary>コマンドセットの GUID。</summary>
    public static readonly Guid CommandSet = new(CommandSetGuidString);

    /// <summary>「表示 &gt; その他のウィンドウ &gt; Git Visualizer」コマンド(vsct: cmdidShowToolWindow)。</summary>
    public const int ShowToolWindowCommandId = 0x0100;
}
