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

        // Path loss exponents by environment
        public static readonly Dictionary<EnvironmentType, float> PATH_LOSS_EXPONENTS = new()
        {
            { EnvironmentType.FreeSpace, 2.0f },
            { EnvironmentType.Urban, 3.0f },
        };

        // Reference distances by environment
        public static readonly Dictionary<EnvironmentType, float> REFERENCE_DISTANCES = new()
        {
            { EnvironmentType.FreeSpace, 1.0f },
            { EnvironmentType.Urban, 100.0f },
        };
    }
}