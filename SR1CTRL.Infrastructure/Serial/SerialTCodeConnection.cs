using System.IO.Ports;
using System.Text;
using SR1CTRL.Application.Abstractions;

namespace SR1CTRL.Infrastructure.Serial;

public sealed class SerialTCodeConnection : ISerialConnection
{
    private readonly SerialPort _serialPort;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private readonly object _rxGate = new();
    private readonly StringBuilder _rx = new();
    private readonly Queue<string> _lines = new();
    private TaskCompletionSource<bool>? _lineArrived;

    public bool IsOpen => _serialPort.IsOpen;

    public SerialTCodeConnection(string portName, int baudRate)
    {
        _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            NewLine = "\n",
            Encoding = Encoding.ASCII,
            ReadTimeout = 50,
            WriteTimeout = 200,
            DtrEnable = true,
            RtsEnable = true,
        };

        _serialPort.DataReceived += OnDataReceived;
    }

    public Task OpenAsync()
    {
        if (!_serialPort.IsOpen)
        {
            _serialPort.Open();
        }

        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        if (_serialPort.IsOpen)
        {
            _serialPort.Close();
        }

        return Task.CompletedTask;
    }

    public async Task SendLineAsync(string line, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _serialPort.WriteLine(line);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<string?> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        lock (_rxGate)
        {
            if (_lines.Count > 0)
            {
                return _lines.Dequeue();
            }

            _lineArrived ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);

        try
        {
            await _lineArrived!.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        lock (_rxGate)
        {
            if (_lines.Count > 0)
            {
                return _lines.Dequeue();
            }

            _lineArrived = null;
            return null;
        }
    }

    private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var data = _serialPort.ReadExisting();
            if (string.IsNullOrEmpty(data))
            {
                return;
            }

            lock (_rxGate)
            {
                _rx.Append(data);

                while (true)
                {
                    var current = _rx.ToString();
                    var idx = current.IndexOf('\n');
                    if (idx < 0)
                    {
                        break;
                    }

                    var line = current.Substring(0, idx).TrimEnd('\r');
                    _lines.Enqueue(line);

                    _rx.Clear();
                    _rx.Append(current.Substring(idx + 1));
                }

                _lineArrived?.TrySetResult(true);
                _lineArrived = null;
            }
        }
        catch
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _serialPort.DataReceived -= OnDataReceived;

        try
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }
        catch
        {
        }

        _serialPort.Dispose();
        _writeLock.Dispose();

        await Task.CompletedTask;
    }
}
