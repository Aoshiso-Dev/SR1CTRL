using SR1CTRL.Application.Models;

namespace SR1CTRL.Application.Abstractions;

public interface IAppStateStore
{
    AppStateSnapshot Load();

    void Save(AppStateSnapshot snapshot);
}
