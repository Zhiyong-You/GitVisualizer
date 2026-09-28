using System.Threading;
using System.Threading.Tasks;

namespace GitVisualizer.UI.Abstractions;

/// <summary>モーダルダイアログの表示。</summary>
public interface IDialogService
{
    /// <summary>ViewModel に対応するダイアログをモーダル表示します。</summary>
    /// <param name="viewModel">ダイアログの ViewModel(例: <c>CheckoutDialogViewModel</c>)。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>OK で閉じた場合は <see langword="true"/>。</returns>
    Task<bool> ShowDialogAsync(object viewModel, CancellationToken ct = default);

    /// <summary>はい/いいえの確認メッセージを表示します。</summary>
    /// <param name="message">メッセージ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>「はい」が選ばれた場合は <see langword="true"/>。</returns>
    Task<bool> ConfirmAsync(string message, CancellationToken ct = default);
}
