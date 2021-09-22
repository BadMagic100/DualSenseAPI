using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualSenseAPI
{
    public struct Touch
    {
        public uint X;
        public uint Y;
        public bool IsDown;
        public byte Id;
    }
}
