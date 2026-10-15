using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitVisualizer.UI.ViewModels.Dialogs;

/// <summary>ダイアログの ViewModel の基底。OK / キャンセルで <see cref="CloseRequested"/> を発生させる。</summary>
public abstract partial class DialogViewModelBase : ObservableObject
{
    /// <summary>ダイアログを閉じるよう要求したときに発生します。引数は OK なら <see langword="true"/>。</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>ダイアログのタイトル。</summary>
    public abstract string Title { get; }

    /// <summary>OK を押せるかどうか。入力値が変わったら <see cref="OkCommand"/> の NotifyCanExecuteChanged を呼ぶこと。</summary>
    /// <returns>入力値が有効なら <see langword="true"/>。</returns>
    protected virtual bool CanAccept() => true;

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void Ok() => CloseRequested?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);
}
