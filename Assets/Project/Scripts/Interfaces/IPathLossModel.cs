using RFSimulation.Propagation.Core;

namespace RFSimulation.Interfaces
{
	public interface IPathLossModel
	{
		float CalculatePathLoss(PropagationContext context);
		string ModelName { get; }
	}
}