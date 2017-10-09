using System;
using OpenBveApi.Runtime;

namespace Plugin
{
    internal class Interlock : Device
    {
        private Train train;

        public Interlock(Train train)
        {
            this.train = train;
        }

        internal override int? Elapse(ElapseData data)
        {
            if(this.train.doorState != DoorStates.None)
            {
                if(Math.Abs(data.Vehicle.Speed.KilometersPerHour) > 0.2)
                {
                    return -this.train.specs.BrakeNotches - 1;
                }
                else
                {
                    return -this.train.specs.BrakeNotches;
                }
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

        internal override void DoorChange(DoorStates oldState, DoorStates newState)
        {

        }
    }
}
