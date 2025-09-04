using RFSimulation.Propagation.Core;

namespace RFSimulation.Interfaces
{
	public interface IPathLossModel
	{
		float Calculate(PropagationContext context);
		string ModelName { get; }
	}
}