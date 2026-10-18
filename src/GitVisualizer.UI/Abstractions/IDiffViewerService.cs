using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Models;

namespace GitVisualizer.UI.Abstractions;

/// <summary>ファイルの差分表示(VS では差分ビューアー)。</summary>
public interface IDiffViewerService
{
    /// <summary>変更前・変更後のファイルを並べた差分ウィンドウを開きます。</summary>
    /// <param name="diff">比較するファイルと見出し。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ウィンドウを開くと完了するタスク。</returns>
    Task ShowDiffAsync(FileDiff diff, CancellationToken ct = default);
}
