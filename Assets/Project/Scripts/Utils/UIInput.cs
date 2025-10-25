// RFSimulation.Utils/UIInput.cs
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnityEngine;

namespace RFSimulation.Utils
{
    public static class UIInput
    {
        public static bool IsTyping()
        {
            var es = EventSystem.current;
            if (es == null) return false;

            var go = es.currentSelectedGameObject;
            if (go == null) return false;

            var a = go.GetComponent<InputField>();
            if (a != null && a.isFocused) return true;

            var b = go.GetComponent<TMP_InputField>();
            if (b != null && b.isFocused) return true;

            return false;
        }

        public static void Defocus()
        {
            var es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(null);
        }
    }
}
