using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Geometry;
using TrafficManager.Util;
using TrafficManager.TrafficLight;
using TrafficManager.State;
using System.Linq;

namespace TrafficManager.Manager {
	/// <summary>
	/// Manages the states of all custom traffic lights on the map
	/// </summary>
	public class CustomSegmentLightsManager : AbstractSegmentGeometryObservingManager, ICustomSegmentLightsManager {
		public static CustomSegmentLightsManager Instance { get; private set; } = null;

		static CustomSegmentLightsManager() {
			Instance = new CustomSegmentLightsManager();
		}

		/// <summary>
		/// custom traffic lights by segment id
		/// </summary>
		private CustomSegment[] CustomSegments = new CustomSegment[NetManager.MAX_SEGMENT_COUNT];

		/// <summary>
		/// Adds custom traffic lights at the specified node and segment.
		/// Light states (red, yellow, green) are taken from the "live" state, that is the traffic light's light state right before the custom light takes control.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="startNode"></param>
		public CustomSegmentLights AddLiveSegmentLights(ushort segmentId, bool startNode) {
			SegmentGeometry segGeometry = SegmentGeometry.Get(segmentId);
			SegmentEndGeometry endGeometry = startNode ? segGeometry.StartNodeGeometry : segGeometry.EndNodeGeometry;

			if (! endGeometry.IsValid()) {
				Log.Error($"CustomTrafficLightsManager.AddLiveSegmentLights: Segment {segmentId} is not connected to a node. startNode={startNode}");
				return null;
			}

			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			RoadBaseAI.TrafficLightState vehicleLightState;
			RoadBaseAI.TrafficLightState pedestrianLightState;
			bool vehicles;
			bool pedestrians;

			RoadBaseAI.GetTrafficLightState(endGeometry.NodeId(), ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId],
				currentFrameIndex - 256u, out vehicleLightState, out pedestrianLightState, out vehicles,
				out pedestrians);

			return AddSegmentLights(segmentId, startNode,
				vehicleLightState == RoadBaseAI.TrafficLightState.Green
					? RoadBaseAI.TrafficLightState.Green
					: RoadBaseAI.TrafficLightState.Red);
		}

		/// <summary>
		/// Adds custom traffic lights at the specified node and segment.
		/// Light stats are set to the given light state, or to "Red" if no light state is given.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="startNode"></param>
		/// <param name="lightState">(optional) light state to set</param>
		public CustomSegmentLights AddSegmentLights(ushort segmentId, bool startNode, RoadBaseAI.TrafficLightState lightState=RoadBaseAI.TrafficLightState.Red) {
#if DEBUG
			Log._Debug($"CustomTrafficLights.AddSegmentLights: Adding segment light: {segmentId} @ startNode={startNode}");
#endif
			CustomSegment customSegment = CustomSegments[segmentId];
			if (customSegment == null) {
				customSegment = new CustomSegment();
				CustomSegments[segmentId] = customSegment;
			} else {
				CustomSegmentLights existingLights = startNode ? customSegment.StartNodeLights : customSegment.EndNodeLights;

				if (existingLights != null) {
					existingLights.SetLights(lightState);
					return existingLights;
				}
			}

			SubscribeToSegmentGeometry(segmentId);
			if (startNode) {
				customSegment.StartNodeLights = new CustomSegmentLights(this, segmentId, startNode, false, lightState);
				customSegment.StartNodeLights.CalculateAutoPedestrianLightState();
				return customSegment.StartNodeLights;
			} else {
				customSegment.EndNodeLights = new CustomSegmentLights(this, segmentId, startNode, false, lightState);
				customSegment.EndNodeLights.CalculateAutoPedestrianLightState();
				return customSegment.EndNodeLights;
			}
		}

		/// <summary>
		/// Removes all custom traffic lights at both ends of the given segment.
		/// </summary>
		/// <param name="segmentId"></param>
		public void RemoveSegmentLights(ushort segmentId) {
			CustomSegments[segmentId] = null;
			UnsubscribeFromSegmentGeometry(segmentId);
		}

		/// <summary>
		/// Removes the custom traffic light at the given segment end.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="startNode"></param>
		public void RemoveSegmentLight(ushort segmentId, bool startNode) {
#if DEBUG
			Log.Warning($"Removing segment light: {segmentId} @ startNode={startNode}");
#endif

			CustomSegment customSegment = CustomSegments[segmentId];
			if (customSegment == null) {
				return;
			}

			if (startNode) {
				customSegment.StartNodeLights = null;
			} else {
				customSegment.EndNodeLights = null;
			}

			if (customSegment.StartNodeLights == null && customSegment.EndNodeLights == null) {
				CustomSegments[segmentId] = null;
				UnsubscribeFromSegmentGeometry(segmentId);
			}
		}

		/// <summary>
		/// Checks if a custom traffic light is present at the given segment end.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="startNode"></param>
		/// <returns></returns>
		public bool IsSegmentLight(ushort segmentId, bool startNode) {
			CustomSegment customSegment = CustomSegments[segmentId];
			if (customSegment == null) {
				return false;
			}

			return (startNode && customSegment.StartNodeLights != null) || (!startNode && customSegment.EndNodeLights != null);
		}

		/// <summary>
		/// Retrieves the custom traffic light at the given segment end. If none exists, a new custom traffic light is created and returned.
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="startNode"></param>
		/// <returns>existing or new custom traffic light at segment end</returns>
		public CustomSegmentLights GetOrLiveSegmentLights(ushort segmentId, bool startNode) {
			if (! IsSegmentLight(segmentId, startNode))
				return AddLiveSegmentLights(segmentId, startNode);

			return GetSegmentLights(segmentId, startNode);
		}

		/// <summary>
		/// Retrieves the custom traffic light at the given segment end.
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="segmentId"></param>
		/// <returns>existing custom traffic light at segment end, <code>null</code> if none exists</returns>
		public CustomSegmentLights GetSegmentLights(ushort segmentId, bool startNode, bool add=true, RoadBaseAI.TrafficLightState lightState = RoadBaseAI.TrafficLightState.Red) {
			if (!IsSegmentLight(segmentId, startNode)) {
				if (add)
					return AddSegmentLights(segmentId, startNode, lightState);
				else
					return null;
			}

			CustomSegment customSegment = CustomSegments[segmentId];
			
			if (startNode) {
				return customSegment.StartNodeLights;
			} else {
				return customSegment.EndNodeLights;
			}
		}

		public CustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId) {
			SegmentEndGeometry endGeometry = SegmentGeometry.Get(segmentId).GetEnd(nodeId);
			if (endGeometry == null)
				return null;
			return GetSegmentLights(segmentId, endGeometry.StartNode, false);
		}

		protected override void HandleInvalidSegment(SegmentGeometry geometry) {
			RemoveSegmentLights(geometry.SegmentId);
		}

		protected override void HandleValidSegment(SegmentGeometry geometry) {
			
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			CustomSegments = new CustomSegment[NetManager.MAX_SEGMENT_COUNT];
		}

		
	}
}
