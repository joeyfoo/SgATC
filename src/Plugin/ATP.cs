using System;
using OpenBveApi.Runtime;

namespace Plugin
{
    internal class ATP : Device
    {
        const float ATP_TARGET_DECELERATION_RATE = -0.65f; //m/s
        const float ATP_SAFETY_DECELERATION_RATE = -0.80f; //m/s


        private Train train;

        private enum AtpStates
        {
            Off,
            Active,
            Tripped
        }
        private double ATP_WARNING_DURATION = 3.0;
        private AtpStates atpState = AtpStates.Off;
        double? atpTripTimer = null;

        public ATP(Train train)
        {
            this.train = train;
        }

        internal override int? Elapse(ElapseData data)
        {
            //Calculate signalling status
            //Distance to next train
            UpdateAtpSpeeds(data, data.PrecedingVehicle);

            //Check trip count
            //Check for speed limits
            ElapseTripTimer(data);
            
            //Is train in RM?
            if (train.trainModeActual == Train.TrainModes.RestrictedManualForward ||
                train.trainModeActual == Train.TrainModes.RestrictedManualReverse ||
                train.trainModeActual == Train.TrainModes.Off)
            {
                atpState = AtpStates.Off;
            }

            switch (atpState)
            {
                case AtpStates.Active:
                    if (atpTripTimer <= 0.0)
                    {
                        atpState = AtpStates.Tripped;
                    }
                    return null;
                case AtpStates.Tripped:

                    return -train.specs.BrakeNotches - 1;
                default:
                    return null;
            }
        }

        internal void ElapseTripTimer(ElapseData data)
        {
            if (atpTripTimer != null)
            {
                atpTripTimer -= data.ElapsedTime.Seconds;
                atpTripTimer = Math.Max(atpTripTimer ?? 0, 0);
                if (data.Vehicle.Speed.KilometersPerHour <= train.atpSafetySpeed)
                {
                    atpTripTimer = null;
                }
            }
            else if (data.Vehicle.Speed.KilometersPerHour > train.atpSafetySpeed)
            {
                atpTripTimer = ATP_WARNING_DURATION;
            }
        }

        internal void UpdateAtpSpeeds(ElapseData data, PrecedingVehicleState precedingVehicle)
        {
            if(precedingVehicle == null)
            {
                train.atpTargetSpeed = train.atpTrackSpeed;
            }
            else
            {
                double distanceToStop = precedingVehicle.Distance - 50.0;
                train.atpTargetSpeed = Math.Max(Math.Min(CalculateSpeedToStop(ATP_TARGET_DECELERATION_RATE, distanceToStop), train.atpTrackSpeed), 0.0);
                if(Double.IsNaN(train.atpTargetSpeed))
                {
                    throw new ApplicationException();
                }
            }
        }
        internal double CalculateSpeedToStop(double acceleration, double distance)
        {
            double result = Math.Sqrt(2 * -acceleration * distance);

            if(Double.IsNaN(result))
            {
                return 0;
            }

            return result*3.6; //Convert to km/h
        }
        internal double CalculateDistanceToStop(double acceleration, double speed)
        {
            if (acceleration == 0.0)
            {
                return 0.0;
            }
            return -(speed * speed) / (2 * acceleration);
        }

        internal override void Initialize(InitializationModes mode)
        {
        }

        internal override void HornBlow(HornTypes type)
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
            if (beacon.Type == 33)
            {
                //atoReceivedData = beacon.Optional;
            }
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
