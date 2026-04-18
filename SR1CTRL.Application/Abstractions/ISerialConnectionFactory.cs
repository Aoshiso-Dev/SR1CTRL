namespace SR1CTRL.Application.Abstractions;

public interface ISerialConnectionFactory
{
    ISerialConnection Create(string portName, int baudRate);
}
