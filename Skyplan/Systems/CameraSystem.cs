using Colossal.UI;
using Game;
using Game.Simulation;
using Unity.Mathematics;
using UnityEngine;

namespace Skyplan.Systems {
	public partial class CameraSystem : GameSystemBase, ICameraSystem {

		#region Core
		private Vector3 m_LastPos;
		private Quaternion m_LastRot;
		private float m_LastFov;
		private TerrainSystem m_TerrainSystem;

		public bool IsReady => GetCamera() != null;

		protected override void OnCreate() {
			base.OnCreate();
			m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
		}

		protected override void OnUpdate() { }

		public bool HasChanged() {
			Camera cam = GetCamera();
			if (cam == null) return false;
			Vector3 pos = cam.transform.position;
			Quaternion rot = cam.transform.rotation;
			float fov = cam.fieldOfView;
			bool same = pos == m_LastPos && rot == m_LastRot && fov == m_LastFov;
			if (!same) { m_LastPos = pos; m_LastRot = rot; m_LastFov = fov; }
			return !same;
		}

		public Vector2 WorldToSVG(Vector3 world) {
			Camera cam = GetCamera();
			if (cam == null) return Vector2.zero;
			// worldToCameraMatrix is stale in CS2 (set once, never updated);
			// derive the view matrix from the live transform instead.
			Matrix4x4 w2c = Matrix4x4.Scale(new Vector3(1, 1, -1)) * cam.transform.worldToLocalMatrix;
			return ProjectWithMatrices(world, w2c, cam.projectionMatrix, cam.pixelWidth, cam.pixelHeight);
		}

		public bool ScreenToWorld(float sx, float sy, out Vector3 world) {
			Camera cam = GetCamera();
			if (cam == null) { world = Vector3.zero; return false; }
			Ray ray = cam.ScreenPointToRay(new Vector3(sx, cam.pixelHeight - sy, 0f));

			// Step 1: intersect Y=0 to get approximate XZ
			if (Mathf.Abs(ray.direction.y) < 0.0001f) { world = Vector3.zero; return false; }
			float t0 = -ray.origin.y / ray.direction.y;
			if (t0 < 0f) { world = Vector3.zero; return false; }
			Vector3 approx = ray.origin + (ray.direction * t0);

			// Step 2: sample actual terrain Y at that XZ
			float terrainY = SampleTerrainHeight(approx.x, approx.z);

			// Step 3: re-intersect ray with Y=terrainY plane
			float dt = ray.origin.y - terrainY;
			float t1 = dt / (-ray.direction.y);
			world = t1 > 0f ? ray.origin + ray.direction * t1 : approx;
			world.y = terrainY;
			return true;
		}

		private float SampleTerrainHeight(float wx, float wz) {
			if (m_TerrainSystem == null) return 0f;
			TerrainHeightData heightData = m_TerrainSystem.GetHeightData();
			return TerrainUtils.SampleHeight(ref heightData, new float3(wx, 0, wz));
		}

		private static Camera GetCamera() =>
			UIManager.defaultUISystem.defaultUIView.RenderingCamera ?? Camera.main;
		#endregion

		#region CSS Transform / Baseline
		private static Vector2 ProjectWithMatrices(Vector3 world, Matrix4x4 worldToCam, Matrix4x4 proj, int pw, int ph) {
			Vector4 viewPos = worldToCam * new Vector4(world.x, world.y, world.z, 1f);
			Vector4 clip = proj * viewPos;
			if (clip.w <= 0f) return Vector2.zero;
			float sx = (clip.x / clip.w + 1f) * 0.5f * pw;
			float sy = (clip.y / clip.w + 1f) * 0.5f * ph;
			return new Vector2(sx, ph - sy);
		}

		private readonly Plane[] m_FrustumPlanes = new Plane[6];
		private bool m_FrustumValid;

		public void RefreshFrustum() {
		  Camera cam = GetCamera();
		  m_FrustumValid = cam != null;
		  if (m_FrustumValid) GeometryUtility.CalculateFrustumPlanes(cam, m_FrustumPlanes);
		}

		public bool IsInView(Vector3 min, Vector3 max) {
		  if (!m_FrustumValid) return false;
		  Bounds b = default;
		  b.SetMinMax(min, max);
		  return GeometryUtility.TestPlanesAABB(m_FrustumPlanes, b);
		}

		public bool IsInView(Bounds extents) {
		  if (!m_FrustumValid) return false;
		  return GeometryUtility.TestPlanesAABB(m_FrustumPlanes, extents);
		}

	#endregion
  }
}
