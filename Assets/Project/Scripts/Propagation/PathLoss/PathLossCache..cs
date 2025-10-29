using System.Collections.Generic;
using UnityEngine;
using RFSimulation.Propagation.Core;

namespace RFSimulation.Propagation.PathLoss
{
	public class PathLossCache
	{
		private readonly Dictionary<string, CacheEntry> _cache;
		private readonly int _maxSize;
		private int _hits;
		private int _misses;

		public PathLossCache(int maxSize = 1000)
		{
			_cache = new Dictionary<string, CacheEntry>();
			_maxSize = maxSize;
			_hits = 0;
			_misses = 0;
		}

		public bool TryGetValue(PropagationContext context, out float value)
		{
			string key = GenerateKey(context);

			if (_cache.TryGetValue(key, out CacheEntry entry))
			{
				_hits++;
				value = entry.Value;
				return true;
			}

			_misses++;
			value = 0f;
			return false;
		}

		public void Store(PropagationContext context, float value)
		{
			string key = GenerateKey(context);

			if (_cache.Count >= _maxSize)
			{
				RemoveOldestEntry();
			}

			_cache[key] = new CacheEntry { Value = value, AccessTime = Time.time };
		}

		public void Clear()
		{
			_cache.Clear();
			_hits = 0;
			_misses = 0;
		}

		public (int entries, float hitRate) GetStats()
		{
			float hitRate = _hits + _misses > 0 ? (float)_hits / (_hits + _misses) : 0f;
			return (_cache.Count, hitRate);
		}

        private string GenerateKey(PropagationContext context)
        {
            // Consider slightly finer rounding if small height edits should matter immediately
            Vector3 roundedTx = RoundVector(context.TransmitterPosition, 0.1f);
            Vector3 roundedRx = RoundVector(context.ReceiverPosition, 0.1f);

            // NEW: bake in model + obstacle flag
            return $"{roundedTx}_{roundedRx}_{context.TransmitterPowerDbm:F1}_" +
                   $"{context.AntennaGainDbi:F1}_{context.FrequencyMHz:F0}_" +
                   $"{context.Model}_{(context.HasObstacles ? 1 : 0)}";
        }

        private Vector3 RoundVector(Vector3 vector, float precision)
		{
			float factor = 1f / precision;
			return new Vector3(
				Mathf.Round(vector.x * factor) / factor,
				Mathf.Round(vector.y * factor) / factor,
				Mathf.Round(vector.z * factor) / factor
			);
		}

		private void RemoveOldestEntry()
		{
			string oldestKey = null;
			float oldestTime = float.MaxValue;

			foreach (var kvp in _cache)
			{
				if (kvp.Value.AccessTime < oldestTime)
				{
					oldestTime = kvp.Value.AccessTime;
					oldestKey = kvp.Key;
				}
			}

			if (oldestKey != null)
			{
				_cache.Remove(oldestKey);
			}
		}

		private struct CacheEntry
		{
			public float Value;
			public float AccessTime;
		}
	}
}