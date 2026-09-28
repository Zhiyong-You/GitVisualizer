using System;

namespace GitVisualizer.UI.Abstractions;

/// <summary>ログ出力先(VS では出力ウィンドウの「Git Visualizer」ペイン)。スレッドセーフであること。</summary>
public interface IOutputLogger
{
    /// <summary>情報ログを 1 行書き込みます。</summary>
    /// <param name="message">メッセージ。</param>
    void Log(string message);

    /// <summary>エラーログを書き込みます。</summary>
    /// <param name="message">メッセージ。</param>
    /// <param name="exception">原因となった例外。なければ <see langword="null"/>。</param>
    void LogError(string message, Exception? exception = null);
}
