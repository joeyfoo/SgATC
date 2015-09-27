using System;
using OpenBveApi.Runtime;

namespace Plugin
{
    internal class Mode_ATO : Device
    {
        const float ACCELERATION_POLL_RATE = 0.5f; //sec
        const float ATO_TARGET_DECELERATION_RATE = 0.75f; //m/s
        const float ATO_TIME_BETWEEN_HANDLE_CHANGE = 0.25f;
        const float ATO_LEVELLING_SPEED = 3.5f; //m/s
        //const float ATO_LEVELLING_DECELERATION_RATE = 0.3f;
        const float ATO_LEVELLING_DISTANCE = 5.0f;
        
        private enum AtoStates
        {
            NoStopInformation,
            Ready,
            Stopping,
            Levelling, 
            Stopped
        }
        private Train train;
        private double accelerationRate = 0.0;
        private double accelerationLastSpeed = 0.0;
        private Time accelerationLastPoll = new Time(0.0);
        private AtoStates atoState = AtoStates.NoStopInformation;

        private double? atoStoppingPosition = null;
        private int? atoReceivedData = null;
        private int? atoDemands = null;

        private string temp = "";

        public Mode_ATO(Train train)
        {
            this.train = train;
        }

        internal override int? Elapse(ElapseData data)
        {
            if (data.TotalTime.Seconds - accelerationLastPoll.Seconds >= ACCELERATION_POLL_RATE)
            {
                //temp = atoStoppingPosition + " " + (data.TotalTime.Seconds - accelerationLastPoll.Seconds);
                accelerationRate = (data.Vehicle.Speed.MetersPerSecond - accelerationLastSpeed) / 
                    (data.TotalTime.Seconds - accelerationLastPoll.Seconds);
                accelerationLastPoll = data.TotalTime;
                accelerationLastSpeed = data.Vehicle.Speed.MetersPerSecond;

                //TEMP
                if (atoState == AtoStates.NoStopInformation)
                {
                    if(atoStoppingPosition != null)
                    {
                        atoState = AtoStates.Ready;
                    }
                }
                else if(atoState == AtoStates.Ready)
                {
                    double speed = data.Vehicle.Speed.MetersPerSecond;
                    double brakingStart = atoStoppingPosition.Value - ATO_LEVELLING_DISTANCE - ((speed * speed) / (2 * ATO_TARGET_DECELERATION_RATE));

                    if (data.Vehicle.Location > brakingStart)
                    {
                        atoState = AtoStates.Stopping;
                        atoDemands = 0;
                    }
                }
                else if (atoState == AtoStates.Stopping)
                {
                    double distanceToStop = atoStoppingPosition.Value - data.Vehicle.Location;
                    double tolerence = (0.05 * distanceToStop);
                    double estimatedStoppingPosition = data.Vehicle.Location + ATO_LEVELLING_DISTANCE 
                        + CalculateDistanceToStop(accelerationRate, data.Vehicle.Speed.MetersPerSecond);
                    int currentDemand = data.Handles.PowerNotch - data.Handles.BrakeNotch;

                    temp = atoState.ToString() + " predictive/stop/current: " + estimatedStoppingPosition + " " + atoStoppingPosition + " " + data.Vehicle.Location;

                    if (estimatedStoppingPosition < atoStoppingPosition - tolerence)
                    {
                        atoDemands = atoDemands + 1;
                    }
                    else if (estimatedStoppingPosition > atoStoppingPosition)
                    {
                        atoDemands = atoDemands - 1;
                    }

                    if (data.Vehicle.Speed.MetersPerSecond <= ATO_LEVELLING_SPEED)
                    {
                        atoState = AtoStates.Levelling;
                    }
                }
                else if (atoState == AtoStates.Levelling)
                {
                    double distanceToStop = atoStoppingPosition.Value - data.Vehicle.Location;
                    double estimatedStoppingPosition = data.Vehicle.Location + CalculateDistanceToStop(
                        accelerationRate, data.Vehicle.Speed.MetersPerSecond);
                    int currentDemand = data.Handles.PowerNotch - data.Handles.BrakeNotch;

                    temp = atoState.ToString() + " predictive/stop/current: " + estimatedStoppingPosition + " " + atoStoppingPosition + " " + data.Vehicle.Location;

                    if (estimatedStoppingPosition < atoStoppingPosition - 0.1)
                    {
                        atoDemands = atoDemands + 1;
                    }
                    else if (estimatedStoppingPosition > atoStoppingPosition + 0)
                    {
                        atoDemands = atoDemands - 1;
                    }

                    if (data.Vehicle.Speed.MetersPerSecond <= 0.1)
                    {
                        atoState = AtoStates.Stopped;
                        atoStoppingPosition = null;
                        atoDemands = null;
                    }
                }
                else if (atoState == AtoStates.Stopped)
                {
                    if (data.Vehicle.Speed.MetersPerSecond >= 0.1)
                    {
                        atoState = AtoStates.NoStopInformation;
                    }
                }
            }
            
            //temp += atoState.ToString() + " " + atoDemands;

            if (atoReceivedData != null)
            {
                atoStoppingPosition = atoReceivedData + data.Vehicle.Location;
                temp = atoReceivedData.ToString();
                atoReceivedData = null;
            }

            data.DebugMessage = temp;
            return atoDemands;
        }

        internal double CalculateDistanceToStop(double acceleration, double speed)
        {
            if(acceleration >= 0)
            {
                acceleration = -0.1;
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
            if(beacon.Type == 33)
            {
                atoReceivedData = beacon.Optional;
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
