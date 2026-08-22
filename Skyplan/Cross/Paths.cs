using Colossal.PSI.Environment;
using System.IO;

namespace Skyplan.Cross {
	public static class Paths {
		public readonly static string ModDataPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", "skyplan");
		public readonly static string DisplaySettingsPath = Path.Combine(EnvPath.kUserDataPath, "ModsSettings", "skyplan", "display.json");
		public readonly static string DefaultLayers = Path.Combine(Mod.modPath, "layers_default.json");
		public readonly static string UserLayers = Path.Combine(ModDataPath, "layers.json");
	}
}
