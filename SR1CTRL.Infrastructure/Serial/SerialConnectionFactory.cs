using SR1CTRL.Application.Abstractions;

namespace SR1CTRL.Infrastructure.Serial;

public sealed class SerialConnectionFactory : ISerialConnectionFactory
{
    public ISerialConnection Create(string portName, int baudRate)
    {
        return new SerialTCodeConnection(portName, baudRate);
    }
}
