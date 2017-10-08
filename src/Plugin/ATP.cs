using System;
using OpenBveApi.Runtime;

namespace Plugin
{
    internal class ATP : Device
    {
        const float ATP_SAFETY_DECELERATION_RATE = -0.70f; //m/s
        const float ATP_SAFETY_STOPPING_DISTANCE = 40.0f; //Buffer distance to next train

        const float ATP_TARGET_DECELERATION_RATE = -0.50f; //m/s
        const float ATP_TARGET_STOPPING_DISTANCE = 75.0f;

        const float ATP_RESTART_DISTANCE = 20.0f; //Distance for preceding train to move before moving off

        const bool ATP_FORCE_STATION_STOP = true; //Bring speed code down to 0 at every station

        private Train train;

        private double currentLocation;

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
            currentLocation = data.Vehicle.Location;

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
                if (data.Vehicle.Speed.KilometersPerHour <= train.atpTrackSafetySpeed)
                {
                    atpTripTimer = null;
                }
            }
            else if (data.Vehicle.Speed.KilometersPerHour > train.atpTrackSafetySpeed)
            {
                atpTripTimer = ATP_WARNING_DURATION;
            }
        }

        internal void UpdateAtpSpeeds(ElapseData data, PrecedingVehicleState precedingVehicle)
        {
            if (train.atpTrackNextSpeedPosition >= data.Vehicle.Location)
            {
                double newTargetSpeed = CalculateSpeedToStop(ATP_TARGET_DECELERATION_RATE, (train.atpTrackNextSpeedPosition - data.Vehicle.Location), train.atpTrackNextTargetSpeed);
                double newSafetySpeed = CalculateSpeedToStop(ATP_SAFETY_DECELERATION_RATE, (train.atpTrackNextSpeedPosition - data.Vehicle.Location), train.atpTrackNextSafetySpeed);

                train.atpTrackTargetSpeed = Math.Min(newTargetSpeed, train.atpTrackTargetSpeed);
                train.atpTrackSafetySpeed = Math.Min(newSafetySpeed, train.atpTrackSafetySpeed);
            }
            else
            {
            }

            if (precedingVehicle == null)
            {
                train.atpTargetSpeed = train.atpTrackTargetSpeed;
                train.atpSafetySpeed = train.atpTrackSafetySpeed;
            }
            else
            {
                double distanceToStop = precedingVehicle.Distance - ATP_TARGET_STOPPING_DISTANCE;

                train.atpTargetSpeed = Math.Max(Math.Min(CalculateSpeedToStop(ATP_TARGET_DECELERATION_RATE, distanceToStop), train.atpTrackTargetSpeed), 0.0);
                if(Double.IsNaN(train.atpTargetSpeed))
                {
                    throw new ApplicationException();
                }
                
                train.atpSafetySpeed = Math.Min(CalculateSpeedToStop(ATP_SAFETY_DECELERATION_RATE, (precedingVehicle.Distance - ATP_SAFETY_STOPPING_DISTANCE)), train.atpTrackSafetySpeed);
                if (Double.IsNaN(train.atpSafetySpeed))
                {
                    throw new ApplicationException();
                }
            }
        }
        internal double CalculateSpeedToStop(double acceleration, double distance, double speed = 0)
        {
            speed = speed / 3.6; //Convert to m/s

            double result = Math.Sqrt((2 * -acceleration * distance) + (speed * speed));

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
            if(beacon.Type == 32) //Upcoming target and safety track speeds
            {
                train.atpTrackNextSpeedPosition = (int)(beacon.Optional / 1000000) + currentLocation;
                train.atpTrackNextTargetSpeed = (int)((int)(beacon.Optional / 1000) % 1000);
                train.atpTrackNextSafetySpeed = (int)(beacon.Optional % 1000);
            }
            if (beacon.Type == 31) //Target and safety track speeds
            {
                train.atpTrackTargetSpeed = (int)(beacon.Optional / 1000);
                train.atpTrackSafetySpeed = (int)(beacon.Optional % 1000);
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
