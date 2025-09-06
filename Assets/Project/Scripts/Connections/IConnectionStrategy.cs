using System.Collections.Generic;

namespace RFSimulation.Connections
{
	/// <summary>
	/// Interface for different connection strategies between transmitters and receivers
	/// </summary>
	public interface IConnectionStrategy
	{
		/// <summary>
		/// Name of the strategy for UI display
		/// </summary>
		string StrategyName { get; }

		/// <summary>
		/// Description of how this strategy works
		/// </summary>
		string Description { get; }

		StrategyType StrategyType { get; }

        /// <summary>
        /// Update connections between transmitters and receivers
        /// </summary>
        /// <param name="transmitters">List of available transmitters</param>
        /// <param name="receivers">List of receivers to connect</param>
        /// <param name="settings">Connection settings (thresholds, margins, etc.)</param>
        void UpdateConnections(
			List<Core.Transmitter> transmitters,
			List<Core.Receiver> receivers,
			ConnectionSettings settings
		);
	}

	/// <summary>
	/// Settings for connection algorithms
	/// </summary>
	[System.Serializable]
	public class ConnectionSettings
	{
		[UnityEngine.Header("Signal Thresholds")]
		public float minimumSignalThreshold = -90f; // dBm
		public float connectionMargin = 10f; // dB above sensitivity
		public float handoverMargin = 3f; // dB to prevent ping-ponging

		[UnityEngine.Header("Quality Requirements")]
		public float minimumSINR = -6f; // dB
		public float excellentSignalThreshold = 15f; // dB above sensitivity
		public float goodSignalThreshold = 10f; // dB above sensitivity

		[UnityEngine.Header("Multi-Connection Settings")]
		public int maxSimultaneousConnections = 3;
		public bool allowLoadBalancing = true;

		[UnityEngine.Header("Debug")]
		public bool enableDebugLogs = false;
	}

    public enum StrategyType
    {
        StrongestSignal,
        BestServerWithInterference,
        LoadBalanced,
        QualityFirst,
        EmergencyCoverage,
        NearestTransmitter
    }
}