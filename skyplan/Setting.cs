using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using skyplan.Systems;
using System.Collections.Generic;

namespace skyplan {
	[FileLocation(nameof(skyplan))]
	[SettingsUIGroupOrder(kPanelGroup, kKeybindingGroup, kAboutGroup)]
	[SettingsUIShowGroupName(kPanelGroup, kKeybindingGroup, kAboutGroup)]
	[SettingsUIKeyboardAction(Mod.kToggleActionName, ActionType.Button,
		usages: new[] { Usages.kMenuUsage, Usages.kDefaultUsage },
		interactions: new[] { "Press" })]
	public class Setting : ModSetting {
		public const string kSection = "Main";
		public const string kPanelGroup = "Panel";
		public const string kAboutGroup = "About";
		public const string kKeybindingGroup = "KeyBinding";

		public Setting(IMod mod) : base(mod) { }

		[SettingsUIButton]
		[SettingsUISection(kSection, kPanelGroup)]
		public bool OpenPanel {
			set { DrawingSystem.instance?.TogglePanel(); }
		}

		[SettingsUIButton]
		[SettingsUISection(kSection, kPanelGroup)]
		public bool ExportGeoJson {
			set { ExportSystem.Instance()?.ExportToGeoJson(); }
		}

		[SettingsUIKeyboardBinding(BindingKeyboard.P, Mod.kToggleActionName)]
		[SettingsUISection(kSection, kKeybindingGroup)]
		public ProxyBinding ToggleBinding { get; set; }

		[SettingsUISection(kSection, kKeybindingGroup)]
		public bool ResetBindings {
			set {
				Mod.log.Info("Reset key bindings");
				ResetKeyBindings();
			}
		}
		[SettingsUISection(kSection, kAboutGroup)]
		[SettingsUIMultilineText]
		public string Version => VersionInfo.Version;
		// 	get => GetVersion();
		// 	set { } // required by CS2, intentionally empty
		// }

		public string GetVersion() {
			// System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
			// System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
			// string version = fvi.FileVersion;
			return $"assembly Version = , VersionInfo = {VersionInfo.Version}";
		}

		public override void SetDefaults() { }
	}

	public class LocaleEN : IDictionarySource {
		private readonly Setting m_Setting;
		public LocaleEN(Setting setting) { m_Setting = setting; }

		public IEnumerable<KeyValuePair<string, string>> ReadEntries(
			IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts) {
			return new Dictionary<string, string> {
				{ m_Setting.GetSettingsLocaleID(), "Skyplan" },
				{ m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

				{ m_Setting.GetOptionGroupLocaleID(Setting.kPanelGroup), "Drawing Panel" },
				{ m_Setting.GetOptionGroupLocaleID(Setting.kKeybindingGroup), "Key Bindings" },

				{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenPanel)), "Open / close panel" },
				{ m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenPanel)), "Toggle the SVG drawing overlay on the map." },

				{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.ToggleBinding)), "Toggle key" },
				{ m_Setting.GetOptionDescLocaleID(nameof(Setting.ToggleBinding)), "Keyboard shortcut to open / close the drawing panel." },

				{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.ExportGeoJson)), "Export To GeoJson" },
				{ m_Setting.GetOptionDescLocaleID(nameof(Setting.ExportGeoJson)), "Export the plan to Geojson" },

				{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetBindings)), "Reset bindings" },
				{ m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetBindings)), "Restore default key bindings for this mod." },

				{ m_Setting.GetOptionGroupLocaleID(Setting.kAboutGroup), "About" },
				{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.Version)), $"Version : {m_Setting.GetVersion()}" },

				{ m_Setting.GetBindingMapLocaleID(), "Skyplan" },
				{ m_Setting.GetBindingKeyLocaleID(Mod.kToggleActionName), "Toggle drawing panel" },


			};
		}

		public void Unload() { }
	}
}
