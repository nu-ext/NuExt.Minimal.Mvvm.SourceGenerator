namespace WpfAppSample.Infrastructure;

public interface ICloseRequest
{
    event EventHandler<CloseRequestEventArgs> CloseRequested;
}

public sealed class CloseRequestEventArgs(bool? dialogResult = null) : EventArgs
{
    public bool? DialogResult { get; } = dialogResult;
}
