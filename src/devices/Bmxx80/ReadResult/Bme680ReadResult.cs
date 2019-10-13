// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Iot.Units;

namespace Bmxx80.ReadResult
{
    /// <summary>
    /// Measurement results of a Bme680.
    /// </summary>
    public struct Bme680ReadResult
    {
        /// <summary>
        /// Collected temperature measurement.
        /// </summary>
        public Temperature Temperature;

        /// <summary>
        /// Collected pressure measurement.
        /// </summary>
        public double Pressure;

        /// <summary>
        /// Collected humidity measurement.
        /// </summary>
        public double Humidity;

        /// <summary>
        /// Collected gas resistance measurement.
        /// </summary>
        public double GasResistance;
    }
}
