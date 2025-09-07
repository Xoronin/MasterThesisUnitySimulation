using UnityEngine;
using System.Collections.Generic;

namespace RFSimulation.Propagation.Core
{
    public static class RFConstants
    {
        public const float SPEED_OF_LIGHT = 299792458f; // m/s
        public const float DEFAULT_NOISE_FLOOR = -110f; // dBm
        public const float DEFAULT_REFERENCE_DISTANCE = 1f; // meters
        public const float MIN_DISTANCE = 0.1f; // Minimum calculation distance
        public const float REFERENCE_DISTANCE = 100f; // Reference distance for path loss
        public const float PATH_LOSS_EXPONENT = 3.0f; // Typical urban value
    }
}