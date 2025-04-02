namespace MauiApp3
{
	public partial class MainPage : ContentPage
	{
		Stream? SurfaceStream = new MemoryStream();
		Stream? HiddenStream = new MemoryStream();
		public MainPage()
		{
			InitializeComponent();
		}
		private static MemoryStream StreamCopy(Stream Source)
		{
			MemoryStream Target = new();
			Source.Position = 0;
			Source.CopyTo(Target);
			Target.Position = 0;
			return Target;
		}
		private async void ContentPage_Loaded(object sender, EventArgs e)
		{
			(await FileSystem.OpenAppPackageFileAsync($"surface.jpg")).CopyTo(SurfaceStream);
			(await FileSystem.OpenAppPackageFileAsync($"hidden.jpg")).CopyTo(HiddenStream);
			Surface.Source = ImageSource.FromStream(() => StreamCopy(SurfaceStream));
			Hidden.Source = ImageSource.FromStream(() => StreamCopy(HiddenStream));
		}
	}
}