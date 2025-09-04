using RFSimulation.Propagation.Core;
using RFSimulation.Propagation.SignalQuality;

namespace RFSimulation.Interfaces
{
	public interface ISignalAnalyzer
	{
		SignalQualityMetrics AnalyzeSignal(float receivedPowerDbm, TechnologyType technology);
		float CalculateSINR(float signalPowerDbm, float interferencePowerDbm, float noisePowerDbm);
		float EstimateThroughput(float sinrDb, TechnologyType technology);
	}
}