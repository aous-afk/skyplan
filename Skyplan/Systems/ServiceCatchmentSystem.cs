using Colossal.Mathematics;
using Colossal.UI.Binding;
using Game;
using Game.Citizens;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Game.UI;
using Newtonsoft.Json;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using BuildingSchool = Game.Buildings.School;
using BuildingStudent = Game.Buildings.Student;
using BuildingHospital = Game.Buildings.Hospital;
using BuildingPatient = Game.Buildings.Patient;
using ObjectTransform = Game.Objects.Transform;
using PropertyRenter = Game.Buildings.PropertyRenter;

namespace Skyplan.Systems {

	// Scaffold: click a school or hospital in-game (native selection), see where its
	// students/patients live. Proof-of-concept for custom (non-vanilla) info-view style
	// overlays - originally school-only (see dev_doc.md), generalized here to a second
	// facility type to prove the pattern reuses. No Burst jobs, kept deliberately simple.
	// Home/facility markers are drawn via the game's own OverlayRenderSystem (world-space,
	// same system tool previews use) - no screen projection/camera-sync needed for those.
	// Only the legend box is still an HTML/SVG overlay, so it still needs a screen anchor.
	internal enum FacilityKind {
		None,
		School,
		Hospital,
	}

	internal class CatchmentLegendDto {
		public string name;
		public int capacity;
		public int enrolled;
		public CatchmentScreenPt facilityPos;
	}

	internal class CatchmentScreenPt {
		public float x;
		public float y;
	}

	public partial class ServiceCatchmentSystem : UISystemBase {
		private static readonly Color SchoolColor = new(0.98f, 0.8f, 0.08f, 1f);
		private static readonly Color HospitalColor = new(1f, 0.27f, 0.53f, 1f);
		private static readonly Color HomeColor = new(0.27f, 0.87f, 1f, 0.85f);
		private static readonly Color LineColor = new(0.27f, 0.87f, 1f, 0.25f);

		private ToolSystem m_ToolSystem;
		private ICameraSystem m_Camera;
		private OverlayRenderSystem m_OverlaySystem;
		private Entity m_LastSelected;
		private ValueBinding<string> m_CatchmentBinding;

		protected override void OnCreate() {
			base.OnCreate();
			m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
			m_Camera = World.GetOrCreateSystemManaged<CameraSystem>();
			m_OverlaySystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
			m_CatchmentBinding = new ValueBinding<string>("skyplan", "catchment", "");
			AddBinding(m_CatchmentBinding);
		}

		protected override void OnUpdate() {
			if (DrawingSystem.instance?.IsPanelVisible != true) {
				if (m_LastSelected != Entity.Null) m_CatchmentBinding.Update("");
				m_LastSelected = Entity.Null;
				return;
			}

			Entity selected = m_ToolSystem.selected;
			FacilityKind kind = DetectFacility(selected);

			if (kind == FacilityKind.None || !m_Camera.IsReady) {
				if (m_LastSelected != Entity.Null) m_CatchmentBinding.Update("");
				m_LastSelected = Entity.Null;
				return;
			}

			// Recomputed every frame (not just on selection change) so the legend's screen
			// anchor tracks the camera - the world-space dots already do this for free,
			// the legend can't since it's still a flat HTML/SVG overlay.
			m_LastSelected = selected;
			m_CatchmentBinding.Update(JsonConvert.SerializeObject(BuildLegend(selected, kind)));

			DrawCatchmentOverlay(selected, kind);
		}

		private FacilityKind DetectFacility(Entity entity) {
			if (entity == Entity.Null) return FacilityKind.None;
			if (EntityManager.HasComponent<BuildingSchool>(entity)) return FacilityKind.School;
			if (EntityManager.HasComponent<BuildingHospital>(entity)) return FacilityKind.Hospital;
			return FacilityKind.None;
		}

		private List<Entity> GetClients(Entity facility, FacilityKind kind) {
			List<Entity> clients = [];
			switch (kind) {
				case FacilityKind.School:
					if (EntityManager.HasBuffer<BuildingStudent>(facility))
						foreach (BuildingStudent s in EntityManager.GetBuffer<BuildingStudent>(facility, isReadOnly: true))
							clients.Add(s.m_Student);
					break;
				case FacilityKind.Hospital:
					if (EntityManager.HasBuffer<BuildingPatient>(facility))
						foreach (BuildingPatient p in EntityManager.GetBuffer<BuildingPatient>(facility, isReadOnly: true))
							clients.Add(p.m_Patient);
					break;
			}
			return clients;
		}

		private int GetCapacity(Entity facility, FacilityKind kind) {
			if (!EntityManager.HasComponent<PrefabRef>(facility)) return 0;
			Entity prefab = EntityManager.GetComponentData<PrefabRef>(facility).m_Prefab;
			return kind switch {
				FacilityKind.School when EntityManager.HasComponent<SchoolData>(prefab)
					=> EntityManager.GetComponentData<SchoolData>(prefab).m_StudentCapacity,
				FacilityKind.Hospital when EntityManager.HasComponent<HospitalData>(prefab)
					=> EntityManager.GetComponentData<HospitalData>(prefab).m_PatientCapacity,
				_ => 0
			};
		}

		private static string NameFor(FacilityKind kind, Entity entity) => kind switch {
			FacilityKind.School => $"School {entity.Index}",
			FacilityKind.Hospital => $"Hospital {entity.Index}",
			_ => "Facility"
		};

		private static Color MarkerColorFor(FacilityKind kind) => kind switch {
			FacilityKind.Hospital => HospitalColor,
			_ => SchoolColor
		};

		private CatchmentLegendDto BuildLegend(Entity facility, FacilityKind kind) {
			Vector2 screen = m_Camera.WorldToSVG(ToVector3(WorldPositionOf(facility)));
			return new CatchmentLegendDto {
				name = NameFor(kind, facility),
				capacity = GetCapacity(facility, kind),
				enrolled = GetClients(facility, kind).Count,
				facilityPos = new CatchmentScreenPt { x = screen.x, y = screen.y },
			};
		}

		private void DrawCatchmentOverlay(Entity facility, FacilityKind kind) {
			List<Entity> clients = GetClients(facility, kind);
			if (clients.Count == 0) return;

			float3 facilityPos = WorldPositionOf(facility);
			OverlayRenderSystem.Buffer buf = m_OverlaySystem.GetBuffer(out JobHandle deps);
			deps.Complete();

			buf.DrawCircle(MarkerColorFor(kind), facilityPos, 30f);

			foreach (Entity citizen in clients) {
				Entity home = HomeOf(citizen);
				if (home == Entity.Null || !EntityManager.HasComponent<ObjectTransform>(home)) continue;
				float3 homePos = WorldPositionOf(home);
				buf.DrawLine(LineColor, new Line3.Segment(facilityPos, homePos), 1f);
				buf.DrawCircle(HomeColor, homePos, 8f);
			}

			m_OverlaySystem.AddBufferWriter(default);
		}

		private Entity HomeOf(Entity citizen) {
			if (citizen == Entity.Null || !EntityManager.Exists(citizen)) return Entity.Null;
			if (!EntityManager.HasComponent<HouseholdMember>(citizen)) return Entity.Null;
			Entity household = EntityManager.GetComponentData<HouseholdMember>(citizen).m_Household;
			if (household == Entity.Null || !EntityManager.HasComponent<PropertyRenter>(household)) return Entity.Null;
			return EntityManager.GetComponentData<PropertyRenter>(household).m_Property;
		}

		private float3 WorldPositionOf(Entity entity) {
			if (entity == Entity.Null || !EntityManager.HasComponent<ObjectTransform>(entity)) return float3.zero;
			return EntityManager.GetComponentData<ObjectTransform>(entity).m_Position;
		}

		private static Vector3 ToVector3(float3 v) => new(v.x, v.y, v.z);
	}
}
