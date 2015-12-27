using ColossalFramework;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;

namespace TrafficManager.CustomAI {
	class CustomHumanAI {
		public bool CustomCheckTrafficLights(ushort node, ushort segment) {
			var nodeSimulation = TrafficPriority.GetNodeSimulation(node);

			var instance = Singleton<NetManager>.instance;
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			var num = (uint)((node << 8) / 32768);
			var num2 = currentFrameIndex - num & 255u;

			RoadBaseAI.TrafficLightState pedestrianLightState;

			if (nodeSimulation == null || (nodeSimulation.FlagTimedTrafficLights && !nodeSimulation.TimedTrafficLightsActive)) {
				RoadBaseAI.TrafficLightState vehicleLightState;
				bool vehicles;
				bool pedestrians;

				RoadBaseAI.GetTrafficLightState(node, ref instance.m_segments.m_buffer[segment], currentFrameIndex - num, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);
				if (pedestrianLightState == RoadBaseAI.TrafficLightState.GreenToRed && !pedestrians && num2 >= 196u)
					RoadBaseAI.SetTrafficLightState(node, ref instance.m_segments.m_buffer[segment], currentFrameIndex - num, vehicleLightState, pedestrianLightState, vehicles, true);
			} else
				pedestrianLightState = TrafficLightsManual.GetSegmentLight(node, segment).GetLightPedestrian();

			switch (pedestrianLightState) {
				case RoadBaseAI.TrafficLightState.RedToGreen:
					if (num2 < 60u) {
						return false;
					}
					break;
				case RoadBaseAI.TrafficLightState.Red:
				case RoadBaseAI.TrafficLightState.GreenToRed:
					return false;
			}
			return true;
		}
	}
}
