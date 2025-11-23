using UnityEngine;
using System.Collections.Generic;
using RFSimulation.Interfaces;
using RFSimulation.Propagation.Models;
using RFSimulation.Visualization;

namespace RFSimulation.Propagation.Core
{

    public class PathLossCalculator
    {
        [Header("Urban Settings")]
        public bool fallbackToBasicModels = true;

        private readonly Dictionary<PropagationModel, IPathLossModel> _models;
        private readonly PathLossCache _cache;

        public RayVisualization RayViz { get; set; }

        public PathLossCalculator()
        {
            _cache = new PathLossCache();

            _models = new Dictionary<PropagationModel, IPathLossModel>
            {
                { PropagationModel.FreeSpace, new FreeSpaceModel() },
                { PropagationModel.LogD, new LogDModel() },
                { PropagationModel.LogNShadow, new LogNShadowingModel() },
                { PropagationModel.Hata, new HataModel() },
                { PropagationModel.COST231, new COST231Model() },
                { PropagationModel.RayTracing, new RayTracingModel() },
            };

            ConfigureRayTracingModel();

        }

        private void ConfigureRayTracingModel()
        {
            var rt = GetRayTracingModel();
            if (rt == null) return;

            rt.enableRayVisualization = true;
            rt.Visualizer = RayViz ?? UnityEngine.Object.FindAnyObjectByType<RayVisualization>();
        }

        public float CalculateReceivedPower(PropagationContext context)
        {
            if (!context.IsValid(out string error))
            {
                Debug.LogWarning($"[PathLoss] Invalid context: {error}");
                return float.NegativeInfinity;
            }

            if (_cache.TryGetValue(context, out float cachedResult))
                return cachedResult;

            float receivedPowerDbm;
            IPathLossModel model = null;
            try
            {
                if (!_models.TryGetValue(context.Model, out model))
                {
                    Debug.LogWarning($"[PathLoss] No path-loss model registered for {context.Model}. Falling back.");
                    if (fallbackToBasicModels)
                    {
                        float fallbackOut = new FreeSpaceModel().CalculatePathLoss(context);
                        receivedPowerDbm = PathLossToReceivedPower(fallbackOut, context);
                    }
                    else
                    {
                        receivedPowerDbm = float.NegativeInfinity;
                    }
                    _cache.Store(context, receivedPowerDbm);
                    return receivedPowerDbm;
                }

                float pathLossDb = model.CalculatePathLoss(context);
                if (float.IsNaN(pathLossDb) || float.IsNegativeInfinity(pathLossDb)) pathLossDb = float.PositiveInfinity;

                receivedPowerDbm = PathLossToReceivedPower(pathLossDb, context);
                if (float.IsPositiveInfinity(pathLossDb)) receivedPowerDbm = float.NegativeInfinity;

            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PathLoss] Error with {model.ModelName}: {e.Message}");
                if (fallbackToBasicModels)
                {
                    float fallbackOut = new FreeSpaceModel().CalculatePathLoss(context);
                    receivedPowerDbm = PathLossToReceivedPower(fallbackOut, context);
                }
                else
                {
                    receivedPowerDbm = float.NegativeInfinity;
                }
            }

            _cache.Store(context, receivedPowerDbm);
            return receivedPowerDbm;
        }

        public float PathLossToReceivedPower(float pathLossDb, PropagationContext context)
        {
            float txDbm = context.TransmitterPowerDbm;
            float txGainDbi = context.AntennaGainDbi;
            float rxGainDbi = context.ReceiverGainDbi;
            return txDbm + txGainDbi + rxGainDbi - pathLossDb;
        }

        public RayTracingModel GetRayTracingModel()
        {
            if (_models.TryGetValue(PropagationModel.RayTracing, out IPathLossModel model))
            {
                return model as RayTracingModel;
            }
            return null;
        }

        public void ClearCache()
        {
            _cache.Clear();
        }

    }
}