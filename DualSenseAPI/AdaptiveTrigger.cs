using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DualSenseAPI
{
    internal enum TriggerEffectType : byte
    {
        ContinuousResistance = 0x01,
        SectionResistance = 0x02,
        Vibrate = 0x26,
        Calibrate = 0xFC,
        Default = 0x00
    }

    public class TriggerEffect
    {
        internal TriggerEffectType InternalEffect { get; private set; } = TriggerEffectType.Default;
        // Used for all trigger effects that apply resistance
        internal float InternalStartPosition { get; private set; } = 0;
        // Used for section resistance
        internal float InternalEndPosition { get; private set; } = 0;
        // Below properties are for EffectEx only
        internal float InternalStartForce { get; private set; } = 0;
        internal float InternalMiddleForce { get; private set; } = 0;
        internal float InternalEndForce { get; private set; } = 0;
        internal bool InternalKeepEffect { get; private set; } = false;
        internal byte InternalVibrationFrequency { get; private set; } = 0;
        private TriggerEffect() { }

        public static readonly TriggerEffect Default = new SimpleEffect(TriggerEffectType.Default);
        public static readonly TriggerEffect Calibrate = new SimpleEffect(TriggerEffectType.Calibrate);

        private sealed class SimpleEffect : TriggerEffect
        {
            public SimpleEffect(TriggerEffectType effect)
            {
                InternalEffect = effect;
            }
        }

        public sealed class Continuous : TriggerEffect
        {
            public float StartPosition
            {
                get { return InternalStartPosition; }
            }

            public float Force
            {
                get { return InternalStartForce; }
            }

            public Continuous(float startPosition, float forcePercentage)
            {
                InternalEffect = TriggerEffectType.ContinuousResistance;
                InternalStartPosition = startPosition;
                InternalStartForce = forcePercentage;
            }
        }

        public sealed class Section : TriggerEffect
        {
            public float StartPosition
            {
                get { return InternalStartPosition; }
            }

            public float EndPosition
            {
                get { return InternalEndPosition; }
            }

            public Section(float startPosition, float endPosition)
            {
                InternalEffect = TriggerEffectType.SectionResistance;
                InternalStartPosition = startPosition;
                InternalEndPosition = endPosition;
            }
        }

        public sealed class Vibrate : TriggerEffect
        {
            public float StartForce { get { return InternalStartForce; } }
            public float MiddleForce { get { return InternalMiddleForce; } }
            public float EndForce { get { return InternalEndForce; } }
            public bool KeepEffect { get { return InternalKeepEffect; } }
            public byte VibrationFrequency { get { return InternalVibrationFrequency; } }

            public Vibrate(byte vibrationFreqHz, float startForce, float middleForce, float endForce, bool keepEffect = true)
            {
                InternalEffect = TriggerEffectType.Vibrate;
                InternalStartForce = startForce;
                InternalMiddleForce = middleForce;
                InternalEndForce = endForce;
                InternalKeepEffect = keepEffect;
                InternalVibrationFrequency = vibrationFreqHz;
            }
        }
    }
}
