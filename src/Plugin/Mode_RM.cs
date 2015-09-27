using System;
using OpenBveApi.Runtime;

namespace Plugin
{
    internal class Mode_RM : Device
    {
        const float RM_MAX_SPEED_KMH = 18.0f;
        const float RM_MOTOR_CUTOUT_KMH = 16.0f;
        const float RM_LIMITER_RESET_KMH = 15.5f;

        private Train train;
        private bool isForcedCoasting = false; //If the train is actively being cut-off by motor cutout speed.
        private bool isForcedBraking = false; //If the train is actively being braked by motor cutout speed.

        public Mode_RM(Train train)
        {
            this.train = train;
        }

        internal override int? Elapse(ElapseData data)
        {
            //Is train in RM?
            if(train.trainModeActual != Train.TrainModes.RestrictedManualForward && train.trainModeActual != Train.TrainModes.RestrictedManualReverse)
            {
                return null;
            }

            //Perform applications
            if (isForcedBraking && Math.Abs(data.Vehicle.Speed.KilometersPerHour) > RM_LIMITER_RESET_KMH)
            {
                return -train.specs.BrakeNotches;
            }
            else if (isForcedCoasting && Math.Abs(data.Vehicle.Speed.KilometersPerHour) > RM_LIMITER_RESET_KMH)
            {
                return 0;
            }

            //Check if applications are needed
            if (Math.Abs(data.Vehicle.Speed.KilometersPerHour) > RM_MAX_SPEED_KMH)
            {
                isForcedBraking = true;
            }
            else if (Math.Abs(data.Vehicle.Speed.KilometersPerHour) > RM_MOTOR_CUTOUT_KMH)
            {
                isForcedCoasting = true;
            }
            else
            {
                isForcedBraking = false;
                isForcedCoasting = false;
            }

            return null;
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
