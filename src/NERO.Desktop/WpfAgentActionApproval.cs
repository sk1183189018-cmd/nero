using NERO.Automation;

namespace NERO.Desktop;

/// <summary>Displays an explicit WPF approval dialog before any agent tool runs.</summary>
public sealed class WpfAgentActionApproval : IAgentActionApproval
{
    public async Task<bool> ConfirmAsync(string summary, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            System.Windows.MessageBox.Show(
                summary + "\n\nNERO will perform only the action described above.",
                "Approve NERO action",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No));
        cancellationToken.ThrowIfCancellationRequested();
        return result == System.Windows.MessageBoxResult.Yes;
    }
}
