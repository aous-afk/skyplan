using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using skyplan.Systems;
using Skyplan.Models;
using System.Collections.Generic;
using System.Globalization;

namespace skyplan {
	[FileLocation(nameof(skyplan))]
	[SettingsUIGroupOrder(kPanelGroup, kKeybindingGroup, kAboutGroup, kExportGroup)]
	[SettingsUIShowGroupName(kPanelGroup, kKeybindingGroup, kAboutGroup, kExportGroup)]
	[SettingsUIKeyboardAction(Mod.kToggleActionName, ActionType.Button,
		usages: new[] { Usages.kMenuUsage, Usages.kDefaultUsage },
		interactions: new[] { "Press" })]
	public class Setting : ModSetting {
		public const string kSection = "Main";
		public const string kExportSection = "Export";
		public const string kPanelGroup = "Panel";
		public const string kExportGroup = "CRS";
		public const string kAboutGroup = "About";
		public const string kKeybindingGroup = "KeyBinding";

		public Setting(IMod mod) : base(mod) { }

		[SettingsUIButton]
		[SettingsUISection(kSection, kPanelGroup)]
		public bool OpenPanel {
			set { DrawingSystem.instance?.TogglePanel(); }
		}

		[SettingsUISection(kExportSection, kExportGroup)]
		public SridOption Srid { get; set; } = SridOption.Epsg4326;

		[SettingsUITextInput]
		[SettingsUISection(kExportSection, kExportGroup)]
		public string OriginX { get; set; } = "0";

		[SettingsUITextInput]
		[SettingsUISection(kExportSection, kExportGroup)]
		public string OriginY { get; set; } = "0";

		[SettingsUIButton]
		[SettingsUISection(kExportSection, kExportGroup)]
		public bool ExportGeoJson {
			set {
				if (double.TryParse(OriginX, NumberStyles.Float, CultureInfo.InvariantCulture, out double ox) &&
					double.TryParse(OriginY, NumberStyles.Float, CultureInfo.InvariantCulture, out double oy)) {
					ExportSystem.Instance()?.ExportToGeoJson((int)Srid, ox, oy);
				} else {
					Mod.log.Warn("Export: invalid OriginX/OriginY values");
				}
			}
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

		public override void SetDefaults() {
			Srid = SridOption.Epsg4326;
			OriginX = "0";
			OriginY = "0";
		}
	}

	public class LocaleEN : IDictionarySource {
		private readonly Setting m_Setting;
		public LocaleEN(Setting setting) { m_Setting = setting; }

		public IEnumerable<KeyValuePair<string, string>> ReadEntries(
			IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts) {
			return new Dictionary<string, string> {
				{ m_Setting.GetSettingsLocaleID(), "Skyplan" },
				{ m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },
				{ m_Setting.GetOptionTabLocaleID(Setting.kExportSection), "Export" },

				{ m_Setting.GetOptionGroupLocaleID(Setting.kPanelGroup), "Drawing Panel" },
				{ m_Setting.GetOptionGroupLocaleID(Setting.kExportGroup), "Coordinate Reference System" },
				{ m_Setting.GetOptionGroupLocaleID(Setting.kKeybindingGroup), "Key Bindings" },

				{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenPanel)), "Open / close panel" },
				{ m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenPanel)), "Toggle the SVG drawing overlay on the map." },

				{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.ToggleBinding)), "Toggle key" },
				{ m_Setting.GetOptionDescLocaleID(nameof(Setting.ToggleBinding)), "Keyboard shortcut to open / close the drawing panel." },

				{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.Srid)), "Coordinate System (SRID)" },
				{ m_Setting.GetOptionDescLocaleID(nameof(Setting.Srid)), "Target coordinate reference system for the exported GeoJSON." },
				{ m_Setting.GetEnumValueLocaleID(SridOption.Epsg4326),  "EPSG:4326 — WGS84" },
				{ m_Setting.GetEnumValueLocaleID(SridOption.Epsg25832), "EPSG:25832 — ETRS89 / UTM zone 32N" },
				{ m_Setting.GetEnumValueLocaleID(SridOption.Epsg25833), "EPSG:25833 — ETRS89 / UTM zone 33N" },
				{ m_Setting.GetEnumValueLocaleID(SridOption.Epsg32632), "EPSG:32632 — WGS84 / UTM zone 32N" },
				{ m_Setting.GetEnumValueLocaleID(SridOption.Epsg32633), "EPSG:32633 — WGS84 / UTM zone 33N" },
				{ m_Setting.GetEnumValueLocaleID(SridOption.Epsg27700), "EPSG:27700 — British National Grid" },
				{ m_Setting.GetEnumValueLocaleID(SridOption.Epsg2154),  "EPSG:2154 — RGF93 / Lambert-93 (France)" },
				{ m_Setting.GetEnumValueLocaleID(SridOption.Epsg3857),  "EPSG:3857 — Web Mercator" },

				{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.OriginX)), "Origin X" },
				{ m_Setting.GetOptionDescLocaleID(nameof(Setting.OriginX)), "X coordinate of world origin (longitude for WGS84, easting for UTM)." },

				{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.OriginY)), "Origin Y" },
				{ m_Setting.GetOptionDescLocaleID(nameof(Setting.OriginY)), "Y coordinate of world origin (latitude for WGS84, northing for UTM)." },

				{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.ExportGeoJson)), "Export To GeoJson" },
				{ m_Setting.GetOptionDescLocaleID(nameof(Setting.ExportGeoJson)), "Export the plan to GeoJSON using the configured CRS." },

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
