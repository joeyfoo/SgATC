using System;
using OpenBveApi.Runtime;

namespace Plugin
{
    internal class ModeSelector : Device
    {
        private Train train;
        private Train.TrainModes? trainModeNew = null;
        const float CHANGE_MODE_TIMER = 2.0f;
        private float changeModeTimer = 0;

        public ModeSelector(Train train)
        {
            this.train = train;
        }

        internal override int? Elapse(ElapseData data)
        {
            if (trainModeNew != null)
            {
                train.trainModeSelected = (Train.TrainModes)trainModeNew;
                changeModeTimer = CHANGE_MODE_TIMER;
                trainModeNew = null;
            }

            if (train.trainModeActual != train.trainModeSelected)
            {
                if (IsModeChangable(data))
                {
                    if (changeModeTimer > 0)
                    {
                        changeModeTimer -= (float)data.ElapsedTime.Seconds;
                    }
                    else
                    {
                        changeModeTimer = 0.0f;
                        train.trainModeActual = train.trainModeSelected;
                    }
                }
                else
                {
                    return -train.specs.BrakeNotches;
                }
            }
            return null;
        }

        private Boolean IsModeChangable(ElapseData data)
        {
            return data.Vehicle.Speed.KilometersPerHour < 0.01 && data.Handles.BrakeNotch >= train.specs.B67Notch;
        }

        internal override void HornBlow(HornTypes type)
        {
            
        }

        internal override void Initialize(InitializationModes mode)
        {
            
        }

        internal override void KeyDown(VirtualKeys key)
        {
            if (key == VirtualKeys.C1) //Mode Up
            {
                if ((int)train.trainModeSelected < train.trainModeCount - 1)
                {
                    trainModeNew = train.trainModeSelected + 1;
                }
            }
            else if (key == VirtualKeys.C2) //Mode Down
            {
                if ((int)train.trainModeSelected > 0)
                {
                    trainModeNew = train.trainModeSelected - 1;
                }
            }


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

        internal override void DoorChange(DoorStates oldState, DoorStates newState)
        {

        }
    }
}
