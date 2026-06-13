using System.IO;
using System.Linq;
using Unity.Entities;
using Colossal.PSI.Environment;
using Skyplan.Models;
using SkyPlan.Export;

namespace skyplan.Systems {
	public partial class ExportSystem : SystemBase {
		public static ExportSystem Instance() {
			return World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ExportSystem>();
		}

		public void ExportToGeoJson(int srid, double originX, double originY) {
			DrawingSystem drawingSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DrawingSystem>();
			var shapes = drawingSystem.m_Shapes
				.Where(s => s.pts.Count >= 2 && s.pts[0] != s.pts[s.pts.Count - 1])
				.ToList();

			Mod.log.Info($"Exporting {shapes.Count} shapes (SRID={srid})");

			string json = GeoJsonExporter.Export(shapes, srid, originX, originY);

			string modDataPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", nameof(skyplan));
			Directory.CreateDirectory(modDataPath);
			File.WriteAllText(Path.Combine(modDataPath, "Plan_1.geojson"), json);

			Mod.log.Info($"Exported to {modDataPath}\\Plan_1.geojson");
		}

		protected override void OnUpdate() { }
	}
}
