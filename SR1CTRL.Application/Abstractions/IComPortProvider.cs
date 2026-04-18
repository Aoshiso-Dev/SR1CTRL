namespace SR1CTRL.Application.Abstractions;

public interface IComPortProvider
{
    IReadOnlyList<string> GetPortNames();
}
