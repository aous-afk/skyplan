using Colossal.UI;
using Game;
using Game.Simulation;
using System.Globalization;
using Unity.Mathematics;
using UnityEngine;

namespace skyplan.Systems {
	public partial class CameraSystem : GameSystemBase, ICameraSystem {

		#region Core
		private Matrix4x4 m_LastMatrix;
		private bool m_HasBaseline;
		private TerrainSystem m_TerrainSystem;

		public bool IsReady => m_HasBaseline && GetCamera() != null;

		protected override void OnCreate() {
			base.OnCreate();
			m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
		}

		protected override void OnUpdate() { }

		public bool HasChanged() {
			if (!m_HasBaseline) return false;
			Camera cam = GetCamera();
			if (cam == null) return false;
			bool same = cam.worldToCameraMatrix == m_LastMatrix;
			if (!same) m_LastMatrix = cam.worldToCameraMatrix;
			return !same;
		}

		public Vector2 WorldToSVG(Vector3 world) {
			Camera cam = GetCamera();
			if (cam == null) return Vector2.zero;
			Vector3 s = cam.WorldToScreenPoint(world);
			return new Vector2(s.x, cam.pixelHeight - s.y);
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
		private Matrix4x4 m_BaselineWorldToCamera;
		private Matrix4x4 m_BaselineProjection;
		private int m_BaselinePixelW;
		private int m_BaselinePixelH;
		private Vector2[] m_BaselineAnchors = new Vector2[3];

		private static readonly Vector3[] k_Anchors = {
			new(   0, 0,   0),
			new( 500, 0,   0),
			new(   0, 0, 500),
		};

		public void SetBaseline() {
			Camera cam = GetCamera();
			if (cam == null) return;
			m_LastMatrix = cam.worldToCameraMatrix;
			m_BaselineWorldToCamera = cam.worldToCameraMatrix;
			m_BaselineProjection = cam.projectionMatrix;
			m_BaselinePixelW = cam.pixelWidth;
			m_BaselinePixelH = cam.pixelHeight;
			for (int i = 0; i < k_Anchors.Length; i++)
				m_BaselineAnchors[i] = ProjectWithMatrices(k_Anchors[i], m_BaselineWorldToCamera, m_BaselineProjection, m_BaselinePixelW, m_BaselinePixelH);
			m_HasBaseline = true;
			Mod.log.Info($"CameraSystem.SetBaseline: cam={cam.name} px={cam.pixelWidth}x{cam.pixelHeight}");
		}

		public Vector2 WorldToSVGBaseline(Vector3 world) {
			if (!m_HasBaseline) return Vector2.zero;
			return ProjectWithMatrices(world, m_BaselineWorldToCamera, m_BaselineProjection, m_BaselinePixelW, m_BaselinePixelH);
		}

		public string ComputeTransformMatrix() {
			if (!m_HasBaseline) return null;
			Camera cam = GetCamera();
			if (cam == null) return null;

			Vector2[] cur = new Vector2[3];
			for (int i = 0; i < k_Anchors.Length; i++)
				cur[i] = WorldToSVG(k_Anchors[i]);

			// SVG matrix(a,b,c,d,e,f): x'=a*x+c*y+e  y'=b*x+d*y+f
			// Solve affine from 3 baseline→current anchor pairs via Cramer's rule
			Vector2[] src = m_BaselineAnchors;
			float x1 = src[0].x, y1 = src[0].y;
			float x2 = src[1].x, y2 = src[1].y;
			float x3 = src[2].x, y3 = src[2].y;
			float X1 = cur[0].x, Y1 = cur[0].y;
			float X2 = cur[1].x, Y2 = cur[1].y;
			float X3 = cur[2].x, Y3 = cur[2].y;

			float det = x1 * (y2 - y3) - x2 * (y1 - y3) + x3 * (y1 - y2);
			if (Mathf.Abs(det) < 0.0001f) return null;

			float a = (X1 * (y2 - y3) - X2 * (y1 - y3) + X3 * (y1 - y2)) / det;
			float c = (x1 * (X2 - X3) - x2 * (X1 - X3) + x3 * (X1 - X2)) / det;
			float e = (x1 * (y2 * X3 - y3 * X2) - x2 * (y1 * X3 - y3 * X1) + x3 * (y1 * X2 - y2 * X1)) / det;
			float b = (Y1 * (y2 - y3) - Y2 * (y1 - y3) + Y3 * (y1 - y2)) / det;
			float d = (x1 * (Y2 - Y3) - x2 * (Y1 - Y3) + x3 * (Y1 - Y2)) / det;
			float f = (x1 * (y2 * Y3 - y3 * Y2) - x2 * (y1 * Y3 - y3 * Y1) + x3 * (y1 * Y2 - y2 * Y1)) / det;

			return $"matrix({F(a)},{F(b)},{F(c)},{F(d)},{F(e)},{F(f)})";
		}

		private static Vector2 ProjectWithMatrices(Vector3 world, Matrix4x4 worldToCam, Matrix4x4 proj, int pw, int ph) {
			Vector4 viewPos = worldToCam * new Vector4(world.x, world.y, world.z, 1f);
			Vector4 clip = proj * viewPos;
			if (clip.w <= 0f) return Vector2.zero;
			float sx = (clip.x / clip.w + 1f) * 0.5f * pw;
			float sy = (clip.y / clip.w + 1f) * 0.5f * ph;
			return new Vector2(sx, ph - sy);
		}

		private static string F(float v) => v.ToString("F6", CultureInfo.InvariantCulture);
		#endregion
	}
}
