using RFSimulation.Core.Components;
using UnityEngine;

namespace RFSimulation.Utils
{
    public static class PlaceObjectsHelper
    {
        private const string RootGroupName = "SimulationObjects";
        private const string TxGroupName = "Transmitters";
        private const string RxGroupName = "Receivers";

        private const string TxTag = "Transmitter";
        private const string RxTag = "Receiver";

        public static void Organize(GameObject go)
        {
            if (go == null) return;

            var tx = go.GetComponent<Transmitter>();
            var rx = go.GetComponent<Receiver>();

            if (tx != null) { SetupAsTransmitter(go); return; }
            if (rx != null) { SetupAsReceiver(go); return; }

            Debug.LogWarning($"[PlaceObjectsHelper] '{go.name}' has neither Transmitter nor Receiver component.");
        }

        public static void OrganizeAllInScene()
        {
            var txs = Object.FindObjectsByType<Transmitter>(FindObjectsSortMode.InstanceID);
            var rxs = Object.FindObjectsByType<Receiver>(FindObjectsSortMode.InstanceID);

            for (int i = 0; i < txs.Length; i++) if (txs[i]) SetupAsTransmitter(txs[i].gameObject);
            for (int i = 0; i < rxs.Length; i++) if (rxs[i]) SetupAsReceiver(rxs[i].gameObject);
        }

        private static Transmitter SetupAsTransmitter(GameObject go)
        {
            var tx = go.GetComponent<Transmitter>();
            if (tx == null)
            {
                Debug.LogWarning($"[PlaceObjectsHelper] '{go.name}' missing Transmitter component.");
                return null;
            }

            var parent = EnsurePath($"{RootGroupName}/{TxGroupName}");
            go.transform.SetParent(parent, worldPositionStays: true);

            var id = string.IsNullOrEmpty(tx.uniqueID) ? go.GetInstanceID().ToString() : tx.uniqueID;
            go.name = $"Transmitter-{id}";

            TrySetTag(go, TxTag);


            return tx;
        }

        private static Receiver SetupAsReceiver(GameObject go)
        {
            var rx = go.GetComponent<Receiver>();
            if (rx == null)
            {
                Debug.LogWarning($"[PlaceObjectsHelper] '{go.name}' missing Receiver component.");
                return null;
            }

            var parent = EnsurePath($"{RootGroupName}/{RxGroupName}");
            go.transform.SetParent(parent, worldPositionStays: true);

            var id = string.IsNullOrEmpty(rx.uniqueID) ? go.GetInstanceID().ToString() : rx.uniqueID;
            go.name = $"Receiver-{id}";

            TrySetTag(go, RxTag);

            return rx;
        }

        private static Transform EnsurePath(string path)
        {
            var parts = path.Split('/');
            Transform current = null;

            for (int i = 0; i < parts.Length; i++)
            {
                var name = parts[i];
                Transform next = (current == null)
                    ? GameObject.Find(name)?.transform
                    : current.Find(name);

                if (next == null)
                {
                    var go = new GameObject(name);
                    if (current != null) go.transform.SetParent(current, false);
                    next = go.transform;
                }
                current = next;
            }
            return current;
        }

        private static void TrySetTag(GameObject go, string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            try { go.tag = tag; }
            catch { Debug.LogWarning($"[PlaceObjectsHelper] Tag '{tag}' not defined. Add it in Project Settings → Tags."); }
        }

    }
}
