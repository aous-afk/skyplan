using Colossal.Serialization.Entities;
using Game;
using Game.City;
using Game.SceneFlow;
using Skyplan.Cross;
using Skyplan.Models;
using Skyplan.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Skyplan.Systems {
	public partial class PlanPersistenceSystem : GameSystemBase {
		public static PlanPersistenceSystem instance;

		private bool m_WasInGame;
		private string m_PlanFilePath;
		private float m_LastAutosaveTime;
		private const float AutosaveIntervalSeconds = 30f;

		protected override void OnCreate() {
			base.OnCreate();
			instance = this;
		}

		protected override void OnUpdate() {
			bool inGame = GameManager.instance != null &&
				(GameManager.instance.gameMode & GameMode.Game) != 0;

			if (!inGame && m_WasInGame) {
				SavePlan();
			}
			m_WasInGame = inGame;

			if (inGame && UnityEngine.Time.realtimeSinceStartup - m_LastAutosaveTime >= AutosaveIntervalSeconds) {
				m_LastAutosaveTime = UnityEngine.Time.realtimeSinceStartup;
				SavePlan();
			}
		}

		protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode) {
			base.OnGameLoadingComplete(purpose, mode);
			if ((mode & GameMode.Game) == 0) return;
			m_PlanFilePath = null;
			LoadLatestPlan();
		}

		public void SavePlan() {
			DrawingSystem drawing = DrawingSystem.instance;
			if (drawing == null) return;
			if (drawing.m_Shapes.Count == 0 && m_PlanFilePath == null) return;
			try {
				string path = GetPlanFilePath();
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, PlanPersistence.Export(drawing.m_Shapes));
			} catch (Exception ex) {
				Mod.log.Warn($"[Skyplan] Failed to save plan: {ex.Message}");
			}
		}

		public void ImportFromSVG(string fileName) {
			string filePath = Path.Combine(Paths.ModDataPath, fileName);
			if (!File.Exists(filePath)) {
				Mod.log.Warn($"[Skyplan] Import: file not found: {filePath}");
				return;
			}
			DrawingSystem drawing = DrawingSystem.instance;
			if (drawing == null) return;

			string svg = File.ReadAllText(filePath);
			int nextId = drawing.m_NextId;
			List<Shape> shapes = SVGImporter.Import(svg, ref nextId);
			drawing.m_NextId = nextId;
			drawing.LoadShapes(shapes);
			Mod.log.Info($"[Skyplan] Imported {shapes.Count} shapes from {fileName}");
		}

		private void LoadLatestPlan() {
			DrawingSystem drawing = DrawingSystem.instance;
			if (drawing == null) return;
			try {
				string city = SanitizeFileName(GetCityName());
				Directory.CreateDirectory(Paths.PlansDir);
				string latest = Directory.GetFiles(Paths.PlansDir, $"{city}_*.json")
					.OrderByDescending(f => f)
					.FirstOrDefault();
				if (latest == null || latest == m_PlanFilePath) return;
				List<Shape> shapes = PlanPersistence.Import(File.ReadAllText(latest), ref drawing.m_NextId);
				drawing.LoadShapes(shapes);
				m_PlanFilePath = latest;
				Mod.log.Info($"[Skyplan] Loaded plan from {latest}");
			} catch (Exception ex) {
				Mod.log.Warn($"[Skyplan] Failed to load plan: {ex.Message}");
			}
		}

		private string GetPlanFilePath() {
			if (m_PlanFilePath == null) {
				string city = SanitizeFileName(GetCityName());
				string stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
				m_PlanFilePath = Path.Combine(Paths.PlansDir, $"{city}_{stamp}.json");
			}
			return m_PlanFilePath;
		}

		private string GetCityName() {
			string name = World.GetOrCreateSystemManaged<CityConfigurationSystem>()?.cityName;
			return string.IsNullOrWhiteSpace(name) ? "UnnamedCity" : name;
		}

		private static string SanitizeFileName(string name) {
			foreach (char c in Path.GetInvalidFileNameChars())
				name = name.Replace(c, '_');
			return name;
		}
	}
}
