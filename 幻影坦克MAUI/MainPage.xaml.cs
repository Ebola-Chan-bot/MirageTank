﻿using Microsoft.Maui.Storage;
using SkiaSharp;

namespace 幻影坦克MAUI
{
    public partial class MainPage : ContentPage
    {
		Stream? 表图流;
		Stream? 里图流;
		public MainPage()
        {
            InitializeComponent();
        }

		private void Generate_Clicked(object sender, EventArgs e)
		{
			SKBitmap 表图对象 = SKBitmap.Decode(表图流);
			SKBitmap 里图对象 = SKBitmap.Decode(里图流);
			ushort 输出高度= (ushort)Math.Max(表图对象.Height,里图对象.Height);
			ushort 输出宽度 = (ushort)Math.Max(表图对象.Width, 里图对象.Width);
		}

		private async void ContentPage_Loaded(object sender, EventArgs e)
		{
			表图流 = await FileSystem.OpenAppPackageFileAsync($"surface_raw.jpg");
			里图流 = await FileSystem.OpenAppPackageFileAsync($"hidden_raw.jpg");
			表图.Source = ImageSource.FromStream(() => 表图流);
			里图.Source = ImageSource.FromStream(() => 里图流);
		}
		readonly PickOptions options = new()
		{
			PickerTitle = "选择图像",
			FileTypes = FilePickerFileType.Images
		};
		private async void 表图_Tapped(object sender, TappedEventArgs e)
		{
			FileResult? result = await FilePicker.Default.PickAsync(options);

			if (result != null)
			{
				表图流 = await result.OpenReadAsync();
				表图.Source = ImageSource.FromStream(() => 表图流);
			}
		}
		private async void 里图_Tapped(object sender, TappedEventArgs e)
		{
			FileResult? result = await FilePicker.Default.PickAsync(options);
			if (result != null)
			{
				里图流 = await result.OpenReadAsync();
				里图.Source = ImageSource.FromStream(() => 里图流);
			}
		}
	}
}
