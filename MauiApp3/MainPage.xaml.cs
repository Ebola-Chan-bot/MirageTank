namespace MauiApp3
{
	public partial class MainPage : ContentPage
	{
		Stream? SurfaceStream;
		Stream? HiddenStream;
		public MainPage()
		{
			InitializeComponent();
		}
		private async void ContentPage_Loaded(object sender, EventArgs e)
		{
			SurfaceStream = await FileSystem.OpenAppPackageFileAsync($"surface.jpg");
			HiddenStream = await FileSystem.OpenAppPackageFileAsync($"hidden.jpg");
			Surface.Source = ImageSource.FromStream(() => SurfaceStream);
			Hidden.Source = ImageSource.FromStream(() => HiddenStream);
		}
	}
}