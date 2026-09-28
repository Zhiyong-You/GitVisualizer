using System.Threading;
using System.Threading.Tasks;

namespace GitVisualizer.UI.Abstractions;

/// <summary>ユーザーへの通知(VS ではステータスバー / メッセージボックス)。</summary>
public interface INotificationService
{
    /// <summary>情報を通知します(操作を妨げない表示)。</summary>
    /// <param name="message">メッセージ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>通知の表示が終わると完了するタスク。</returns>
    Task ShowInfoAsync(string message, CancellationToken ct = default);

    /// <summary>エラーを通知します(ユーザーの確認が必要な表示)。</summary>
    /// <param name="message">メッセージ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ユーザーが確認すると完了するタスク。</returns>
    Task ShowErrorAsync(string message, CancellationToken ct = default);
}
