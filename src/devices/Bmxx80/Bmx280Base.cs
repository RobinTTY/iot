// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//Ported from https://github.com/adafruit/Adafruit_BMP280_Library/blob/master/Adafruit_BMP280.cpp
//Formulas and code examples can also be found in the datasheet http://www.adafruit.com/datasheets/BST-BMP280-DS001-11.pdf

using System;
using System.Device.I2c;
using System.IO;
using Iot.Device.Bmxx80.PowerMode;
using Iot.Device.Bmxx80.Register;
using Iot.Device.Bmxx80.FilteringMode;
using Iot.Units;

namespace Iot.Device.Bmxx80
{
    /// <summary>
    /// Represents the core functionality of the Bmx280 family.
    /// </summary>
    public abstract class Bmx280Base : Bmxx80Base
    {
        /// <summary>
        /// Default I2C bus address.
        /// </summary>
        public const byte DefaultI2cAddress = 0x77;

        /// <summary>
        /// Secondary I2C bus address.
        /// </summary>
        public const byte SecondaryI2cAddress = 0x76;

        /// <summary>
        /// Converts oversampling to needed measurement cycles for that oversampling.
        /// </summary>
        protected static readonly int[] s_osToMeasCycles = { 0, 7, 9, 14, 23, 44 };

        private Bmx280OperationMode _operationMode;
        private Bmx280FilteringMode _filteringMode;
        private StandbyTime _standbyTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bmx280Base"/> class.
        /// </summary>
        /// <param name="deviceId">The ID of the device.</param>
        /// <param name="i2cDevice">The <see cref="I2cDevice"/> to create with.</param>
        protected Bmx280Base(byte deviceId, I2cDevice i2cDevice)
            : base(deviceId, i2cDevice) { }

        /// <summary>
        /// Operation mode the sensor is operating in.
        /// </summary>
        public Bmx280OperationMode OperationMode
        {
            get => _operationMode;
            set
            {
                switch (value)
                {
                    case Bmx280OperationMode.Continuous:
                        SetPowerMode(Bmx280PowerMode.Normal);
                        break;
                    case Bmx280OperationMode.Manual:
                        // Set to sleep, sensor will enter forced mode when Read() is called
                        SetPowerMode(Bmx280PowerMode.Sleep);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }
                _operationMode = value;
            }
        }


        /// <summary>
        /// Gets or sets the IIR filter mode.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the <see cref="Bmx280FilteringMode"/> is set to an undefined mode.</exception>
        public Bmx280FilteringMode FilterMode
        {
            get => _filteringMode;
            set
            {
                byte current = Read8BitsFromRegister((byte)Bmx280Register.CONFIG);
                current = (byte)((current & 0b_1110_0011) | (byte)value << 2);

                Span<byte> command = stackalloc[] { (byte)Bmx280Register.CONFIG, current };
                _i2cDevice.Write(command);
                _filteringMode = value;
            }
        }

        /// <summary>
        /// Gets or sets the standby time between two consecutive measurements.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the <see cref="Bmxx80.StandbyTime"/> is set to an undefined mode.</exception>
        public StandbyTime StandbyTime
        {
            get => _standbyTime;
            set
            {
                byte current = Read8BitsFromRegister((byte)Bmx280Register.CONFIG);
                current = (byte)((current & 0b_0001_1111) | (byte)value << 5);

                Span<byte> command = stackalloc[] { (byte)Bmx280Register.CONFIG, current };
                _i2cDevice.Write(command);
                _standbyTime = value;
            }
        }

        /// <summary>
        /// Reads the temperature from register.
        /// </summary>
        /// <returns>The measured <see cref="Temperature"/>.</returns>
        protected override Temperature ReadTemperatureRegister()
        {
            if (TemperatureSampling == Sampling.Skipped)
                return Temperature.FromCelsius(double.NaN);

            var temp = (int)Read24BitsFromRegister((byte)Bmx280Register.TEMPDATA_MSB, Endianness.BigEndian);
            return CompensateTemperature(temp >> 4);
        }

        /// <summary>
        /// Reads the pressure from register.
        /// </summary>
        /// <returns>The measured pressure.</returns>
        protected override double ReadPressureRegister()
        {
            if (PressureSampling == Sampling.Skipped)
                return double.NaN;

            var press = (int)Read24BitsFromRegister((byte)Bmx280Register.PRESSUREDATA, Endianness.BigEndian);
            long pressPa = CompensatePressure(press >> 4);
            return (double)pressPa / 256;
        }

        /// <summary>
        /// Get the current status of the device.
        /// </summary>
        /// <returns>The <see cref="DeviceStatus"/>.</returns>
        public DeviceStatus ReadStatus()
        {
            var status = Read8BitsFromRegister((byte)Bmx280Register.STATUS);

            // Bit 3.
            var measuring = ((status >> 3) & 1) == 1;

            // Bit 0.
            var imageUpdating = (status & 1) == 1;

            return new DeviceStatus
            {
                ImageUpdating = imageUpdating,
                Measuring = measuring
            };
        }

        /// <summary>
        /// Sets the power mode to the given mode
        /// </summary>
        /// <param name="powerMode">The <see cref="Bmx280PowerMode"/> to set.</param>
        protected void SetPowerMode(Bmx280PowerMode powerMode)
        {
            byte read = Read8BitsFromRegister(_controlRegister);

            // Clear last 2 bits.
            var cleared = (byte)(read & 0b_1111_1100);

            Span<byte> command = stackalloc[] { _controlRegister, (byte)(cleared | (byte)powerMode) };
            _i2cDevice.Write(command);
        }

        /// <summary>
        /// Gets the required time in ms to perform a measurement with the current sampling modes.
        /// </summary>
        /// <returns>The time it takes for the chip to read data in milliseconds rounded up.</returns>
        public virtual int GetMeasurementDuration()
        {
            return s_osToMeasCycles[(int)PressureSampling] + s_osToMeasCycles[(int)TemperatureSampling];
        }

        /// <summary>
        /// Sets the default configuration for the sensor.
        /// </summary>
        protected override void SetDefaultConfiguration()
        {
            base.SetDefaultConfiguration();
            FilterMode = Bmx280FilteringMode.Off;
            StandbyTime = StandbyTime.Ms125;
        }

        /// <summary>
        /// Compensates the pressure in Pa, in Q24.8 format (24 integer bits and 8 fractional bits).
        /// </summary>
        /// <param name="adcPressure">The pressure value read from the device.</param>
        /// <returns>Pressure in Hectopascals (hPa).</returns>
        /// <remarks>
        /// Output value of “24674867” represents 24674867/256 = 96386.2 Pa = 963.862 hPa.
        /// </remarks>
        private long CompensatePressure(int adcPressure)
        {
            // Formula from the datasheet http://www.adafruit.com/datasheets/BST-BMP280-DS001-11.pdf
            // The pressure is calculated using the compensation formula in the BMP280 datasheet
            long var1 = TemperatureFine - 128000;
            long var2 = var1 * var1 * (long)_calibrationData.DigP6;
            var2 = var2 + ((var1 * (long)_calibrationData.DigP5) << 17);
            var2 = var2 + ((long)_calibrationData.DigP4 << 35);
            var1 = ((var1 * var1 * (long)_calibrationData.DigP3) >> 8) + ((var1 * (long)_calibrationData.DigP2) << 12);
            var1 = (((((long)1 << 47) + var1)) * (long)_calibrationData.DigP1) >> 33;
            if (var1 == 0)
            {
                return 0; //Avoid exception caused by division by zero
            }
            //Perform calibration operations
            long p = 1048576 - adcPressure;
            p = (((p << 31) - var2) * 3125) / var1;
            var1 = ((long)_calibrationData.DigP9 * (p >> 13) * (p >> 13)) >> 25;
            var2 = ((long)_calibrationData.DigP8 * p) >> 19;
            p = ((p + var1 + var2) >> 8) + ((long)_calibrationData.DigP7 << 4);

            return p;
        }
    }
}
