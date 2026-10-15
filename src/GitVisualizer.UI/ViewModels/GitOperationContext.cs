using System;
using System.Threading;
using System.Threading.Tasks;
using GitVisualizer.Core.Models;
using GitVisualizer.UI.Abstractions;

namespace GitVisualizer.UI.ViewModels;

/// <summary>
/// 子 ViewModel が共有する操作対象リポジトリと、git 操作の実行手順
/// (ビジー表示 → 実行 → 出力ウィンドウへの記録 → 失敗時の通知 → 画面の再読み込み)。
/// </summary>
public sealed class GitOperationContext
{
    private readonly INotificationService _notificationService;
    private int _busyCount;

    /// <summary><see cref="GitOperationContext"/> を初期化します。</summary>
    /// <param name="notificationService">通知サービス。</param>
    /// <param name="logger">ログ出力先。</param>
    public GitOperationContext(INotificationService notificationService, IOutputLogger logger)
    {
        _notificationService = notificationService;
        Logger = logger;
    }

    /// <summary>操作対象リポジトリのルートパス。リポジトリが開かれていなければ <see langword="null"/>。</summary>
    public string? RepositoryPath { get; set; }

    /// <summary>ログ出力先。</summary>
    public IOutputLogger Logger { get; }

    /// <summary>ビジー表示・状態メッセージの表示先(ステータスバー)。</summary>
    public OperationStateViewModel? State { get; set; }

    /// <summary>操作の後に画面全体を再読み込みする処理。</summary>
    public Func<CancellationToken, Task>? RefreshAll { get; set; }

    /// <summary>git 操作を実行します(進捗通知なし)。</summary>
    /// <param name="name">操作名(ステータスバー・ログに表示)。</param>
    /// <param name="operation">リポジトリのパスを受け取って git を実行する処理。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    public Task<bool> RunAsync(string name, Func<string, CancellationToken, Task<GitResult>> operation, CancellationToken ct = default)
    {
        return RunAsync(name, (path, _, token) => operation(path, token), ct);
    }

    /// <summary>git 操作を実行します。UI スレッドから呼ぶこと。</summary>
    /// <param name="name">操作名(ステータスバー・ログに表示)。</param>
    /// <param name="operation">リポジトリのパスと進捗の通知先を受け取って git を実行する処理。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    public async Task<bool> RunAsync(
        string name,
        Func<string, IProgress<OperationProgress>, CancellationToken, Task<GitResult>> operation,
        CancellationToken ct = default)
    {
        string? path = RepositoryPath;
        if (path == null)
        {
            await _notificationService.ShowErrorAsync("Git リポジトリが開かれていません。", ct);
            return false;
        }

        SetBusy(true);
        SetStatus($"{name} を実行中...");
        Logger.Log($"{name} を開始します。");

        // UI スレッドで生成するので、進捗は UI スレッドに届く
        var progress = new Progress<OperationProgress>(p => SetStatus($"{name}: {p.Message}"));
        bool success = false;
        try
        {
            GitResult result = await operation(path, progress, ct);
            LogOutput(result);
            success = result.IsSuccess;

            if (success)
            {
                SetStatus($"{name} が完了しました。");
                await _notificationService.ShowInfoAsync($"Git Visualizer: {name} が完了しました。", ct);
            }
            else
            {
                SetStatus($"{name} に失敗しました。");
                string detail = FirstNonEmpty(result.StandardError, result.StandardOutput).Trim();
                await _notificationService.ShowErrorAsync($"{name} に失敗しました。{Environment.NewLine}{Environment.NewLine}{detail}", ct);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus($"{name} を取り消しました。");
            Logger.Log($"{name} を取り消しました。");
        }
        catch (Exception ex)
        {
            SetStatus($"{name} に失敗しました。");
            Logger.LogError($"{name} に失敗しました。", ex);
            await _notificationService.ShowErrorAsync($"{name} に失敗しました。{Environment.NewLine}{Environment.NewLine}{ex.Message}", CancellationToken.None);
        }
        finally
        {
            SetBusy(false);
        }

        // 失敗(競合で停止など)でもリポジトリの状態は変わりうるので、常に再読み込みする
        if (RefreshAll != null)
        {
            await RefreshAll(CancellationToken.None);
        }

        return success;
    }

    /// <summary>読み込み処理の失敗を記録します(ダイアログは出さずにステータスバーと出力ウィンドウに出す)。</summary>
    /// <param name="what">失敗した処理。</param>
    /// <param name="exception">原因の例外。</param>
    public void ReportLoadError(string what, Exception exception)
    {
        Logger.LogError($"{what}に失敗しました。", exception);
        SetStatus($"{what}に失敗しました: {exception.Message.Trim()}");
    }

    private void LogOutput(GitResult result)
    {
        string output = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
        if (output.Length != 0)
        {
            Logger.Log(output);
        }

        if (!result.IsSuccess)
        {
            Logger.LogError($"git が終了コード {result.ExitCode} で終了しました。");
        }
    }

    private void SetBusy(bool busy)
    {
        _busyCount = Math.Max(0, _busyCount + (busy ? 1 : -1));
        if (State != null)
        {
            State.IsBusy = _busyCount > 0;
        }
    }

    private void SetStatus(string message)
    {
        if (State != null)
        {
            State.StatusMessage = message;
        }
    }

    private static string FirstNonEmpty(string first, string second) => string.IsNullOrWhiteSpace(first) ? second : first;
}
