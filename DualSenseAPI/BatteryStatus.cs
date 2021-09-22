using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualSenseAPI
{
    public struct BatteryStatus
    {
        public bool IsCharging;
        public bool IsFullyCharged;
        public float Level;
    }
}
