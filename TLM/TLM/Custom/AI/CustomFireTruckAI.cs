﻿#define PATHRECALCx

using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Geometry;
using TrafficManager.Manager;
using TrafficManager.Traffic;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	class CustomFireTruckAI : CarAI {
		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
#if DEBUG
			//Log._Debug($"CustomFireTruckAI.CustomStartPathFind called for vehicle {vehicleID}");
#endif

#if PATHRECALC
			VehicleState state = VehicleStateManager._GetVehicleState(vehicleID);
			bool recalcRequested = state.PathRecalculationRequested;
			state.PathRecalculationRequested = false;
#endif
			ExtVehicleType vehicleType = (vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0 ? ExtVehicleType.Emergency : ExtVehicleType.Service;
			VehicleStateManager.Instance._GetVehicleState(vehicleID).VehicleType = vehicleType;

			VehicleInfo info = this.m_info;
			bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != 0;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float startDistSqrA;
			float startDistSqrB;
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float endDistSqrA;
			float endDistSqrB;
			if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out startDistSqrA, out startDistSqrB) &&
				CustomPathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, undergroundTarget, false, 32f, out endPosA, out endPosB, out endDistSqrA, out endDistSqrB)) {
				if (!startBothWays || startDistSqrA < 10f) {
					startPosB = default(PathUnit.Position);
				}
				if (!endBothWays || endDistSqrA < 10f) {
					endPosB = default(PathUnit.Position);
				}
				uint path;
				if (CustomPathManager._instance.CreatePath(
#if PATHRECALC
					recalcRequested,
#endif
					vehicleType, vehicleID, ExtCitizenInstance.ExtPathType.None, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false)) {
#if USEPATHWAITCOUNTER
					VehicleState state = VehicleStateManager.Instance._GetVehicleState(vehicleID);
					state.PathWaitCounter = 0;
#endif

					if (vehicleData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					}
					vehicleData.m_path = path;
					vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
					return true;
				}
			}
			return false;
		}
	}
}
