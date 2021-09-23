using Device.Net;
using DualSenseAPI.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualSenseAPI
{
    public class DualSense
    {
        private static HidScanner _hidScannerSingleton = null;
        private static HidScanner HidScannerSingleton
        {
            get
            {
                if (_hidScannerSingleton == null)
                {
                    _hidScannerSingleton = new HidScanner();
                }
                return _hidScannerSingleton;
            }
        }

        // IO state
        private readonly IDevice underlyingDevice;
        private readonly int? readBufferSize;
        private readonly int? writeBufferSize;

        // async polling
        private IDisposable pollerSubscription;
        private Func<DualSenseInputState, DualSenseOutputState, DualSenseOutputState> onState;

        public IoMode IoMode { get; private set; }

        public float JoystickDeadZone { get; set; } = 0;

        // how do we deal with the (im)mutability of this?
        public DualSenseOutputState OutputState { get; set; } = new DualSenseOutputState();

        public DualSenseInputState InputState { get; private set; } = null;

        private DualSense(IDevice underlyingDevice, int? readBufferSize, int? writeBufferSize)
        {
            this.underlyingDevice = underlyingDevice;
            this.readBufferSize = readBufferSize;
            this.writeBufferSize = writeBufferSize;
            IoMode = readBufferSize switch
            {
                64 => IoMode.USB,
                78 => IoMode.Bluetooth,
                _ => IoMode.Unknown
            };
            if (IoMode == IoMode.Unknown)
            {
                throw new InvalidOperationException("Can't initialize device - supported IO modes are USB and Bluetooth.");
            }
        }

        public void Connect()
        {
            if (!underlyingDevice.IsInitialized)
            {
                underlyingDevice.InitializeAsync().Wait();
            }
        }

        public void Release()
        {
            if (underlyingDevice.IsInitialized)
            {
                underlyingDevice.Close();
            }
        }

        private async Task<DualSenseInputState> ReadWriteOnceAsync()
        {
            TransferResult result = await underlyingDevice.WriteAndReadAsync(GetOutputDataBytes());
            if (result.BytesTransferred == readBufferSize)
            {
                // this can effectively determine which input packet you've recieved, USB or bluetooth, and offset by the right amount
                int offset = result.Data[0] switch
                {
                    0x01 => 1,
                    0x31 => 2,
                    _ => 0
                };
                return new DualSenseInputState(result.Data.Skip(offset).ToArray(), IoMode, JoystickDeadZone);
            }
            else
            {
                throw new IOException("Failed to read data - buffer size mismatch");
            }
        }

        public DualSenseInputState ReadWriteOnce()
        {
            Task<DualSenseInputState> stateTask = ReadWriteOnceAsync();
            stateTask.Wait();
            InputState = stateTask.Result;
            return InputState;
        }

        private void ProcessState(DualSenseInputState inputState)
        {
            // pass a copy so we're not modifying the current state by reference. we'll reassign when done.
            OutputState = onState(inputState, new DualSenseOutputState(OutputState));
        }

        public void BeginPolling(uint pollingIntervalMs, Func<DualSenseInputState, DualSenseOutputState, DualSenseOutputState> onState)
        {
            this.onState = onState;

            IObservable<DualSenseInputState> stateObserver = Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(pollingIntervalMs))
                .SelectMany(Observable.FromAsync(() => ReadWriteOnceAsync()));
            // todo - figure how we can leverage DistinctUntilChanged (or similar) so we can do filtered eventing (e.g. button pressed only)

            // how to allow return values on these subscriptions? ideally we'd like onState to be a pure function that takes the current IO states,
            // and returns a new output state without doing any modification by reference
            pollerSubscription = stateObserver.Subscribe(ProcessState);
        }

        public void EndPolling()
        {
            pollerSubscription.Dispose();
            pollerSubscription = null;
            onState = null;
        }

        private byte[] GetOutputDataBytes()
        {
            byte[] bytes = new byte[writeBufferSize ?? 0];
            byte[] hidBuffer = OutputState.BuildHidOutputBuffer();
            if (IoMode == IoMode.USB)
            {
                bytes[0] = 0x02;
                Array.Copy(hidBuffer, 0, bytes, 1, 47);
            }
            else if (IoMode == IoMode.Bluetooth)
            {
                bytes[0] = 0x31;
                bytes[1] = 0x02;
                Array.Copy(hidBuffer, 0, bytes, 2, 47);
                // make a 32 bit checksum of the first 74 bytes and add it at the end
                uint crcChecksum = CRC32Utils.ComputeCRC32(bytes, 74);
                byte[] checksumBytes = BitConverter.GetBytes(crcChecksum);
                Array.Copy(checksumBytes, 0, bytes, 74, 4);
            }
            else
            {
                throw new InvalidOperationException("Can't send data - supported IO modes are USB and Bluetooth.");
            }
            return bytes;
        }

        public override string ToString()
        {
            return $"DualSense Controller ({IoMode})";
        }

        public static IEnumerable<DualSense> EnumerateControllers()
        {
            foreach (ConnectedDeviceDefinition deviceDefinition in HidScannerSingleton.ListDevices())
            {
                IDevice device = HidScannerSingleton.GetConnectedDevice(deviceDefinition);
                yield return new DualSense(device, deviceDefinition.ReadBufferSize, deviceDefinition.WriteBufferSize);
            }
        }
    }
}
