using System;
using OpenBveApi.Runtime;

namespace Plugin
{
    internal class Mode_ATO : Device
    {
        //C751A
        /*const float ACCELERATION_POLL_RATE = 0.0f; //sec
        const float ATO_TARGET_DECELERATION_RATE = -0.7f; //m/s
        const float ATO_TIME_BETWEEN_NOTCH_CHANGE = 0.10f;
        const float ATO_FRICTION_BRAKING_CUT_IN = 3.0f; //m/s
        const float ATO_BRAKING_TOLERANCE = 0.75f; //m
        const float ATO_LEVELLING_SPEED = 1.25f; //m/s
        const float ATO_LEVELLING_DECELERATION_RATE = -0.30f;
        const float ATO_LEVELLING_DISTANCE = 5.0f;
        const float ATO_LEVELLING_TOLERANCE = 0.1f; //m
        const float ATO_LEVELLING_CRAWL_SPEED = 0.5f; //m/s
        const float ATO_TIME_BETWEEN_LEVELLING_NOTCH_CHANGE = 0.10f;
        const float ATO_POWERING_AMOUNT = 0.5f;
        const float ATO_BRAKING_AMOUNT = 0.1f;
        const float ATO_READY_TIMER = 1.0f;*/

        //C830
        const float ACCELERATION_POLL_RATE = 0.0f; //sec
        const float ATO_TARGET_DECELERATION_RATE = -0.65f; //m/s
        const float ATO_TIME_BETWEEN_NOTCH_CHANGE = 0.15f;
        const float ATO_FRICTION_BRAKING_CUT_IN = 3.0f; //m/s
        const float ATO_BRAKING_TOLERANCE = 0.5f; //m
        const float ATO_LEVELLING_SPEED = 1.0f; //m/s
        const float ATO_LEVELLING_DECELERATION_RATE = -0.30f;
        const float ATO_LEVELLING_DISTANCE = 3.0f;
        const float ATO_LEVELLING_TOLERANCE = 0.2f; //m
        const float ATO_LEVELLING_CRAWL_SPEED = 0.5f; //m/s
        const float ATO_TIME_BETWEEN_LEVELLING_NOTCH_CHANGE = 0.10f;
        const float ATO_POWERING_AMOUNT = 0.5f;
        const float ATO_BRAKING_AMOUNT = 0.1f;
        const float ATO_READY_TIMER = 1.0f;

        private enum AtoStates
        {
            Ready,
            Enroute,
            Stopped
        }
        private AtoStates atoState = AtoStates.Ready;

        private Train train;
        private double accelerationRate = 0.0;
        private double accelerationLastSpeed = 0.0;
        private Time accelerationLastPoll = new Time(0.0);
        private double notchLastChange = 0;
        private double readyTimer = 0.0;

        private double? atoStoppingPosition = null;
        private int? atoReceivedData = null;
        internal int? atoDemands = null;

        private double? lastFrameTrackPosition = null;
        const float STATION_JUMP_THRESHOLD_DISTANCE = 200;

        public Mode_ATO(Train train)
        {
            this.train = train;
        }

        internal override int? Elapse(ElapseData data)
        {
            train.debugMessage = "";

            //Detect jump to station
            if(lastFrameTrackPosition != null)
            {
                if(Math.Abs(lastFrameTrackPosition.Value - data.Vehicle.Location) > STATION_JUMP_THRESHOLD_DISTANCE)
                {
                    //Reset ATO stopping pos
                    atoStoppingPosition = null;
                }
            }
            lastFrameTrackPosition = data.Vehicle.Location;

            //Is train in Auto?
            if (train.trainModeSelected != Train.TrainModes.Auto)
            {
                atoState = AtoStates.Stopped;
                return null;
            }

            if (atoState == AtoStates.Stopped)
            {
                atoStoppingPosition = null;
                return -train.specs.BrakeNotches;
            }

            //Calculate current acceleration rate
            if (data.TotalTime.Seconds - accelerationLastPoll.Seconds >= ACCELERATION_POLL_RATE)
            {
                UpdateAcceleration(data);
            }

            //Calculate distance to stop
            double? distanceToNextStop = null;
            if (atoStoppingPosition != null)
            {
                distanceToNextStop = atoStoppingPosition - data.Vehicle.Location;
            }

            train.debugMessage = $"{atoState}";

            if (atoState == AtoStates.Ready)
            {
                //At station counting down to departure
                atoDemands = -train.specs.BrakeNotches;

                readyTimer -= data.ElapsedTime.Seconds;

                if (readyTimer <= 0.0)
                {
                    atoState = AtoStates.Enroute;
                }

                //Reset stop pos if closer than 25m
                else if (atoStoppingPosition != null && distanceToNextStop < 25)
                {
                    atoStoppingPosition = null;
                }
            }
            else if (atoState == AtoStates.Enroute)
            {
                //Travelling to next station

                //Get the train to stick to the ATP Target Speed
                int atoTargetSpeedDemand = CalculateTargetSpeedNotchChange(data);

                //Run function for stopping at station
                int? atoStoppingDemand = null;
                if (atoStoppingPosition != null)
                {
                    //Call a function that returns the desired brake/power notch (or null if none)
                    atoStoppingDemand = CalculateStationStopNotchChange(data, distanceToNextStop ?? 0);
                }

                //Select the lowest notch between "TASC" and the target speed code above
                int notch = (atoDemands ?? 0) + Math.Min(atoStoppingDemand ?? train.specs.PowerNotches, atoTargetSpeedDemand);

                //Limit powering below 2km/h
                if(data.Vehicle.Speed.KilometersPerHour < 2.0)
                {
                    notch = Math.Min(notch, 1);
                }

                //Call ChangeAtoDemands with appropriate time between notch 
                ChangeAtoDemands(data, notch, ATO_TIME_BETWEEN_NOTCH_CHANGE, true, true);

                train.debugMessage = $"Enroute. TS {atoTargetSpeedDemand}, TASC {atoStoppingDemand}. Req {notch}. Actual {atoDemands}. Target speed {train.atpTargetSpeed}";
            }

            if (atoReceivedData != null)
            {
                atoStoppingPosition = atoReceivedData + data.Vehicle.Location;
                atoReceivedData = null;
            }

            //train.debugMessage = $" Ato: {atoState} demanding {atoDemands} for {train.atpTargetSpeed}";
            return atoDemands;
        }

        internal int CalculateTargetSpeedNotchChange(ElapseData data)
        {
            double speedDifference = train.atpTargetSpeed - data.Vehicle.Speed.KilometersPerHour;
            
            if (train.atpTargetSpeed <= 3.0 && data.Vehicle.Speed.KilometersPerHour <= 3.0)
            {
                return -1;
            }
            else if (train.atpTargetSpeed < data.Vehicle.Speed.KilometersPerHour)
            {
                int notch = Math.Min(0, (int)(speedDifference / ATO_BRAKING_AMOUNT) - 1); 
                return notch - atoDemands??0;
            }
            else
            {
                int notch = Math.Max(0, (int)(speedDifference / ATO_POWERING_AMOUNT));
                return notch - atoDemands??0;
            }
        }

        internal int CalculateStationStopNotchChange(ElapseData data, double distanceToNextStop)
        {
            //Returns notch adjustment 
            double speed = data.Vehicle.Speed.MetersPerSecond;

            if (speed > ATO_LEVELLING_SPEED)
            {
                double stoppingDistance = CalculateDecelerationDistance(data.Vehicle.Speed.MetersPerSecond, ATO_TARGET_DECELERATION_RATE) ?? 0;

                if (stoppingDistance < distanceToNextStop - ATO_LEVELLING_DISTANCE - ATO_BRAKING_TOLERANCE)
                {
                    return 1;
                }
                else if (stoppingDistance > distanceToNextStop - ATO_LEVELLING_DISTANCE + ATO_BRAKING_TOLERANCE)
                {
                    return -1;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                double stoppingDistance = CalculateDecelerationDistance(data.Vehicle.Speed.MetersPerSecond, ATO_LEVELLING_DECELERATION_RATE) ?? 0;

                if (stoppingDistance < distanceToNextStop - ATO_LEVELLING_TOLERANCE)
                {
                    if(data.Vehicle.Speed.MetersPerSecond > ATO_LEVELLING_CRAWL_SPEED && atoDemands >= 0)
                    {
                        return 0;
                    }
                    else
                    {
                        return 1;
                    }
                }
                else if (stoppingDistance > distanceToNextStop + ATO_LEVELLING_TOLERANCE)
                {
                    return -1;
                }
                else
                {
                    return 0;
                }
            }


        }

        internal double? CalculateDecelerationDistance(double initialSpeed, double acceleration, double targetSpeed = 0)
        {
            if (acceleration >= 0.0)
            {
                return null;
            }
            else
            {
                double distance = ((targetSpeed * targetSpeed) - (initialSpeed * initialSpeed)) / (acceleration * 2);
                return distance;
            }
        }

        /*internal int CalculateStationStop(ElapseData data, double tolerence, double targetStoppingPosition)
        {
            double projectedStoppingPosition = data.Vehicle.Location +
                CalculateDistanceToStop(accelerationRate, data.Vehicle.Speed.MetersPerSecond);

            if (projectedStoppingPosition > targetStoppingPosition || accelerationRate > 0)
            {
                return -1;
            }
            else if (projectedStoppingPosition < (targetStoppingPosition - tolerence))
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }*/

        internal bool ChangeAtoDemands(ElapseData data, int newNotch, double frequency, bool clampToServiceBraking = true, bool clampToTargetDecelerationRate = false)
        {
            double currentTime = data.TotalTime.Seconds;

            if (currentTime - notchLastChange >= frequency)
            {
                int newDemand = Math.Max((int)atoDemands - 1, Math.Min((int)atoDemands + 1, newNotch));
                if (clampToTargetDecelerationRate && accelerationRate < ATO_TARGET_DECELERATION_RATE)
                {
                    newDemand++;
                }

                int min = -train.specs.BrakeNotches;
                if (!clampToServiceBraking)
                {
                    min -= 1;
                }
                int max = train.specs.PowerNotches;
                atoDemands = Math.Max(min, Math.Min(max, newDemand));
                notchLastChange = currentTime;
                train.debugMessage += $" {newNotch}";
                return true;
            }
            return false;

        }
        internal double UpdateAcceleration(ElapseData data)
        {
            double speedPrev = accelerationLastSpeed;
            double speedNow = data.Vehicle.Speed.MetersPerSecond;
            double time = (data.TotalTime.Seconds - accelerationLastPoll.Seconds);

            accelerationRate = (speedNow - speedPrev) / time;

            accelerationLastPoll = data.TotalTime;
            accelerationLastSpeed = speedNow;

            return accelerationRate;
        }

        internal override void Initialize(InitializationModes mode)
        {
            atoStoppingPosition = null;
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
            if (beacon.Type == 33) //Distance to stop point
            {
                atoReceivedData = beacon.Optional;
            }

            //Central Line ATP
            if (beacon.Type == 6)
            {
                atoReceivedData = beacon.Optional % 1000;
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

        internal override void DoorChange(DoorStates oldState, DoorStates newState)
        {
            if(oldState == DoorStates.None && newState != DoorStates.None)
            {
                atoState = AtoStates.Stopped;
            }
            else if(oldState != DoorStates.None && newState == DoorStates.None)
            {
                //Door closed
                atoState = AtoStates.Ready;
                readyTimer = ATO_READY_TIMER;
            }
        }
    }
}
