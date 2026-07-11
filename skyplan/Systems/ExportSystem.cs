using System.IO;
using System.Linq;
using Unity.Entities;
using Colossal.PSI.Environment;
using Skyplan.Models;
using Skyplan.Persistence;
using System.Collections.Generic;

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

		public void ExportToSVG(string fileName = "Plan_1.svg") {
			DrawingSystem drawingSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DrawingSystem>();
			var shapes = drawingSystem.m_Shapes
				.Where(s => s.pts.Count >= 2)
				.ToList();

			Mod.log.Info($"Exporting {shapes.Count} shapes to svg as {fileName}");

			string svg = SVGExporter.Export(shapes);

			string modDataPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", nameof(skyplan));
			Directory.CreateDirectory(modDataPath);
			File.WriteAllText(Path.Combine(modDataPath, fileName), svg);

			Mod.log.Info($"Exported to {modDataPath}\\{fileName}");
		}

		public void ImportFromSVG(string fileName) {
			string modDataPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", nameof(skyplan));
			string filePath = Path.Combine(modDataPath, fileName);
			if (!File.Exists(filePath)) {
				Mod.log.Warn($"[Skyplan] Import: file not found: {filePath}");
				return;
			}

			string svg = File.ReadAllText(filePath);
			DrawingSystem drawing = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DrawingSystem>();
			int nextId = drawing.m_NextId;
			List<Shape> shapes = SVGImporter.Import(svg, ref nextId);
			drawing.m_NextId = nextId;
			drawing.LoadShapes(shapes);
			Mod.log.Info($"[Skyplan] Imported {shapes.Count} shapes from {fileName}");
		}

		protected override void OnUpdate() { }
	}
}
