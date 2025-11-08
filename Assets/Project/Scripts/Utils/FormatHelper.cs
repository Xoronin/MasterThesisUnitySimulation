using System;

namespace RFSimulation.Utils
{
    public class FormatHelper
    {
        public static string SafeString(object obj, string fieldOrProp, string fallback)
        {
            if (obj == null) return fallback;
            var t = obj.GetType();
            var p = t.GetProperty(fieldOrProp);
            if (p != null && p.PropertyType == typeof(string)) return (string)p.GetValue(obj);
            var f = t.GetField(fieldOrProp);
            if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(obj);
            return fallback;
        }

        public static float SafeFloat(object obj, string fieldOrProp, float fallback)
        {
            if (obj == null) return fallback;
            var t = obj.GetType();
            var p = t.GetProperty(fieldOrProp);
            if (p != null && (p.PropertyType == typeof(float) || p.PropertyType == typeof(double)))
                return Convert.ToSingle(p.GetValue(obj));
            var f = t.GetField(fieldOrProp);
            if (f != null && (f.FieldType == typeof(float) || f.FieldType == typeof(double)))
                return Convert.ToSingle(f.GetValue(obj));
            return fallback;
        }

        public static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return (s.Contains(",") || s.Contains("\"")) ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        }

        public static string FormatFloat(float v, string fmt) => float.IsNaN(v) ? "" : v.ToString(fmt);

    }
}