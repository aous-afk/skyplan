using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Input;
using Game.Modding;
using Game.SceneFlow;
using Skyplan.Persistence.Helpers;
using Skyplan.Systems;
using System.IO;

namespace Skyplan {
	public class Mod : IMod {
		public static ILog log = LogManager.GetLogger($"{nameof(Skyplan)}.{nameof(Mod)}").SetShowsErrorsInUI(true);
		public static Mod instance;
		public static string modPath;

		private Setting m_Setting;

		public static ProxyAction m_ToggleAction;
		public const string kToggleActionName = "ToggleDrawingPanel";

		public void OnLoad(UpdateSystem updateSystem) {
			log.Info(nameof(OnLoad));
			instance = this;
			LayerMerger.LogInfo = msg => log.Info(msg);
			LayerMerger.LogDebug = msg => log.Debug(msg);
			LayerMerger.LogWarn = msg => log.Warn(msg);

			if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset)) {
				log.Info($"Current mod asset at {asset.path}");
				modPath = Path.GetDirectoryName(asset.path);
			}

			m_Setting = new Setting(this);
			m_Setting.RegisterInOptionsUI();
			GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));
			m_Setting.RegisterKeyBindings();

			m_ToggleAction = m_Setting.GetAction(kToggleActionName);
			m_ToggleAction.shouldBeEnabled = true;

			updateSystem.UpdateAt<DrawingSystem>(SystemUpdatePhase.UIUpdate);
			updateSystem.UpdateAt<PlanPersistenceSystem>(SystemUpdatePhase.MainLoop);
			updateSystem.UpdateAt<ServiceCatchmentSystem>(SystemUpdatePhase.UIUpdate);

			AssetDatabase.global.LoadSettings(nameof(Skyplan), m_Setting, new Setting(this));
		}

		public void OnDispose() {
			log.Info(nameof(OnDispose));
			m_Setting?.UnregisterInOptionsUI();
			m_Setting = null;
		}
	}
}
