using Terraria.Localization;

namespace WallpaperEngine.Core
{
	internal static class WeText
	{
		internal static string UI(string key) => Language.GetTextValue("Mods.WallpaperEngine.UI." + key);

		internal static string Layer(string id) =>
			Language.GetTextValue("Mods.WallpaperEngine.UI.Layer_" + id.Replace('.', '_'));
	}
}
