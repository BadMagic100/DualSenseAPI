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

        private readonly IDevice underlyingDevice;
        private IDisposable pollerSubscription;
        private readonly int? readBufferSize;
        private readonly int? writeBufferSize;

        public IoMode IoMode { get; private set; }

        // should input state go here? seems kinda whack that output state is the controller instance, but the input state is only available as a return state

        public float JoystickDeadZone { get; set; } = 0;

        public float LeftRumble { get; set; } = 0;

        public float RightRumble { get; set; } = 0;

        public MicLed MicLed { get; set; } = MicLed.Off;

        public PlayerLed PlayerLed { get; set; } = PlayerLed.None;

        public PlayerLedBrightness PlayerLedBrightness { get; set; } = PlayerLedBrightness.High;

        public LightbarBehavior LightbarBehavior { get; set; } = LightbarBehavior.PulseBlue;

        public LightbarColor LightbarColor { get; set; } = new LightbarColor(0, 0, 1);

        public TriggerEffect R2Effect { get; set; } = TriggerEffect.Default;

        public TriggerEffect L2Effect { get; set; } = TriggerEffect.Default;

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

        private async Task<DualSenseState> ReadWriteOnceAsync()
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
                return new DualSenseState(result.Data.Skip(offset).ToArray(), IoMode, JoystickDeadZone);
            }
            else
            {
                throw new IOException("Failed to read data - buffer size mismatch");
            }
        }

        public DualSenseState ReadWriteOnce()
        {
            Task<DualSenseState> stateTask = ReadWriteOnceAsync();
            stateTask.Wait();
            return stateTask.Result;
        }

        public void BeginPolling(uint pollingIntervalMs, Action<DualSenseState> onState)
        {
            IObservable<DualSenseState> stateObserver = Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(pollingIntervalMs))
                .SelectMany(Observable.FromAsync(() => ReadWriteOnceAsync()));
            // todo - figure how we can leverage DistinctUntilChanged (or similar) so we can do filtered eventing (e.g. button pressed only)

            pollerSubscription = stateObserver.Subscribe(onState);
        }

        public void EndPolling()
        {
            pollerSubscription.Dispose();
            pollerSubscription = null;
        }

        private byte[] BuildTriggerReport(TriggerEffect props)
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

        private byte[] BuildHidOutputBuffer()
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

        private byte[] GetOutputDataBytes()
        {
            byte[] bytes = new byte[writeBufferSize ?? 0];
            if (IoMode == IoMode.USB)
            {
                bytes[0] = 0x02;
                Array.Copy(BuildHidOutputBuffer(), 0, bytes, 1, 47);
            }
            else if (IoMode == IoMode.Bluetooth)
            {
                bytes[0] = 0x31;
                bytes[1] = 0x02;
                Array.Copy(BuildHidOutputBuffer(), 0, bytes, 2, 47);
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
