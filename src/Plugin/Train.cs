using System;
using OpenBveApi.Runtime;
using System.Collections.Generic;

namespace Plugin
{
    internal class Train
    {
        internal enum TrainStates
        {
            Uninitialised,
            Normal,
            RestrictedManual,
            ServiceBrake,
            EmergencyBrake
        }

        internal enum TrainModes
        {
            Off = 0,
            Auto = 1,
            CodedManual = 2,
            RestrictedManualReverse = 3,
            RestrictedManualForward = 4
        }
        internal int trainModeCount = Enum.GetNames(typeof(TrainModes)).Length;

        internal int[] panel;
        //Sounds

        internal VehicleSpecs specs;
        internal TrainModes trainModeSelected = TrainModes.Off;
        internal TrainModes trainModeActual = TrainModes.Off;
        internal DoorStates doorState = DoorStates.None;
        internal string debugMessage = "";
        
        internal double atpTrackTargetSpeed = 00.0; //ATP Target Speed
        internal double atpTrackNextTargetSpeed = 00.0; //upcoming target Speed
        internal double atpTargetSpeed = 00.0; //ATP Target Speed taking into account next train
        internal double atpTrackSafetySpeed = 00.0; //ATP Safety Speed
        internal double atpTrackNextSafetySpeed = 00.0; //upcoming safety Speed
        internal double atpSafetySpeed = 00.0; //ATP Safety Speed taking into account next train
        internal double atpTrackNextSpeedPosition = -1; //distance to upcoming

        internal double temp1 = 0;

        internal Dictionary<string, Device> devices = new Dictionary<string, Device>();
        
        internal Train(int[] panel, PlaySoundDelegate playSound)
        {
            this.panel = panel;

            devices.Add("internlock", new Interlock(this));
            devices.Add("modeselector", new ModeSelector(this));
            //devices.Add("rm", new Mode_RM(this));
            devices.Add("ato", new Mode_ATO(this));
            devices.Add("atp", new ATP(this));
        }

        internal void Elapse(ElapseData data)
        {
            //debugMessage = "";

            //Run through devices
            List<int> demands = new List<int>();

            //When in ATO, do not add
            if (trainModeActual != TrainModes.Auto)
            {
                demands.Add(data.Handles.PowerNotch - data.Handles.BrakeNotch);
            }

            foreach (Device device in devices.Values)
            {
                int? d = device.Elapse(data);
                if(d != null)
                {
                    demands.Add(d.Value);
                }
            }

            //Override reverser
            switch(trainModeActual)
            {
                case TrainModes.Off:
                    data.Handles.Reverser = 0;
                    break;
                case TrainModes.Auto:
                case TrainModes.CodedManual:
                case TrainModes.RestrictedManualForward:
                    data.Handles.Reverser = 1;
                    break;
                case TrainModes.RestrictedManualReverse:
                    data.Handles.Reverser = -1;
                    break;
            }

            //Calculate necessary brake/power application
            int demandApplication = int.MaxValue;

            foreach(int demand in demands)
            {
                if(demand < demandApplication)
                {
                    demandApplication = demand;
                }
            }

            //Apply necessary brake/power application
            if(demandApplication < 0) //Braking
            {
                data.Handles.PowerNotch = 0;
                if(-demandApplication > specs.BrakeNotches)
                {
                    data.Handles.BrakeNotch = specs.BrakeNotches + 1;
                }
                else
                {
                    data.Handles.BrakeNotch = -demandApplication;
                }
            }
            else if(demandApplication > 0)
            {
                data.Handles.BrakeNotch = 0;
                if(demandApplication > specs.PowerNotches)
                {
                    data.Handles.PowerNotch = specs.PowerNotches;
                }
                else
                {
                    data.Handles.PowerNotch = demandApplication;
                }
            }
            else
            {
                data.Handles.PowerNotch = 0;
                data.Handles.BrakeNotch = 0;
            }

            //Update panel variables
            for (int i = 0; i < panel.Length; i++)
            {
                panel[i] = 0;
            }
            panel[1] = (int)trainModeSelected;
            panel[2] = (int)atpSafetySpeed;
            panel[3] = (int)atpTargetSpeed;

            //Print debug message
            data.DebugMessage = "Selected mode: " + panel[1] + " " + trainModeSelected;
            //data.DebugMessage += "\nDoors: " + doorState;
            data.DebugMessage += "\nDemands: ";
            foreach (int x in demands)
            {
                data.DebugMessage += x + ", ";
            }
            data.DebugMessage = debugMessage;
            data.DebugMessage = $"Safety/target speeds are {atpTrackSafetySpeed}/{atpTrackTargetSpeed}"; 
            //
            //data.DebugMessage= data.DebugMessage.Replace("\n",Environment.NewLine);
        }

        internal void Initialize(InitializationModes mode)
        {
            switch(mode)
            {
                case InitializationModes.OffEmergency:
                    trainModeSelected = TrainModes.Off;
                    break;
                case InitializationModes.OnEmergency:
                    trainModeSelected = TrainModes.CodedManual;
                    break;
                case InitializationModes.OnService:
                    trainModeSelected = TrainModes.CodedManual;
                    break;
            }
            trainModeActual = trainModeSelected;
            
            foreach(Device device in devices.Values)
            {
                device.Initialize(mode);
            }
        }

        internal void SetReverser(int reverser)
        {
            // Reverser state is ignored in game. 
            //TODO: Implement Adviser

            foreach (Device device in devices.Values)
            {
                device.SetReverser(reverser);
            }
        }

        internal void SetPower(int powerNotch)
        {


            foreach (Device device in devices.Values)
            {
                device.SetPower(powerNotch);
            }
        }

        internal void SetBrake(int brakeNotch)
        {


            foreach (Device device in devices.Values)
            {
                device.SetBrake(brakeNotch);
            }
        }

        internal void KeyDown(VirtualKeys key)
        {
            foreach (Device device in devices.Values)
            {
                device.KeyDown(key);
            }
        }

        internal void KeyUp(VirtualKeys key)
        {
            foreach (Device device in devices.Values)
            {
                device.KeyUp(key);
            }
        }

        internal void HornBlow(HornTypes type)
        {
            foreach (Device device in devices.Values)
            {
                device.HornBlow(type);
            }
        }

        internal void DoorChange(DoorStates oldState, DoorStates newState)
        {
            foreach (Device device in devices.Values)
            {
                device.DoorChange(oldState, newState);
            }
        }

        internal void SetSignal(SignalData[] signal)
        {
            foreach (Device device in devices.Values)
            {
                device.SetSignal(signal);
            }
        }

        internal void SetBeacon(BeaconData beacons)
        {
            foreach (Device device in devices.Values)
            {
                device.SetBeacon(beacons);
            }
        }

    }
}
