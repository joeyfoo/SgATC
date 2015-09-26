﻿using System;
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
        int trainModeCount = Enum.GetNames(typeof(TrainModes)).Length;

        internal int[] panel;
        //Sounds

        internal VehicleSpecs specs;
        internal TrainModes trainModeSelected = TrainModes.Off;
        internal TrainModes trainModeActual = TrainModes.Off;
        internal DoorStates doorState = DoorStates.None;

        internal List<Device> devices = new List<Device>();
        
        internal Train(int[] panel, PlaySoundDelegate playSound)
        {
            this.panel = panel;

            devices.Add(new Interlock(this));
            devices.Add(new ModeSelector(this));
        }

        internal void Elapse(ElapseData data)
        {
            //Run through devices
            List<int> demands = new List<int>();
            demands.Add(data.Handles.PowerNotch - data.Handles.BrakeNotch);

            foreach (Device device in devices)
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

            //Print debug message
            data.DebugMessage = "Selected mode: " + panel[1] + " " + trainModeSelected;
            data.DebugMessage += "\nDoors: " + doorState;
            data.DebugMessage += "\nDemands: ";
            foreach(int x in demands)
            {
                data.DebugMessage += x + ", ";
            }

            data.DebugMessage= data.DebugMessage.Replace("\n",Environment.NewLine);
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
        }

        internal void SetReverser(int reverser)
        {
            // Reverser state is ignored in game. 
            //TODO: Implement Adviser
        }

        internal void SetTrainMode(TrainModes previous, TrainModes current)
        {
            // Handle changing of train mode.

        }

        internal void SetPower(int powerNotch)
        {

        }

        internal void SetBrake(int brakeNotch)
        {

        }

        internal void KeyDown(VirtualKeys key)
        {
            if (key == VirtualKeys.C1) //Mode Up
            {
                if ((int)trainModeSelected < trainModeCount - 1)
                {
                    trainModeSelected++;
                }
            }
            else if (key == VirtualKeys.C2) //Mode Down
            {
                if ((int)trainModeSelected > 0)
                {
                    trainModeSelected--;
                }
            }

        }

        internal void KeyUp(VirtualKeys key)
        {

        }

        internal void HornBlow(HornTypes type)
        {

        }

        internal void DoorChange(DoorStates oldState, DoorStates newState)
        {

        }

        internal void SetSignal(SignalData[] signal)
        {

        }

        internal void SetBeacon(BeaconData beacons)
        {

        }

    }
}