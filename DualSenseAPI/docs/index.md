# DualSenseAPI
This a .NET library for interfacing with the full feature set of a DualSense controller.

## Features
- **Basic Input**: Analog sticks and triggers, d-pad, and all buttons. Basically any input library
you will use can offer this, including DirectInput or similar.
- **Advanced Input**: Most of the rest of input features for the DualSense. This includes:
  - 6-axis accelerometer (accelerometer and gyroscope)
  - 2-point multitouch
  - Battery status
- **Output**: Most of the full suite of output features for the DualSense. This includes:
  - Haptic motors
  - Adaptive triggers
  - Lightbar color
- **Flexiblility of Control**: Supports both synchronous and asynchronous/event-driven IO.

## Example
This simple example connects to a DualSense controller using asynchronous polling. The repo contains
a more detailed sample and also shows the usage of synchronous polling as well. Check it out
[here](https://github.com/The-Demp/DualSenseAPI/blob/master/TestDriver/Program.cs#L53)!

```csharp
static void Main(string[] args)
{
    DualSense ds = DualSense.EnumerateControllers().First();
    ds.Acquire();
    ds.JoystickDeadZone = 0.1f;
    ds.BeginPolling(20, (sender) => {
        DualSenseInputState dss = sender.InputState;
        Console.WriteLine($"LS: ({dss.LeftAnalogStick.X:F2}, {dss.LeftAnalogStick.Y:F2})");
        Console.WriteLine($"RS: ({dss.RightAnalogStick.X:F2}, {dss.RightAnalogStick.Y:F2})");
        Console.WriteLine($"Triggers: ({dss.L2:F2}, {dss.R2:F2})");
        Console.WriteLine($"Touch 1: ({dss.Touchpad1.X}, {dss.Touchpad1.Y}, {dss.Touchpad1.IsDown}, {dss.Touchpad1.Id})");
        Console.WriteLine($"Touch 2: ({dss.Touchpad2.X}, {dss.Touchpad2.Y}, {dss.Touchpad2.IsDown}, {dss.Touchpad2.Id})");
        Console.WriteLine($"Gyro: ({dss.Gyro.X}, {dss.Gyro.Y}, {dss.Gyro.Z})");
        Console.WriteLine($"Accel: ({dss.Accelerometer.X}, {dss.Accelerometer.Y}, {dss.Accelerometer.Z}); m={dss.Accelerometer.Magnitude()}");
        Console.WriteLine($"Headphone: {dss.IsHeadphoneConnected}");
        Console.WriteLine($"Battery: {dss.BatteryStatus.IsCharging}, {dss.BatteryStatus.IsFullyCharged}, {dss.BatteryStatus.Level}");
    });
    Console.ReadKey(true);
    ds.EndPolling();
    ds.Release();
}
```