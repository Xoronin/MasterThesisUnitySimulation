using UnityEngine;
using System.Collections.Generic;

namespace RFSimulation.Propagation.Core
{
    public static class RFConstants
    {
        public const float SPEED_OF_LIGHT = 299792458f;                 // m/s
        public const float DEFAULT_REFERENCE_DISTANCE = 1f;             // meters
        public const float MIN_DISTANCE = 0.1f;                         // Minimum calculation distance
        public const float REFERENCE_DISTANCE = 100f;                   // Reference distance for path loss
        public const float PATH_LOSS_EXPONENT_FREE_SPACE = 2.0f;        // free space value
        public const float PATH_LOSS_EXPONENT_URBAN = 3.0f;             // Typical urban value
        public const float PATH_LOSS_EXPONENT_URBAN_SHADOWED = 4.0f;    // Typical shadowed urban value
        public const float METROPOLITAN_CORRECTION = 3.0f;
        public const float EPS = 1e-6f;                                 // Small epsilon value to avoid division by zero
        public const double EPS0 = 8.854e-12;
        public const double PI = System.Math.PI;
    }
}