using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualSenseAPI
{
    public enum PlayerLed
    {
        None = 0x00,
        Left = 0x01,
        MiddleLeft = 0x02,
        Middle = 0x04,
        MiddleRight = 0x08,
        Right = 0x10,
        Player1 = Middle,
        Player2 = MiddleLeft | MiddleRight,
        Player3 = Left | Middle | Right,
        Player4 = Left | MiddleLeft | MiddleRight | Right,
        All = Left | MiddleLeft | Middle | MiddleRight | Right
    }

    public enum PlayerLedBrightness
    {
        Low = 0x02,
        Medium = 0x01,
        High = 0x00
    }

    public enum LightbarBehavior
    {
        PulseBlue = 0x1,
        CustomColor = 0x2
    }

    public struct LightbarColor
    {
        public float R { get; set; }
        public float G { get; set; }
        public float B { get; set; }

        public LightbarColor(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }
    }

    public enum MicLed
    {
        Off = 0,
        On = 1,
        Pulse = 2
    }
}
