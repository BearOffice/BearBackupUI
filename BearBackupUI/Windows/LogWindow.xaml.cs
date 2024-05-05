using BearBackupUI.Core;
using BearBackupUI.Core.Actions;
using BearBackupUI.Helpers;
using BearBackupUI.Stores;

namespace BearBackupUI.Windows;

public partial class LogWindow : FluentWindow
{
    private readonly DispatchCenter _dispatchCenter;
    private readonly LogStore _store;
    private readonly int _minFontSize = 10;
    private readonly int _maxFontSize = 28;
    private int _fontSize = 14;
    private int LogTextFontSize
    {
        get => _fontSize;
        set
        {
            _fontSize = value;
            LogTextBlock.FontSize = _fontSize;

            if (_fontSize == _minFontSize)
            {
                DecrMenuItem.IsEnabled = false;
            }
            else if (_fontSize == _maxFontSize)
            {
                IncrMenuItem.IsEnabled = false;
            }
            else
            {
                IncrMenuItem.IsEnabled = true;
                DecrMenuItem.IsEnabled = true;
            }
        }
    }

    public LogWindow(DispatchCenter dispatchCenter, LogStore logStore)
    {
        InitializeComponent();

        _dispatchCenter = dispatchCenter;
        _store = logStore;
        _store.Changed += (sender, e) => this.InvokeIfNeeded(() => Store_Changed(sender, e));

        var data = _store.GetData();
        LogTextBlock.Text = (string)(data.GetAnonymousData() ?? string.Empty);
        LogScrollViewer.ScrollToBottom();
    }
    
    private void Store_Changed(object? sender, DataArgs e)
    {
        LogTextBlock.Text = (string)(e.GetAnonymousData() ?? string.Empty);
        LogScrollViewer.ScrollToBottom();
    }

    private void IncrMenuItem_Click(object sender, RoutedEventArgs e)
    {
        LogTextFontSize += 2;
    }

    private void DecrMenuItem_Click(object sender, RoutedEventArgs e)
    {
        LogTextFontSize -= 2;
    }

    private void ClearMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Clear logs? This operation cannot be reverted.", "Notification",
            MessageBoxButton.OKCancel, MessageBoxImage.Warning);

        if (result == MessageBoxResult.OK)
        {
            var action = new ActionArgs(LogAction.ClearLog);
            _dispatchCenter.DispatchEvent(action, newThread: true);
        }
    }

    private void OnClosed(object sender, EventArgs e)
    {
        _store.Dispose();
    }
}
