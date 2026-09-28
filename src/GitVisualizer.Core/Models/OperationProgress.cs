namespace GitVisualizer.Core.Models;

/// <summary>fetch / pull / push などの進捗通知。</summary>
public sealed class OperationProgress
{
    /// <summary><see cref="OperationProgress"/> を初期化します。</summary>
    /// <param name="message">表示用メッセージ(git の進捗行そのままでもよい)。</param>
    /// <param name="percent">進捗率(0-100)。不明なら <see langword="null"/>。</param>
    public OperationProgress(string message, int? percent = null)
    {
        Message = message;
        Percent = percent;
    }

    /// <summary>表示用メッセージ。</summary>
    public string Message { get; }

    /// <summary>進捗率(0-100)。不明なら <see langword="null"/>。</summary>
    public int? Percent { get; }
}
