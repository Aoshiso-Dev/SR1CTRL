# SR1CTRL Clean Architecture

開発時の共通ポリシーは [AGENTS.md](AGENTS.md) を参照してください。

## Layering

- `SR1CTRL.Domain`
  - Axis motion business rules and TCode command construction.
  - No dependency on other SR1CTRL projects.

- `SR1CTRL.Application`
  - Use cases for device connection, query, and reciprocation control.
  - Depends on `SR1CTRL.Domain`.
  - Accesses serial/com-port only through abstractions.
  - Exposes DI entrypoint: `AddApplication()`.
  - Services split responsibilities:
    - `DeviceConnectionManager` (connection/session lifecycle)
    - `DeviceExecutionController` (start/stop and motion apply)

- `SR1CTRL.Infrastructure`
  - SerialPort and COM port adapter implementations.
  - Depends on `SR1CTRL.Application` abstractions.
  - Exposes DI entrypoint: `AddInfrastructure()`.

- `SR1CTRL` (WPF)
  - UI, ViewModel, composition root.
  - Depends on `SR1CTRL.Application` and `SR1CTRL.Infrastructure`.
  - UI defaults/ranges are centralized in `Presentation/Config/MotionDefaults.cs`.

## Dependency direction

`SR1CTRL (Presentation) -> SR1CTRL.Application -> SR1CTRL.Domain`

`SR1CTRL.Infrastructure -> SR1CTRL.Application`

## Testing

- `SR1CTRL.Application.Tests`
  - Unit tests for `DeviceControlUseCase` and `DeviceController`.