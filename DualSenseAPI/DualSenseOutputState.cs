using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DualSenseAPI.Util;

namespace DualSenseAPI
{
    public class DualSenseOutputState
    {
        public float LeftRumble { get; set; } = 0;

        public float RightRumble { get; set; } = 0;

        public MicLed MicLed { get; set; } = MicLed.Off;

        public PlayerLed PlayerLed { get; set; } = PlayerLed.None;

        public PlayerLedBrightness PlayerLedBrightness { get; set; } = PlayerLedBrightness.High;

        public LightbarBehavior LightbarBehavior { get; set; } = LightbarBehavior.PulseBlue;

        public LightbarColor LightbarColor { get; set; } = new LightbarColor(0, 0, 1);

        public TriggerEffect R2Effect { get; set; } = TriggerEffect.Default;

        public TriggerEffect L2Effect { get; set; } = TriggerEffect.Default;

        // default no-arg constructor
        public DualSenseOutputState() { }

        internal DualSenseOutputState(DualSenseOutputState original)
        {
            LeftRumble = original.LeftRumble;
            RightRumble = original.RightRumble;
            MicLed = original.MicLed;
            PlayerLed = original.PlayerLed;
            PlayerLedBrightness = original.PlayerLedBrightness;
            LightbarBehavior = original.LightbarBehavior;
            // lightbar color is a struct, which is a value type so this assignment is fine.
            LightbarColor = original.LightbarColor;
            // trigger effects all have no setters, making them immutables. this reassignment is fine
            R2Effect = original.R2Effect;
            L2Effect = original.L2Effect;
        }

        private static byte[] BuildTriggerReport(TriggerEffect props)
        {
            byte[] bytes = new byte[10];
            bytes[0] = (byte)props.InternalEffect;
            switch (props.InternalEffect)
            {
                case TriggerEffectType.ContinuousResistance:
                    bytes[1] = props.InternalStartPosition.UnsignedToByte();
                    bytes[2] = props.InternalStartForce.UnsignedToByte();
                    break;
                case TriggerEffectType.SectionResistance:
                    bytes[1] = props.InternalStartPosition.UnsignedToByte();
                    bytes[2] = props.InternalEndPosition.UnsignedToByte();
                    break;
                case TriggerEffectType.Vibrate:
                    bytes[1] = 0xFF;
                    if (props.InternalKeepEffect)
                    {
                        bytes[2] = 0x02;
                    }
                    bytes[4] = props.InternalStartForce.UnsignedToByte();
                    bytes[5] = props.InternalMiddleForce.UnsignedToByte();
                    bytes[6] = props.InternalEndForce.UnsignedToByte();
                    bytes[9] = props.InternalVibrationFrequency;
                    break;
                default:
                    // leave other bytes as 0. this handles Default/No-resist and calibration modes.
                    break;
            }
            return bytes;
        }

        internal byte[] BuildHidOutputBuffer()
        {
            byte[] baseBuf = new byte[47];

            // Feature mask
            baseBuf[0x00] = 0xFF;
            baseBuf[0x01] = 0xF7;

            // L/R rumble
            baseBuf[0x02] = RightRumble.UnsignedToByte();
            baseBuf[0x03] = LeftRumble.UnsignedToByte();

            // mic led
            baseBuf[0x08] = (byte)MicLed;

            // 0x01 to allow customization, 0x02 to enable uninterruptable blue pulse
            baseBuf[0x26] = 0x03;
            // 0x01 to do a slow-fade to blue (uninterruptable) if 0x26 & 0x01 is set.
            // 0x02 to allow a slow-fade-out and set to configured color
            baseBuf[0x29] = (byte)LightbarBehavior;
            baseBuf[0x2A] = (byte)PlayerLedBrightness;
            baseBuf[0x2B] = (byte)(0x20 | (byte)PlayerLed);

            //lightbar
            baseBuf[0x2C] = LightbarColor.R.UnsignedToByte();
            baseBuf[0x2D] = LightbarColor.G.UnsignedToByte();
            baseBuf[0x2E] = LightbarColor.B.UnsignedToByte();

            //adaptive triggers
            byte[] r2Bytes = BuildTriggerReport(R2Effect);
            Array.Copy(r2Bytes, 0, baseBuf, 0x0A, 10);
            byte[] l2Bytes = BuildTriggerReport(L2Effect);
            Array.Copy(l2Bytes, 0, baseBuf, 0x15, 10);

            return baseBuf;
        }
    }
}
