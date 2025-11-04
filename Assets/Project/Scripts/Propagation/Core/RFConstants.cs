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
        public const float PATH_LOSS_EXPONENT_URBAN = 3.0f;             // Typical urban value
        public const float PATH_LOSS_EXPONENT_URBAN_SHADOWED = 4.0f;    // Typical shadowed urban value
        public const float METROPOLITAN_CORRECTION = 3.0f;              
    }
}