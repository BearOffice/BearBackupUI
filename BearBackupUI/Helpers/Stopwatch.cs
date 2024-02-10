namespace BearBackupUI.Helpers;

public class Stopwatch
{
    public TimeSpan Elapsed { get => _sw.Elapsed; }
    public event Action<TimeSpan>? Signal;
    private readonly System.Diagnostics.Stopwatch _sw = new System.Diagnostics.Stopwatch();
    private CancellationTokenSource? _tokenSource;
    private readonly int _period;

    public Stopwatch(int millisecondsPeriod)
    {
        _period = millisecondsPeriod;
    }

    public void Start()
    {
        _tokenSource = new CancellationTokenSource();
        _sw.Start();

        Task.Run(() =>
        {
            while (!_tokenSource.IsCancellationRequested)
            {
                Task.Delay(_period).Wait();
                Signal?.Invoke(_sw.Elapsed);
            }
        }, _tokenSource.Token);
    }

    public void Stop()
    {
        _tokenSource?.Cancel();
        _sw.Stop();
    }

    public void Reset()
    {
        _tokenSource?.Cancel();
        _sw.Reset();
    }

    public void Restart()
    {
        Reset();
        Start();
    }
}