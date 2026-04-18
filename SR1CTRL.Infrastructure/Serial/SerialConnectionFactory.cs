using Microsoft.Extensions.Logging;
using SR1CTRL.Application.Abstractions;

namespace SR1CTRL.Infrastructure.Serial;

public sealed class SerialConnectionFactory : ISerialConnectionFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public SerialConnectionFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public ISerialConnection Create(string portName, int baudRate)
    {
        return new SerialTCodeConnection(portName, baudRate, _loggerFactory.CreateLogger<SerialTCodeConnection>());
    }
}