using RFSimulation.Propagation.Core;

namespace RFSimulation.Interfaces
{
    public interface IObstacleCalculator
    {
        float CalculatePenetrationLoss(PropagationContext context);
        bool HasLineOfSight(PropagationContext context);
        float CalculateHandoverProbability(float currentSINR, float targetSINR, float margin = 3f);
    }
}