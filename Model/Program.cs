﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

// Change: Read from register
// Change: Write to register
// Change: How to do when multiple registers
// Note: Delete all Set...
// Note: Keep SetMode?


namespace Model
{
    class Program
    {
        static class Register
        {
            // DEFINE REGISTERS 
            // REGISTER: 450/451 (int32)
            public const ushort TargetInput = 450;
            // REGISTER: 200/201 (int32)
            public const ushort Position = 200;
            // REGISTER: 202 (int16)
            public const ushort Speed = 202;
            // REGISTER: 203 (int16)
            public const ushort Torque = 203;
            // REGISTER: 420/421 (int32)
            public const ushort Time = 420;
            // REGISTER: 170-173 (int16)
            public const ushort Pressure = 170;
            // REGISTER: 353 (int16)
            public const ushort Acceleration = 353;
            // REGISTER: 354 (int16)
            public const ushort Deacceleration = 354;
            // PositionRamp (Mode 21): Closed control of position with ramp control.
            // SpeedRamp (Mode 33): Speed control mode with ramp control.
            // Shutdown (Mode 4)
            public const ushort PositionRamp = 400;
            public const ushort SpeedRamp = 400;
            public const ushort Shutdown = 400;
            public const ushort Mode = 400;
            public const ushort MotorTorqueMax = 204;
            public const ushort Status = 410;
        }
        static class Hardware
        {
            //DEFINE HARDWARE PARAMETERS
            public const double Pitch = 32; // [mm] of gearwheel
            public const double Diameter = 34; // [mm] of syringe
            public const double Area = Diameter * Diameter / 400 * Math.PI; // [cm^2] of syringe
        }
        static class Mode
        {
            public const int PositionRamp = 21;
            public const int SpeedRamp = 33;
            public const int Shutdown = 4;
            public const int MotorOff = 0;
            public const int Beep = 60;
        }

        //Data from Frontend to Backend

        public class Backend
        {
            struct MeasurementData
            {
                public double rpm;
                public double flow;
                public double volume;
                public double position;
                public double torque;
                public double pressure;
                public double time;
            };
            struct MotorMeasurementData
            {
                public double speed;
                public double position;
                public double torque;
                public double pressure;
                public double time;
            };

            // GET DATA
            static MeasurementData getMeasurements()
            {
                // Receives measurement data from motor, convert to other units and return them
                MeasurementData data;
                MotorMeasurementData motordata = getMotorMeasurements();
                data.rpm = MotorSpeedToRPM(motordata.speed);
                data.flow = MotorSpeedToFlow(motordata.speed);
                data.volume = MotorPositionToVolume(motordata.position);
                data.position = MotorPositionToPosition(motordata.position);
                data.torque = MotorTorqueToTorque(motordata.torque);
                data.pressure = MotorPressureToPressure(motordata.pressure);
                data.time = motordata.time;
                return data;
            }
            static MotorMeasurementData getMotorMeasurements()
            {
                // Receive measurement data from motor
                MotorMeasurementData data;
                data.speed = ReadFromRegister(Register.Speed);
                data.position = ReadFromRegister(Register.Position);
                data.torque = ReadFromRegister(Register.Torque);
                data.pressure = ReadFromRegister(Register.Pressure);
                data.time = ReadFromRegister(Register.Time);
                return data;
            }
            static double ReadFromRegister(int registerindex)
            {
                // READ FROM REGISTER INDEX
                double index = 1; //Dummie
                return index;
            }

            // Convert motor measured data to standard units
            static double MotorSpeedToRPM(double motorSpeed)
            {
                // Converting speed data from motor to RPM
                // Inputs:
                // motorSpeed (raw data from motor)
                double rpm = motorSpeed * 60 / 256;
                return rpm;
            }
            static double MotorSpeedToFlow(double motorSpeed)
            {
                // Converting speed data from motor to flow (ml/s)
                // Inputs:
                // motorSpeed (raw data from motor)

                // f = (motorSpeed * 60 / 256) / 60 * (pitch / 10) * area = (rev/min)*(min/s)*(cm/rev)*A = (cm/s * A)
                double f = motorSpeed * Hardware.Pitch * Hardware.Area / 2560;
                return f;
            }
            static double MotorPositionToVolume(double motorPosition)
            {
                // Converting position data (ticks) from motor to Volume (ml)
                // Inputs:
                // motorPosition (raw data from motor (ticks))

                // v = (motorPosition / 4096) * (pitch / 10) * Area = (Rotations * Length/Rotation * Area = Length * Area)
                double v = motorPosition * Hardware.Pitch * Hardware.Area / 40960;
                return v;
            }
            static double MotorTorqueToTorque(double motorTorque)
            {
                // Converting torque data (mNm) from motor to Torque (Nm)
                // Inputs:
                // motorTorque (raw data from motor (mNm))

                double t = motorTorque / 1000;
                return t;
            }
            static double MotorPressureToPressure(double motorPressureData)
            {
                // Converting pressure data (VDC [0,5]) from motor to pressure (N/m^2)
                // Inputs:
                // motorPressure (VDC)

                double pBias = 0; // Constant unwanted bias [VDC]
                double ampFactor = 1; // Linear relationship [N/m^2/VDC]
                double staticFactor = 0; // Static additive constant [N/m^2]

                double p = (motorPressureData - pBias) * ampFactor + staticFactor;
                return p;
            }
            static double MotorPositionToPosition(double motorPosition)
            {
                // Converting position data (ticks) from motor to position (cm)
                // Inputs:
                // motorPosition (raw data from motor (ticks))

                // p = (motorPosition / 4096) * (pitch / 10)  = (Rotations * Length/Rotation = Length)
                double p = motorPosition * Hardware.Pitch / 40960;
                return p;
            }


            // SEND & RECEIVE DATA
            static List<double> SendReceiveData(List<double> targetvalues, List<double> time, int writeRegister, int readRegister)
            {
                // Send targetvalues to register "writeRegister" at specified times.
                // reads data from "readRegister"


                // abort if lists of different lengths
                if (targetvalues.Count() != time.Count())
                {

                    throw new Exception("Input lists not of equal length");
                }

                List<double> motorData = new List<double>(); // save data from motor

                Stopwatch stopWatch = new Stopwatch();
                Console.WriteLine("time: 0s");
                WriteToRegister(writeRegister, targetvalues[0]); //write initial target
                motorData.Add(ReadFromRegister(readRegister));  //read initial motor data

                int i = 1;
                stopWatch.Start();
                while (i < targetvalues.Count())
                {

                    if (time[i] <= stopWatch.Elapsed.TotalSeconds)
                    { // If more time have elapsed than time[i]
                        Console.WriteLine("time: {0}s", stopWatch.Elapsed.TotalSeconds);
                        WriteToRegister(writeRegister, targetvalues[i]); // write data
                        motorData.Add(ReadFromRegister(readRegister)); // read data
                        i += 1;
                    }

                    // in case of...
                    double overtime = 30;
                    if (stopWatch.Elapsed.TotalSeconds > overtime)
                    {
                        Console.WriteLine("SendTagetData says: something is wrong");
                        stopWatch.Stop();
                        break;
                    }
                }
                stopWatch.Stop();
                return motorData;
            }

            static Tuple<List<double>, List<double>> SendReceiveData(List<double> targetvalues, List<double> time, int writeRegister, int readRegister1, int readRegister2)
            {
                // Send targetvalues to register "writeRegister" at specified times.
                // reads data from "readRegister"

                // abort if lists of different lengths
                if (targetvalues.Count() != time.Count())
                {
                    throw new Exception("Input lists not of equal length");
                }

                List<double> motorData1 = new List<double>(); // save data from motor
                List<double> motorData2 = new List<double>();
                Stopwatch stopWatch = new Stopwatch();
                Console.WriteLine("time: 0s");
                WriteToRegister(writeRegister, targetvalues[0]); //write initial target
                motorData1.Add(ReadFromRegister(readRegister1));  //read initial motor data
                motorData2.Add(ReadFromRegister(readRegister2));

                int i = 1;
                stopWatch.Start();
                while (i < targetvalues.Count())
                {

                    if (time[i] <= stopWatch.Elapsed.TotalSeconds)
                    { // If more time have elapsed than time[i]
                        Console.WriteLine("time: {0}s", stopWatch.Elapsed.TotalSeconds);
                        WriteToRegister(writeRegister, targetvalues[i]); // write data
                        motorData1.Add(ReadFromRegister(readRegister1)); // read data
                        motorData2.Add(ReadFromRegister(readRegister2));
                        i += 1;
                    }

                    // in case of...
                    double overtime = 30;
                    if (stopWatch.Elapsed.TotalSeconds > overtime)
                    {
                        Console.WriteLine("SendTagetData says: something is wrong");
                        stopWatch.Stop();
                        break;
                    }
                }
                stopWatch.Stop();
                return Tuple.Create(motorData1, motorData2);
            }

            // SEND DATA
            static void SetMotorRPM(double rpm)
            {
                // Converts RPM to motor speed and store in register
                // REGISTER: 202 (int16)
                double motorSpeedData = RPMToMotorSpeed(rpm);
                WriteToRegister(Register.Speed, motorSpeedData);
            }
            static void SetMotorFlow(double flow)
            {
                // Converts flow (ml/s) to motor speed and store in register
                // REGISTER: 202 (int16)
                double motorSpeedData = FlowToMotorSpeed(flow);
                WriteToRegister(Register.Speed, motorSpeedData);
            }
            static void SetMotorVolume(double volume)
            {
                // Converts volume (ml) to motor position and store in register
                // REGISTER: 200/201 (int32)
                double motorPosition = VolumeToMotorPosition(volume);
                WriteToRegister(Register.Position, motorPosition);
            }
            static void SetMotorMaxAcc(double acc)
            {
                // Converts acceleration (ml/s^2) to motor acceleration and store in register
                // REGISTER: 353/354 (int16/int16)

                double motorAcc = AccToMotorAcc(acc);

                // STORE IN REGISTERS 353 or 354
                if (motorAcc >= 0)
                { // Max acceleration
                    WriteToRegister(Register.Acceleration, motorAcc);
                }
                else
                { // Max deacceleration
                    motorAcc = -motorAcc; //Positive
                    WriteToRegister(Register.Deacceleration, motorAcc);
                }
            }

            // Convert standard units to motor units
            static double RPMToMotorSpeed(double rpm)
            {
                // Converting RPM to motor speed data
                // Inputs:
                // RPM
                double motorSpeed = rpm * 256 / 60;
                return motorSpeed;
            }
            static double FlowToMotorSpeed(double flow)
            {
                // Converting flow (ml/s) to motor speed data
                // Inputs:
                // flow (ml/s)

                double motorSpeed = flow * 2560 / Hardware.Pitch / Hardware.Area;
                return motorSpeed;
            }
            static double VolumeToMotorPosition(double volume)
            {
                // Converting volume (ml) to motor position data (ticks)
                // Inputs:
                // volume (ml)

                double motorPosition = volume * 40960 / Hardware.Pitch / Hardware.Area;
                return motorPosition;
            }
            static double TorqueToMotorTorque(double torque)
            {
                // Converting torque (Nm) to motor Torque (mNm)
                // Inputs:
                // motorTorque (raw data from motor (mNm))

                double motorTorque = 1000 * torque;
                return motorTorque;
            }
            static double AccToMotorAcc(double acc)
            {
                // Converting acceleration to motor accereration (pulses/s^2)/256
                // Inputs:
                // acc (m/s^2)
                double motorAccereration = acc * Hardware.Pitch * 4096000 / 256;
                return motorAccereration;
            }

            static List<double> FlowListToMotorSpeed(List<double> flow)
            {
                // Converting flow (ml/s) to motor speed data
                // Inputs:
                // flow (ml/s)
                for (int i = 1; i <= flow.Count; i++)
                {
                    flow[i - 1] = FlowToMotorSpeed(flow[i - 1]);
                }
                return flow;
            }

            static List<double> MotorSpeedListFlow(List<double> motorSpeed)
            {
                // Converting flow (ml/s) to motor speed data
                // Inputs:
                // flow (ml/s)
                for (int i = 1; i <= motorSpeed.Count; i++)
                {
                    motorSpeed[i - 1] = MotorSpeedToFlow(motorSpeed[i - 1]);
                }
                return motorSpeed;
            }

            static void WriteToRegister(int registerindex, double data)
            {
                // Writes data [data] in register [registerindex]
                //WRITE DATA TO INDEX

                //How to cast from double to ushort[] ?
                // if the double can be represented by a Int32 this should work
                // the largest value we ever need to write to a register is int32
                //int intdata = (int) data;
                //ushort[] m = new ushort[2];
                //m[0] = (ushort)intdata;
                //m[1] = (ushort)(intdata >> 16);

                //ModbusCommunication.RunModbus((ushort)registerindex, m);
                //Console.WriteLine("{0} written to register {1}",data,registerindex);
            }

            static void SetMode(int mode)
            {
                WriteToRegister(Register.PositionRamp, mode);
            }

            // Calculate velocity parameters
            static void LinearMovementToVelocity(double t0, double t1, double ft0, double ft1)
            {
                SetMode(Mode.SpeedRamp);                  // Set linear movement between velocities
                SetMotorMaxAcc(GetAcc(t0, t1, ft0, ft1)); //Acc to motor
                SetMotorFlow(ft1);                        //Speed to motor
            }

            static double GetAcc(double t0, double t1, double vt0, double vt1)
            {
                // Returns acceleration required (units/(s^2)) to bring the system from velocity vt0 at time t0 to vt1 at time t1
                // Inputs:
                // t0 (s)
                // t1 (s)
                // vt0 (units/s)
                // vt1 (units/s)
                if (t1 <= t0)
                {
                    //ERROR MESSAGE
                }
                double acc = (vt1 - vt0) / (t1 - t0);
                return acc;
            }

            public static void Main(string[] args)
            {
                String portName = ModbusCommunication.getSerialPortName();
                ModbusCommunication modCom = new ModbusCommunication(portName);
                modCom.RunModbus(Register.Mode, (Int16)1);
                modCom.RunModbus(Register.TargetInput, 0);
                modCom.RunModbus(Register.Mode, (Int16)21);

                //modCom.RunModbus(Register.TargetInput,0);
                //modCom.RunModbus(Register.Mode,(Int16)33);
                //modCom.RunModbus(Register.TargetInput,100);
                //Thread.Sleep(2000);
                //modCom.RunModbus(Register.TargetInput,2000);
                //Thread.Sleep(2000);
                //modCom.RunModbus(Register.TargetInput,8000);
                //Thread.Sleep(2000);
                //modCom.RunModbus(Register.TargetInput,30000);
                //Thread.Sleep(2000);
                //modCom.RunModbus(Register.TargetInput,2000);
                //Thread.Sleep(2000);
                //modCom.RunModbus(Register.TargetInput,0);
                //modCom.RunModbus(Register.Mode,(Int16)1);

                MotorControl motCon = new MotorControl(modCom);

                // test of event safety function
                int currentTorque = modCom.ReadModbus(Register.Torque, (ushort)1, false);
                Console.WriteLine("current Torque:");
                Console.WriteLine(currentTorque);

                motCon.CreateEvent((ushort)0,
                                   (Int16)(0B000000000100000), //bitmask to get torque from status register
                                   (Int16)(Register.Status),
                                   (ushort)0XF007, // and between bitmask and status register
                                   (Int16)(Register.Mode),
                                   (ushort)0,
                                   (Int16)0); //no source register

                modCom.RunModbus(Register.MotorTorqueMax, (Int16)100);

                int dummieRead;
                Double[] RecordedTimes1 = new Double[100];
                Double[] RecordedTimes2 = new Double[100];
                Double[] RecordedTimesRead = new Double[100];
                Double[] RecordedTimesWrite = new Double[100];
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                for (int i = 0; i < 99; i++)
                {
                    RecordedTimes1[i] = stopWatch.Elapsed.TotalSeconds;
                    dummieRead = modCom.ReadModbus(Register.Position, 2, true);
                    RecordedTimes2[i] = stopWatch.Elapsed.TotalSeconds;
                }
                for (int i = 0; i < 100; i++)
                {
                    RecordedTimesRead[i] = RecordedTimes2[i] - RecordedTimes1[i];
                }
                for (int i = 0; i < 100; i++)
                {
                    RecordedTimes1[i] = stopWatch.Elapsed.TotalSeconds;
                    modCom.RunModbus((ushort)450, (int)0);
                    RecordedTimes2[i] = stopWatch.Elapsed.TotalSeconds;
                }
                for (int i = 0; i < 100; i++)
                {
                    RecordedTimesWrite[i] = RecordedTimes2[i] - RecordedTimes1[i];
                }
                Console.WriteLine("Read: Max: {0}, Min: {1}, Avr: {2}", RecordedTimesRead.Max(), RecordedTimesRead.Min(), RecordedTimesRead.Average());
                Console.WriteLine("Write: Max: {0}, Min: {1}, Avr: {2}", RecordedTimesWrite.Max(), RecordedTimesWrite.Min(), RecordedTimesWrite.Average());

                //List<Int32> ticks = new List<Int32>() { 0, 100, 1000, 2000, 3000, 2000, 1000, 100, 0 };
                //List<Int32> ticks = new List<Int32>() {0,2000,4000,8000,4000,500,-2000,-2000,0};
                //List<double> times = new List<double>() { 0, 1, 2, 3, 4, 5, 6, 7, 8 };

                //motCon.RunTickSequence(ticks, times, Mode.PositionRamp);
                //motCon.ManualControl();
                //Console.ReadLine();
                modCom.EndModbus();

                Console.ReadLine();

            }
        }
    }
}
