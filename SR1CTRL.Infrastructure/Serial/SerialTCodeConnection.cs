using Microsoft.Extensions.Logging;
using SR1CTRL.Application.Abstractions;
using System.IO.Ports;
using System.Text;

namespace SR1CTRL.Infrastructure.Serial;

public sealed class SerialTCodeConnection : ISerialConnection
{
    private readonly SerialPort _serialPort;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ILogger<SerialTCodeConnection> _logger;

    private readonly object _rxGate = new();
    private readonly StringBuilder _rx = new();
    private readonly Queue<string> _lines = new();
    private TaskCompletionSource<bool>? _lineArrived;

    public bool IsOpen => _serialPort.IsOpen;

    public SerialTCodeConnection(string portName, int baudRate, ILogger<SerialTCodeConnection> logger)
    {
        _logger = logger;
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
            cancellationToken.ThrowIfCancellationRequested();
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
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
                ExtractCompleteLines();

                _lineArrived?.TrySetResult(true);
                _lineArrived = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process incoming serial data.");
        }
    }

    private void ExtractCompleteLines()
    {
        var segmentStart = 0;

        for (var i = 0; i < _rx.Length; i++)
        {
            if (_rx[i] != '\n')
            {
                continue;
            }

            var segmentLength = i - segmentStart;
            var line = _rx.ToString(segmentStart, segmentLength).TrimEnd('\r');
            _lines.Enqueue(line);
            segmentStart = i + 1;
        }

        if (segmentStart > 0)
        {
            _rx.Remove(0, segmentStart);
        }
    }

    public ValueTask DisposeAsync()
    {
        _serialPort.DataReceived -= OnDataReceived;

        try
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to close serial port while disposing.");
        }

        _serialPort.Dispose();
        _writeLock.Dispose();

        return ValueTask.CompletedTask;
    }
}
