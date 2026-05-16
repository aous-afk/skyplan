using System;
using System.IO;
using System.Text;
using Unity.Entities;

namespace skyplan.Systems {
	public partial class ExportSystem : SystemBase {
		public static ExportSystem Instance() {
			return World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<ExportSystem>();
		}
		public void ExportToGeoJson() {
			DrawingSystem drawingSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DrawingSystem>();
			var shaps = drawingSystem.m_Shapes;
			StringBuilder sb = new();
			foreach (var shap in shaps) {
				foreach (var pt in shap.pts) {
					sb.AppendLine($"{pt.x} , {pt.y}");
				}
			}
			string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			File.AppendAllText(Path.Combine(docPath, "WriteFile.txt"), sb.ToString());
		}

		protected override void OnUpdate() {
		}
	}
}
