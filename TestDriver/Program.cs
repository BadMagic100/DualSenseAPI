using DualSenseAPI;
using DualSenseAPI.Util;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;

namespace Demo
{
    class Program
    {
        static DualSense ChooseController()
        {
            DualSense[] available = DualSense.EnumerateControllers().ToArray();
            while (available.Length == 0)
            {
                Console.WriteLine("No DualSenses connected, press any key to retry.");
                Console.ReadKey(true);
                available = DualSense.EnumerateControllers().ToArray();
            }

            for (int i = 0; i < available.Length; i++)
            {
                Console.WriteLine($"{i}: {available[i]}");
            }
            Console.Write("Found some DualSenses, select one: ");
            int idx;
            do
            {
                bool parseSuccess = int.TryParse(Console.ReadLine(), out idx);
                if (!parseSuccess) idx = -1;
            } while (idx < 0 || idx >= available.Length);

            return available[idx];
        }

        static void Main2(string[] args)
        {
            DualSense ds = ChooseController();

            ds.Connect();
            bool pMicBtnState = false;
            bool pR1State = false;
            bool pL1State = false;

            ds.LightbarBehavior = LightbarBehavior.CustomColor;
            ds.JoystickDeadZone = 0.1f;
            ds.R2Effect = new TriggerEffect.Vibrate(20, 1f, 1f, 1f);
            ds.L2Effect = new TriggerEffect.Section(0.0f, 0.5f);
            int wheelPos = 0;
            DualSenseState dss;
            do
            {
                Console.Clear();
                dss = ds.ReadWriteOnce();

                Console.WriteLine($"LS: ({dss.LeftAnalogStick.X:F2}, {dss.LeftAnalogStick.Y:F2})");
                Console.WriteLine($"RS: ({dss.RightAnalogStick.X:F2}, {dss.RightAnalogStick.Y:F2})");
                Console.WriteLine($"Triggers: ({dss.L2:F2}, {dss.R2:F2})");
                Console.WriteLine($"Touch 1: ({dss.Touchpad1.X}, {dss.Touchpad1.Y}, {dss.Touchpad1.IsDown}, {dss.Touchpad1.Id})");
                Console.WriteLine($"Touch 2: ({dss.Touchpad2.X}, {dss.Touchpad2.Y}, {dss.Touchpad2.IsDown}, {dss.Touchpad2.Id})");
                Console.WriteLine($"Gyro: ({dss.Gyro.X}, {dss.Gyro.Y}, {dss.Gyro.Z})");
                Console.WriteLine($"Accel: ({dss.Accelerometer.X}, {dss.Accelerometer.Y}, {dss.Accelerometer.Z})");
                Console.WriteLine($"Headphone: {dss.IsHeadphoneConnected}");
                Console.WriteLine($"Battery: {dss.BatteryStatus.IsCharging}, {dss.BatteryStatus.IsFullyCharged}, {dss.BatteryStatus.Level}");

                ListPressedButtons(dss);

                ds.LeftRumble = Math.Abs(dss.LeftAnalogStick.Y);
                ds.RightRumble = Math.Abs(dss.RightAnalogStick.Y);

                if (!pMicBtnState && dss.MicButton)
                {
                    ds.MicLed = ds.MicLed switch
                    {
                        MicLed.Off => MicLed.Pulse,
                        MicLed.Pulse => MicLed.On,
                        _ => MicLed.Off
                    };
                }
                pMicBtnState = dss.MicButton;

                if (!pR1State && dss.R1Button)
                {
                    ds.PlayerLed = ds.PlayerLed switch
                    {
                        PlayerLed.None => PlayerLed.Player1,
                        PlayerLed.Player1 => PlayerLed.Player2,
                        PlayerLed.Player2 => PlayerLed.Player3,
                        PlayerLed.Player3 => PlayerLed.Player4,
                        PlayerLed.Player4 => PlayerLed.All,
                        _ => PlayerLed.None
                    };
                }
                pR1State = dss.R1Button;

                if (!pL1State && dss.L1Button)
                {
                    ds.PlayerLedBrightness = ds.PlayerLedBrightness switch
                    {
                        PlayerLedBrightness.High => PlayerLedBrightness.Low,
                        PlayerLedBrightness.Low => PlayerLedBrightness.Medium,
                        _ => PlayerLedBrightness.High
                    };
                }
                pL1State = dss.L1Button;

                ds.LightbarColor = ColorWheel(wheelPos);
                wheelPos = (wheelPos + 5) % 384;

                Thread.Sleep(20);
            } while (!dss.LogoButton);
            ds.LightbarBehavior = LightbarBehavior.PulseBlue;
            ds.PlayerLed = PlayerLed.None;
            ds.R2Effect = TriggerEffect.Default;
            ds.L2Effect = TriggerEffect.Default;
            ds.ReadWriteOnce();
            ds.Release();
        }

        static void Main(string[] args)
        {
            DualSense ds = ChooseController();

            ds.Connect();
            bool pMicBtnState = false;
            bool pR1State = false;
            bool pL1State = false;

            ds.LightbarBehavior = LightbarBehavior.CustomColor;
            ds.JoystickDeadZone = 0.1f;
            ds.R2Effect = new TriggerEffect.Vibrate(10, 0.5f, 1, 1);
            ds.L2Effect = new TriggerEffect.Section(0.0f, 0.5f);
            int wheelPos = 0;
            // note this polling rate is actually slower than the delay above, because it can do the processing while waiting for the next poll
            // (20ms/50Hz is actually quite fast and will clear the screen faster than it can write the data)
            ds.BeginPolling(100, dss => { 
                Console.Clear();

                Console.WriteLine($"LS: ({dss.LeftAnalogStick.X:F2}, {dss.LeftAnalogStick.Y:F2})");
                Console.WriteLine($"RS: ({dss.RightAnalogStick.X:F2}, {dss.RightAnalogStick.Y:F2})");
                Console.WriteLine($"Triggers: ({dss.L2:F2}, {dss.R2:F2})");
                Console.WriteLine($"Touch 1: ({dss.Touchpad1.X}, {dss.Touchpad1.Y}, {dss.Touchpad1.IsDown}, {dss.Touchpad1.Id})");
                Console.WriteLine($"Touch 2: ({dss.Touchpad2.X}, {dss.Touchpad2.Y}, {dss.Touchpad2.IsDown}, {dss.Touchpad2.Id})");
                Console.WriteLine($"Gyro: ({dss.Gyro.X}, {dss.Gyro.Y}, {dss.Gyro.Z})");
                Console.WriteLine($"Accel: ({dss.Accelerometer.X}, {dss.Accelerometer.Y}, {dss.Accelerometer.Z})");
                Console.WriteLine($"Headphone: {dss.IsHeadphoneConnected}");
                Console.WriteLine($"Battery: {dss.BatteryStatus.IsCharging}, {dss.BatteryStatus.IsFullyCharged}, {dss.BatteryStatus.Level}");

                ListPressedButtons(dss);

                ds.LeftRumble = Math.Abs(dss.LeftAnalogStick.Y);
                ds.RightRumble = Math.Abs(dss.RightAnalogStick.Y);

                if (!pMicBtnState && dss.MicButton)
                {
                    ds.MicLed = ds.MicLed switch
                    {
                        MicLed.Off => MicLed.Pulse,
                        MicLed.Pulse => MicLed.On,
                        _ => MicLed.Off
                    };
                }
                pMicBtnState = dss.MicButton;

                if (!pR1State && dss.R1Button)
                {
                    ds.PlayerLed = ds.PlayerLed switch
                    {
                        PlayerLed.None => PlayerLed.Player1,
                        PlayerLed.Player1 => PlayerLed.Player2,
                        PlayerLed.Player2 => PlayerLed.Player3,
                        PlayerLed.Player3 => PlayerLed.Player4,
                        PlayerLed.Player4 => PlayerLed.All,
                        _ => PlayerLed.None
                    };
                }
                pR1State = dss.R1Button;

                if (!pL1State && dss.L1Button)
                {
                    ds.PlayerLedBrightness = ds.PlayerLedBrightness switch
                    {
                        PlayerLedBrightness.High => PlayerLedBrightness.Low,
                        PlayerLedBrightness.Low => PlayerLedBrightness.Medium,
                        _ => PlayerLedBrightness.High
                    };
                }
                pL1State = dss.L1Button;

                ds.LightbarColor = ColorWheel(wheelPos);
                wheelPos = (wheelPos + 5) % 384;

            });
            //note that readkey is blocking, which means we know this input method is truly async
            Console.ReadKey(true);
            ds.LightbarBehavior = LightbarBehavior.PulseBlue;
            ds.PlayerLed = PlayerLed.None;
            ds.R2Effect = TriggerEffect.Default;
            ds.L2Effect = TriggerEffect.Default;
            ds.MicLed = MicLed.Off;
            ds.EndPolling();
            ds.ReadWriteOnce();
            ds.Release();
        }

        static LightbarColor ColorWheel(int position)
        {
            int r = 0, g = 0, b = 0;
            switch (position / 128)
            {
                case 0:
                    r = 127 - position % 128;   //Red down
                    g = position % 128;      // Green up
                    b = 0;                  //blue off
                    break;
                case 1:
                    g = 127 - position % 128;  //green down
                    b = position % 128;      //blue up
                    r = 0;                  //red off
                    break;
                case 2:
                    b = 127 - position % 128;  //blue down
                    r = position % 128;      //red up
                    g = 0;                  //green off
                    break;
            }
            return new LightbarColor(r / 255f, g / 255f, b / 255f);
        }

        static void ListPressedButtons(DualSenseState dss)
        {
            IEnumerable<string> pressedButtons = dss.GetType().GetProperties()
                .Where(p => p.Name.EndsWith("Button") && p.PropertyType == typeof(bool))
                .Where(p => (bool)p.GetValue(dss))
                .Select(p => p.Name.Replace("Button", ""));
            string joined = string.Join(", ", pressedButtons);
            Console.WriteLine($"Buttons: {joined}");
        }
    }
}
