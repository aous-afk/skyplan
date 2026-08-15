using System.IO;
using System.Linq;
using Unity.Entities;
using Skyplan.Models;
using Skyplan.Persistence;
using System.Collections.Generic;
using Skyplan.Cross;

namespace Skyplan.Systems {
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

			Directory.CreateDirectory(Paths.ModDataPath);
			File.WriteAllText(Path.Combine(Paths.ModDataPath, "Plan_1.geojson"), json);

			Mod.log.Info($"Exported to {Paths.ModDataPath}\\Plan_1.geojson");
		}

		public void ExportToSVG(string fileName = "Plan_1.svg") {
			DrawingSystem drawingSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DrawingSystem>();
			var shapes = drawingSystem.m_Shapes
				.Where(s => s.pts.Count >= 1)
				.ToList();

			Mod.log.Info($"Exporting {shapes.Count} shapes to svg as {fileName}");

			string svg = SVGExporter.Export(shapes);

			Directory.CreateDirectory(Paths.ModDataPath);
			File.WriteAllText(Path.Combine(Paths.ModDataPath, fileName), svg);

			Mod.log.Info($"Exported to {Paths.ModDataPath}\\{fileName}");
		}

		public void ImportFromSVG(string fileName) {
			string filePath = Path.Combine(Paths.ModDataPath, fileName);
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
