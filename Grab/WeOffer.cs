namespace WallpaperEngine.Grab
{
	internal enum WeOfferKind
	{
		Logo,
		Sky
	}

	internal sealed class WeOffer
	{
		public string Id { get; init; } = "";
		public WeOfferKind Kind { get; init; }
		public string ModName { get; init; } = "";
		public string ModTitle { get; init; } = "";
		public string MenuTitle { get; init; } = "";
		public bool UseStyle { get; set; }
		public bool UseMenuScene { get; set; }
		public bool UseThemeFx { get; set; }
		public bool Pending { get; set; }
	}
}
