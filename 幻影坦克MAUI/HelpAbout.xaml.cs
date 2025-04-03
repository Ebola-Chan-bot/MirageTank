#if ANDROID
using Android.Content;
#elif WINDOWS
using System.Diagnostics;
#endif
namespace 幻影坦克MAUI;
public partial class HelpAbout : ContentPage
{
	public HelpAbout()
	{
		InitializeComponent();
	}
	string? MyUrl;
	private void Web页_Navigated(object sender, WebNavigatedEventArgs e)
	{
		MyUrl = e.Url;
		Web页.Navigating += Web页_Navigating;
		Web页.Navigated -= Web页_Navigated;
	}

	private void Web页_Navigating(object sender, WebNavigatingEventArgs e)
	{
		if (MyUrl != e.Url && e.Url != "README.html")
		{
			e.Cancel = true;
#if ANDROID
			Intent I = new(Intent.ActionView, Android.Net.Uri.Parse(e.Url));
			I.AddFlags(ActivityFlags.NewTask);
			MauiApplication.Current.StartActivity(I);
#elif WINDOWS
			Process.Start(new ProcessStartInfo(e.Url) { UseShellExecute = true });
#endif
		}
	}
}