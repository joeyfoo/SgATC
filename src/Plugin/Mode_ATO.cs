using System;
using OpenBveApi.Runtime;

namespace Plugin
{
    internal class Mode_ATO : Device
    {
        const float ACCELERATION_POLL_RATE = 0.0f; //sec
        const float ATO_TARGET_DECELERATION_RATE = -0.7f; //m/s
        const float ATO_TIME_BETWEEN_NOTCH_CHANGE = 0.35f;
        const float ATO_FRICTION_BRAKING_CUT_IN = 3.0f; //m/s
        const float ATO_LEVELLING_SPEED = 1.25f; //m/s
        const float ATO_LEVELLING_DECELERATION_RATE = -0.30f;
        const float ATO_LEVELLING_DISTANCE = 5.0f;
        const float ATO_TIME_BETWEEN_LEVELLING_NOTCH_CHANGE = 0.10f;
        const float ATO_POWERING_AMOUNT = 0.5f;
        const float ATO_BRAKING_AMOUNT = 0.1f;
        const float ATO_READY_TIMER = 1.0f;

        private enum AtoStates
        {
            Ready,
            Enroute,
            Stopping,
            Levelling, 
            Docked,
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

        public Mode_ATO(Train train)
        {
            this.train = train;
        }

        internal override int? Elapse(ElapseData data)
        {
            train.debugMessage = "";
            //Is train in Auto?
            if (train.trainModeActual != Train.TrainModes.Auto)
            {
                atoState = AtoStates.Docked;
                return null;
            }

            //Calculate current acceleration rate

            if (data.TotalTime.Seconds - accelerationLastPoll.Seconds >= ACCELERATION_POLL_RATE)
            {
                UpdateAcceleration(data);
            }

            train.debugMessage = $"{atoState}";

            if (atoState == AtoStates.Ready)
            {
                atoDemands = -train.specs.BrakeNotches;

                readyTimer -= data.ElapsedTime.Seconds;
                if (train.doorState != DoorStates.None)
                {
                    atoState = AtoStates.Stopped;
                }
                else if(readyTimer <= 0.0)
                {
                    atoState = AtoStates.Enroute;
                }
            }
            else if (atoState == AtoStates.Enroute)
            {
                double speedDifference = train.atpTargetSpeed - data.Vehicle.Speed.KilometersPerHour;

                int notch = 0;

                if (train.atpTargetSpeed <= 3.0 && data.Vehicle.Speed.KilometersPerHour <= 3.0)
                {
                    ChangeAtoDemands(data, -train.specs.B67Notch, ATO_TIME_BETWEEN_NOTCH_CHANGE);
                }
                else if (train.atpTargetSpeed < data.Vehicle.Speed.KilometersPerHour) //Braking
                {
                    notch = Math.Min(0, (int)(speedDifference / ATO_BRAKING_AMOUNT) - 1);
                }
                else //Powering
                {
                    notch = Math.Max(0, (int)(speedDifference / ATO_POWERING_AMOUNT));
                }

                ChangeAtoDemands(data, notch, ATO_TIME_BETWEEN_NOTCH_CHANGE, true, true);
                train.debugMessage = $"Enroute. {atoDemands}. Target speed {train.atpTargetSpeed}";

                if (atoStoppingPosition != null)
                {
                    double speed = data.Vehicle.Speed.MetersPerSecond;
                    double brakingStart = atoStoppingPosition.Value - ATO_LEVELLING_DISTANCE -
                        CalculateDistanceToStop(ATO_TARGET_DECELERATION_RATE, speed);

                    if (data.Vehicle.Location > brakingStart)
                    {
                        atoState = AtoStates.Stopping;
                        atoDemands = 0;
                    }
                }
            }
            else if (atoState == AtoStates.Stopping)
            {
                double distanceToStop = atoStoppingPosition.Value - data.Vehicle.Location;
                double tolerence = 0.0f; //(0.05 * distanceToStop);

                if (data.Vehicle.Speed.MetersPerSecond > ATO_LEVELLING_SPEED)
                {
                    double stoppingPositionOffset = ATO_LEVELLING_DISTANCE * 1.5;
                    if (data.Vehicle.Speed.MetersPerSecond < ATO_FRICTION_BRAKING_CUT_IN)
                    {
                        stoppingPositionOffset = ATO_LEVELLING_DISTANCE;
                    }

                    //Deceleration
                    int notchChange = CalculateStationStop(data, tolerence, atoStoppingPosition.Value - stoppingPositionOffset);
                    ChangeAtoDemands(data, (atoDemands.Value + notchChange), ATO_TIME_BETWEEN_NOTCH_CHANGE);
                }
                else
                {
                    atoState = AtoStates.Levelling;
                }
            }
            else if (atoState == AtoStates.Levelling)
            {
                double distanceToStop = atoStoppingPosition.Value - data.Vehicle.Location;

                double stoppingStart = atoStoppingPosition.Value - CalculateDistanceToStop(
                    ATO_LEVELLING_DECELERATION_RATE, data.Vehicle.Speed.MetersPerSecond);

                if (data.Vehicle.Location < stoppingStart)
                {
                    if (atoDemands < 0)
                    {
                        ChangeAtoDemands(data, (atoDemands.Value + 1), ATO_TIME_BETWEEN_LEVELLING_NOTCH_CHANGE);
                    }
                }
                else
                {
                    int notchChange = CalculateStationStop(data, 0.0, atoStoppingPosition.Value);
                    ChangeAtoDemands(data, (atoDemands.Value + notchChange), ATO_TIME_BETWEEN_LEVELLING_NOTCH_CHANGE);
                }

                if (data.Vehicle.Speed.MetersPerSecond <= 0.05)
                {
                    atoState = AtoStates.Docked;
                    atoStoppingPosition = null;
                    atoDemands = null;
                }
            }
            else if (atoState == AtoStates.Docked)
            {
                atoDemands = -train.specs.BrakeNotches;

                if (train.doorState != DoorStates.None)
                {
                    atoState = AtoStates.Stopped;
                }
            }
            else if (atoState == AtoStates.Stopped)
            {
                atoDemands = -train.specs.BrakeNotches;

                if (train.doorState == DoorStates.None)
                {
                    atoState = AtoStates.Ready;
                    readyTimer = ATO_READY_TIMER;
                }
            }

            if (atoReceivedData != null)
            {
                atoStoppingPosition = atoReceivedData + data.Vehicle.Location;
                atoReceivedData = null;
            }

            //train.debugMessage = $" Ato: {atoState} demanding {atoDemands} for {train.atpTargetSpeed}";
            return atoDemands;
        }

        internal int CalculateStationStop(ElapseData data, double tolerence, double targetStoppingPosition)
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
        }
        internal bool ChangeAtoDemands(ElapseData data, int newNotch, double frequency, bool clampToServiceBraking=true, bool clampToTargetDecelerationRate=false)
        {
            double currentTime = data.TotalTime.Seconds;

            if(currentTime - notchLastChange >= frequency)
            {
                int newDemand = Math.Max((int)atoDemands - 1, Math.Min((int)atoDemands + 1, newNotch));
                if (clampToTargetDecelerationRate && accelerationRate < ATO_TARGET_DECELERATION_RATE)
                {
                    newDemand++;
                }

                int min = -train.specs.BrakeNotches;
                if(!clampToServiceBraking)
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
        internal double CalculateDistanceToStop(double acceleration, double speed)
        {
            if(acceleration == 0.0)
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
            if(beacon.Type == 33) //Distance to stop point
            {
                atoReceivedData = beacon.Optional;
            }

            //Central Line ATP
            if(beacon.Type == 6)
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
    }
}
