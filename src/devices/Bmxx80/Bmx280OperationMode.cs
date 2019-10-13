// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Iot.Device.Bmxx80
{
    /// <summary>
    /// The operation modes the Bmx280 can operate in.
    /// </summary>
    public enum Bmx280OperationMode
    {
        /// <summary>
        /// In continuous mode the sensor won't go to sleep after
        /// performing a measurement.
        /// </summary>
        Continuous,
        /// <summary>
        /// In manual mode the sensor will go to sleep after performing
        /// a measurement.
        /// </summary>
        Manual
    }
}
