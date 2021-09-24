using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Device.Net;
using Hid.Net.Windows;

namespace DualSenseAPI.Util
{
    internal class HidScanner
    {
        private readonly IDeviceFactory hidFactory;

        private static HidScanner? _instance = null;
        internal static HidScanner Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new HidScanner();
                }
                return _instance;
            }
        }

        private HidScanner()
        {
            hidFactory = new FilterDeviceDefinition(1356, 3302, label: "DualSense").CreateWindowsHidDeviceFactory();
        }

        public IEnumerable<ConnectedDeviceDefinition> ListDevices()
        {
            Task<IEnumerable<ConnectedDeviceDefinition>> scannerTask = hidFactory.GetConnectedDeviceDefinitionsAsync();
            scannerTask.Wait();
            return scannerTask.Result;
        }

        public IDevice GetConnectedDevice(ConnectedDeviceDefinition deviceDefinition)
        {
            Task<IDevice> connectTask = hidFactory.GetDeviceAsync(deviceDefinition);
            connectTask.Wait();
            return connectTask.Result;
        }
    }
}
