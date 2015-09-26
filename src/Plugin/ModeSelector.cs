using System;
using OpenBveApi.Runtime;

namespace Plugin
{
    internal class ModeSelector : Device
    {
        private Train train;
        private Train.TrainModes trainModePending = Train.TrainModes.Off;

        public ModeSelector(Train train)
        {
            this.train = train;
        }

        internal override int? Elapse(ElapseData data)
        {
            if (train.trainModeSelected != train.trainModeActual)
            {
                trainModePending = train.trainModeSelected;
            }
            else
            {
                return null;
            }

            if (data.Vehicle.Speed.KilometersPerHour > 0.5 || data.Handles.BrakeNotch < train.specs.BrakeNotches+1)
            {
                return -train.specs.BrakeNotches - 1;
            }
            else
            {
                train.trainModeActual = trainModePending;
                return null;
            }
        }

        internal override void HornBlow(HornTypes type)
        {
            
        }

        internal override void Initialize(InitializationModes mode)
        {
            
        }

        internal override void KeyDown(VirtualKeys key)
        {
            
        }

        internal override void KeyUp(VirtualKeys key)
        {
            
        }

        internal override void SetBeacon(BeaconData beacon)
        {
            
        }

        internal override void SetBrake(int brakeNotch)
        {
            
        }

        internal override void SetPower(int powerNotch)
        {
            
        }

        internal override void SetReverser(int reverser)
        {
            
        }

        internal override void SetSignal(SignalData[] signal)
        {
            
        }
    }
}
