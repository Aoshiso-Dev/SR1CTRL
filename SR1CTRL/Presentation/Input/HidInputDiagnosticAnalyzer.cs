using System.Collections.Concurrent;
using System.Text;

namespace SR1CTRL.Presentation.Input;

public sealed class HidInputDiagnosticAnalyzer
{
    private readonly ConcurrentDictionary<string, DeviceSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Analyze(RawHidInput input, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(input.Data);

        if (input.ReportSize <= 0 || input.ReportCount <= 0 || input.Data.Length == 0)
        {
            return [];
        }

        var snapshot = _snapshots.GetOrAdd(input.DeviceName, _ => new DeviceSnapshot());
        var logs = new List<string>();

        for (var index = 0; index < input.ReportCount; index++)
        {
            var offset = index * input.ReportSize;
            if (offset + input.ReportSize > input.Data.Length)
            {
                break;
            }

            var reportBytes = new byte[input.ReportSize];
            Array.Copy(input.Data, offset, reportBytes, 0, reportBytes.Length);

            var reportId = reportBytes[0];
            if (!snapshot.LastReports.TryGetValue(reportId, out var previous))
            {
                snapshot.LastReports[reportId] = reportBytes;
                logs.Add($"{timestamp:HH:mm:ss.fff} report=0x{reportId:X2} baseline {ToHex(reportBytes)}");
                continue;
            }

            var differences = GetDifferences(previous, reportBytes);
            if (differences.Count == 0)
            {
                continue;
            }

            snapshot.LastReports[reportId] = reportBytes;
            logs.Add(BuildInferenceLog(timestamp, reportId, differences, reportBytes));
        }

        return logs;
    }

    private static string BuildInferenceLog(DateTimeOffset timestamp, byte reportId, IReadOnlyList<ByteDiff> differences, byte[] reportBytes)
    {
        if (differences.Count == 1)
        {
            var diff = differences[0];
            if (diff.Index > 0)
            {
                var delta = unchecked((sbyte)(diff.Current - diff.Previous));
                if (Math.Abs(delta) == 1)
                {
                    var direction = delta > 0 ? "CW" : "CCW";
                    return $"{timestamp:HH:mm:ss.fff} report=0x{reportId:X2} knobCandidate byte={diff.Index} dir={direction} raw={ToHex(reportBytes)}";
                }

                if (IsSingleBitChange(diff.Previous, diff.Current))
                {
                    var state = diff.Current > diff.Previous ? "Pressed" : "Released";
                    return $"{timestamp:HH:mm:ss.fff} report=0x{reportId:X2} buttonCandidate byte={diff.Index} state={state} raw={ToHex(reportBytes)}";
                }
            }
        }

        var summary = string.Join(", ", differences.Select(static d => $"b{d.Index}:{d.Previous:X2}->{d.Current:X2}"));
        return $"{timestamp:HH:mm:ss.fff} report=0x{reportId:X2} multiChange [{summary}] raw={ToHex(reportBytes)}";
    }

    private static bool IsSingleBitChange(byte previous, byte current)
    {
        var xor = previous ^ current;
        return xor != 0 && (xor & (xor - 1)) == 0;
    }

    private static List<ByteDiff> GetDifferences(byte[] previous, byte[] current)
    {
        var minLength = Math.Min(previous.Length, current.Length);
        var differences = new List<ByteDiff>(minLength);
        for (var i = 0; i < minLength; i++)
        {
            if (previous[i] == current[i])
            {
                continue;
            }

            differences.Add(new ByteDiff(i, previous[i], current[i]));
        }

        return differences;
    }

    private static string ToHex(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 3);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(bytes[i].ToString("X2"));
        }

        return builder.ToString();
    }

    private sealed class DeviceSnapshot
    {
        public Dictionary<byte, byte[]> LastReports { get; } = new();
    }

    private readonly record struct ByteDiff(int Index, byte Previous, byte Current);
}

