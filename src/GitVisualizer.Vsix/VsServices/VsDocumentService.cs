using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.UI.Abstractions;
using Microsoft.VisualStudio.Shell;

namespace GitVisualizer.Vsix.VsServices;

/// <summary>VS のドキュメントウィンドウでファイルを開く <see cref="IDocumentService"/> 実装。</summary>
internal sealed class VsDocumentService : IDocumentService
{
    private readonly AsyncPackage _package;

    /// <summary><see cref="VsDocumentService"/> を初期化します。</summary>
    /// <param name="package">所有するパッケージ。</param>
    public VsDocumentService(AsyncPackage package)
    {
        _package = package;
    }

    /// <inheritdoc/>
    public async Task OpenFileAsync(string fullPath, CancellationToken ct = default)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("ファイルが見つかりません。", fullPath);
        }

        // 既に開いていればそのウィンドウを前面に出す
        VsShellUtilities.OpenDocument(_package, fullPath);
    }
}
