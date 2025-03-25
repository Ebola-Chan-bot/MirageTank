namespace 幻影坦克MAUI
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

		private async void OnImageTapped(object sender, TappedEventArgs e)
		{
			var result = await FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = "请选择一张图片",
				FileTypes = FilePickerFileType.Images
			});

			if (result != null)
			{
				var stream = await result.OpenReadAsync();
				((Image)sender).Source = ImageSource.FromStream(() => stream);
			}
		}
	}
}
