using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DualSenseAPI.Util;

namespace DualSenseAPI
{
    public class DualSenseInputState
    {
        private readonly byte[] data;
        private readonly IoMode inputMode;
        private readonly float deadZone;
        internal DualSenseInputState(byte[] data, IoMode inputMode, float deadZone)
        {
            this.data = data;
            this.inputMode = inputMode;
            this.deadZone = deadZone;

            // Analog inputs
            LeftAnalogStick = ReadAnalogStick(data[0], data[1]);
            RightAnalogStick = ReadAnalogStick(data[2], data[3]);
            L2 = GetModeSwitch(4, 7).ToUnsignedFloat();
            R2 = GetModeSwitch(5, 8).ToUnsignedFloat();

            // Buttons
            byte btnBlock1 = GetModeSwitch(7, 4);
            byte btnBlock2 = GetModeSwitch(8, 5);
            byte btnBlock3 = GetModeSwitch(9, 6);
            SquareButton = btnBlock1.HasFlag(0x10);
            CrossButton = btnBlock1.HasFlag(0x20);
            CircleButton = btnBlock1.HasFlag(0x40);
            TriangleButton = btnBlock1.HasFlag(0x80);
            DPadUpButton = ReadDPadButton(btnBlock1, 0, 1, 7);
            DPadRightButton = ReadDPadButton(btnBlock1, 1, 2, 3);
            DPadDownButton = ReadDPadButton(btnBlock1, 3, 4, 5);
            DPadLeftButton = ReadDPadButton(btnBlock1, 5, 6, 7);
            L1Button = btnBlock2.HasFlag(0x01);
            R1Button = btnBlock2.HasFlag(0x02);
            L2Button = btnBlock2.HasFlag(0x04);
            R2Button = btnBlock2.HasFlag(0x08);
            CreateButton = btnBlock2.HasFlag(0x10);
            MenuButton = btnBlock2.HasFlag(0x20);
            L3Button = btnBlock2.HasFlag(0x40);
            R3Button = btnBlock2.HasFlag(0x80);
            LogoButton = btnBlock3.HasFlag(0x01);
            TouchpadButton = btnBlock3.HasFlag(0x02);
            MicButton = GetModeSwitch(9, -1).HasFlag(0x04); // not supported on the broken BT protocol, otherwise would likely be in btnBlock3

            // Multitouch
            Touchpad1 = ReadTouchpad(GetModeSwitch(32, -1, 4));
            Touchpad2 = ReadTouchpad(GetModeSwitch(36, -1, 4));

            // 6-axis
            Gyro = ReadAccelAxes(GetModeSwitch(15, -1, 2), GetModeSwitch(17, -1, 2), GetModeSwitch(19, -1, 2));
            Accelerometer = ReadAccelAxes(GetModeSwitch(21, -1, 2), GetModeSwitch(23, -1, 2), GetModeSwitch(25, -1, 2));

            // Misc
            byte batteryByte = GetModeSwitch(52, -1);
            byte miscByte = GetModeSwitch(53, -1); // this contains various stuff, seems to have both audio and battery info
            BatteryStatus = new BatteryStatus
            {
                IsCharging = batteryByte.HasFlag(0x10),
                IsFullyCharged = batteryByte.HasFlag(0x20),
                Level = (byte)(batteryByte & 0x0F)
            };
            IsHeadphoneConnected = miscByte.HasFlag(0x01);
        }

        // todo - find a way to differentiate between the "valid" and "broken" BT states - now that stuff is working right,
        // we can always take the USB index. Hold the other one for when we can fix this.
        // this seems to be a discovery issue of some kind, other things (like steam and ds4windows) have no problem finding it.
        // seems to be fixed permanently after using DS4Windows but ideally we shouldn't have to have that precondition,
        // and steam was able to handle it fine before that
        private byte GetModeSwitch(int indexIfUsb, int indexIfBt)
        {
            return indexIfUsb >= 0 ? data[indexIfUsb] : (byte)0;
            //return InputMode switch
            //{
            //    InputMode.USB => indexIfUsb >= 0 ? readData[indexIfUsb] : (byte)0,
            //    InputMode.Bluetooth => indexIfBt >= 0 ? readData[indexIfBt] : (byte)0,
            //    _ => throw new InvalidOperationException("")
            //};
        }

        private byte[] GetModeSwitch(int startIndexIfUsb, int startIndexIfBt, int size)
        {
            return startIndexIfUsb >= 0 ? data.Skip(startIndexIfUsb).Take(size).ToArray() : new byte[size];
            //return InputMode switch
            //{
            //    InputMode.USB => startIndexIfUsb >= 0 ? readData.Skip(startIndexIfUsb).Take(size).ToArray() : new byte[size],
            //    InputMode.Bluetooth => startIndexIfBt >= 0 ? readData.Skip(startIndexIfBt).Take(size).ToArray() : new byte[size],
            //    _ => throw new InvalidOperationException("")
            //};
        }

        private Vec2 ReadAnalogStick(byte x, byte y)
        {
            float x1 = x.ToSignedFloat();
            float y1 = -y.ToSignedFloat();
            return new Vec2
            {
                X = Math.Abs(x1) >= deadZone ? x1 : 0,
                Y = Math.Abs(y1) >= deadZone ? y1 : 0
            };
        }

        private static bool ReadDPadButton(byte b, int v1, int v2, int v3)
        {
            int val = b & 0x0F;
            return val == v1 || val == v2 || val == v3;
        }

        private static Touch ReadTouchpad(byte[] bytes)
        {
            // force everything into the right byte order; input bytes are LSB-first
            if (!BitConverter.IsLittleEndian)
            {
                bytes = bytes.Reverse().ToArray();
            }
            uint raw = BitConverter.ToUInt32(bytes);
            return new Touch
            {
                X = (raw & 0x000FFF00) >> 8,
                Y = (raw & 0xFFF00000) >> 20,
                IsDown = (raw & 128) == 0,
                Id = bytes[0]
            };
        }

        private static Vec3 ReadAccelAxes(byte[] x, byte[] y, byte[] z)
        {
            // force everything into the right byte order; assuming that input bytes is little-endian
            if (!BitConverter.IsLittleEndian)
            {
                x = x.Reverse().ToArray();
                y = y.Reverse().ToArray();
                z = z.Reverse().ToArray();
            }
            return new Vec3
            {
                X = BitConverter.ToInt16(x),
                Y = BitConverter.ToInt16(y),
                Z = BitConverter.ToInt16(z)
            };
        }

        public Vec2 LeftAnalogStick { get; private set; }

        public Vec2 RightAnalogStick { get; private set; }

        public float L2 { get; private set; }

        public float R2 { get; private set; }

        public bool SquareButton { get; private set; }

        public bool CrossButton { get; private set; }

        public bool CircleButton { get; private set; }

        public bool TriangleButton { get; private set; }

        public bool DPadUpButton { get; private set; }

        public bool DPadRightButton { get; private set; }

        public bool DPadDownButton { get; private set; }

        public bool DPadLeftButton { get; private set; }

        public bool L1Button { get; private set; }

        public bool R1Button { get; private set; }

        public bool L2Button { get; private set; }

        public bool R2Button { get; private set; }

        public bool CreateButton { get; private set; }

        public bool MenuButton { get; private set; }

        public bool L3Button { get; private set; }

        public bool R3Button { get; private set; }

        public bool LogoButton { get; private set; }

        public bool TouchpadButton { get; private set; }

        public bool MicButton { get; private set; }

        public Touch Touchpad1 { get; private set; }

        public Touch Touchpad2 { get; private set; }

        // todo - this gets numbers, unclear how correct they are but they do seem to move in the expected direction.
        // they settle back to 0 so it seems like this may be measuring torques (rotational accellerations)
        // document directions, unit unclear. axes/signs may be wrong too, seems like pitch, yaw, and roll may not fit into RHR unless order is switched around
        public Vec3 Gyro { get; private set; }

        // todo - document directions, unit unclear
        public Vec3 Accelerometer { get; private set; }

        public BatteryStatus BatteryStatus { get; private set; }

        public bool IsHeadphoneConnected { get; private set; }
    }
}
