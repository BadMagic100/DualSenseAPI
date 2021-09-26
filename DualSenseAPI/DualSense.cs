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
    /// <summary>
    /// A handler for a state polling IO event
    /// </summary>
    /// <param name="currentInputState">The most recently polled input state.</param>
    /// <param name="currentOutputState">The most recently sent output state.</param>
    /// <returns>The next output state to send, or null if the output state shouldn't change</returns>
    public delegate DualSenseOutputState? StateHandler(DualSenseInputState currentInputState, DualSenseOutputState currentOutputState);

    public class DualSense
    {
        // IO parameters
        private readonly IDevice underlyingDevice;
        private readonly int? readBufferSize;
        private readonly int? writeBufferSize;

        // async polling
        private IDisposable? pollerSubscription;
        private Func<DualSenseInputState, DualSenseOutputState, DualSenseOutputState?>? onState;

        /// <summary>
        /// The I/O mode the controller is connected by.
        /// </summary>
        public IoMode IoMode { get; private set; }

        /// <summary>
        /// Configurable dead zone for gamepad joysticks. A joystick axis with magnitude less than this value will
        /// be returned as 0.
        /// </summary>
        public float JoystickDeadZone { get; set; } = 0;

        // TODO: how do we deal with the (im)mutability of this?
        /// <summary>
        /// This controller's output state.
        /// </summary>
        public DualSenseOutputState OutputState { get; set; } = new DualSenseOutputState();

        /// <summary>
        /// This controller's most recently polled input state.
        /// </summary>
        public DualSenseInputState? InputState { get; private set; } = null;

        /// <summary>
        /// Private constructor for <see cref="EnumerateControllers"/>.
        /// </summary>
        /// <param name="underlyingDevice"></param>
        /// <param name="readBufferSize"></param>
        /// <param name="writeBufferSize"></param>
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

        /// <summary>
        /// Acquires the controller.
        /// </summary>
        public void Acquire()
        {
            if (!underlyingDevice.IsInitialized)
            {
                underlyingDevice.InitializeAsync().Wait();
            }
        }

        /// <summary>
        /// Releases the controller.
        /// </summary>
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
                    0x01 => 1, // USB packet flag
                    0x31 => 2, // Bluetooth packet flag
                    _ => 0
                };
                return new DualSenseInputState(result.Data.Skip(offset).ToArray(), IoMode, JoystickDeadZone);
            }
            else
            {
                throw new IOException("Failed to read data - buffer size mismatch");
            }
        }

        /// <summary>
        /// Updates the input and output states once. This operation is blocking.
        /// </summary>
        /// <returns>The polled state, for convenience. This is also updated on the controller instance.</returns>
        public DualSenseInputState ReadWriteOnce()
        {
            Task<DualSenseInputState> stateTask = ReadWriteOnceAsync();
            stateTask.Wait();
            InputState = stateTask.Result;
            return InputState;
        }

        /// <summary>
        /// Process a state event. Wraps around user-provided handler since Reactive needs an Action<>.
        /// </summary>
        /// <param name="inputState">The receieved input state</param>
        private void ProcessState(DualSenseInputState inputState)
        {
            if (onState == null)
            {
                throw new InvalidOperationException("Can't handle state without a handler");
            }
            // TODO: dislike that this modifies output state on the instance, but not the input state. would prefer this doesn't modify instance state at all,
            // so the polling is independent of the instance's sync state entirely, aside from the subscription. could settle for updating both states on the
            // instance, would need to be thread-safe

            // pass a copy so we're not modifying the current state by reference. we'll reassign when done. would be nice if these were properly immutable,
            // but that causes problems almost everywhere we use it
            OutputState = onState(inputState, new DualSenseOutputState(OutputState)) ?? OutputState;
        }

        /// <summary>
        /// Begins asynchously updating the output state and polling the input state at the specified interval.
        /// </summary>
        /// <param name="pollingIntervalMs">How long to wait between each I/O loop, in milliseconds</param>
        /// <param name="onState">The state handler</param>
        public void BeginPolling(uint pollingIntervalMs, Func<DualSenseInputState, DualSenseOutputState, DualSenseOutputState?> onState)
        {
            if (pollerSubscription != null)
            {
                throw new InvalidOperationException("Can't begin polling after it's already started.");
            }
            this.onState = onState;

            IObservable<DualSenseInputState> stateObserver = Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(pollingIntervalMs))
                .SelectMany(Observable.FromAsync(() => ReadWriteOnceAsync()));
            // TODO: figure how we can leverage DistinctUntilChanged (or similar) so we can do filtered eventing (e.g. button pressed only)
            // how do we allow both to modify state in a smart way (i.e. without overriding each other?)

            pollerSubscription = stateObserver.Subscribe(ProcessState);
        }

        /// <summary>
        /// Stop asynchronously updating the output state and polling for new inputs.
        /// </summary>
        public void EndPolling()
        {
            if (pollerSubscription == null)
            {
                throw new InvalidOperationException("Can't end polling without starting polling first");
            }
            pollerSubscription.Dispose();
            pollerSubscription = null;
            onState = null;
        }

        /// <summary>
        /// Builds the output byte array that will be sent to the controller.
        /// </summary>
        /// <returns>An array of bytes to send to the controller</returns>
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

        /// <summary>
        /// Enumerates available controllers.
        /// </summary>
        /// <returns>Enumerable of available controllers.</returns>
        public static IEnumerable<DualSense> EnumerateControllers()
        {
            foreach (ConnectedDeviceDefinition deviceDefinition in HidScanner.Instance.ListDevices())
            {
                IDevice device = HidScanner.Instance.GetConnectedDevice(deviceDefinition);
                yield return new DualSense(device, deviceDefinition.ReadBufferSize, deviceDefinition.WriteBufferSize);
            }
        }
    }
}
