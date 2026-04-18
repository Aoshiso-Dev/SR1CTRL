using System.IO.Ports;
using SR1CTRL.Application.Abstractions;

namespace SR1CTRL.Infrastructure.Ports;

public sealed class ComPortProvider : IComPortProvider
{
    public IReadOnlyList<string> GetPortNames()
    {
        return SerialPort.GetPortNames().OrderBy(x => x).ToArray();
    }
}
