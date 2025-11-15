using UnityEngine;

namespace RFSimulation.Utils
{
    public static class UnitConversionHelper
    {
        public static float MHzToGHz(float mhz)
        {
            return mhz / 1000f;
        }
        public static float GHzToMHz(float ghz)
        {
            return ghz * 1000f;
        }
        public static float MHzToHz(float mhz)
        {
            return mhz * 1e6f;
        }
        public static float mToKm(float meters)
        {
            return meters / 1000f;
        }
    }
}