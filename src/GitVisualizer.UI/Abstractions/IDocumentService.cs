using System.Threading;
using System.Threading.Tasks;

namespace GitVisualizer.UI.Abstractions;

/// <summary>ファイルをエディターで開く(VS ではドキュメントウィンドウ)。</summary>
public interface IDocumentService
{
    /// <summary>ファイルをエディターで開きます。</summary>
    /// <param name="fullPath">開くファイルのフルパス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ファイルを開くと完了するタスク。</returns>
    Task OpenFileAsync(string fullPath, CancellationToken ct = default);
}
