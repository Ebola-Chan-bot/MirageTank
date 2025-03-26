using Maui.ColorPicker;
using Microsoft.Maui.Storage;
using Microsoft.ML.OnnxRuntime;
using Onnx;
using SkiaSharp;
using SkiaSharp.Views.Maui;

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
		private static OrtValue 位图转张量(SKBitmap 位图对象, ushort 输出高度, ushort 输出宽度, ColorPicker 颜色)
		{
			SKBitmap 输出位图 = new(输出宽度, 输出高度, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
			using (SKCanvas 画布 = new(输出位图))
			{
				画布.Clear(颜色.PickedColor.ToSKColor());
				输出高度 = (ushort)Math.Min(输出高度, 位图对象.Height * 输出宽度 / 位图对象.Width);
				输出宽度 = (ushort)Math.Min(输出宽度, 位图对象.Width * 输出高度 / 位图对象.Height);
				ushort 左偏移 = (ushort)((输出位图.Width - 输出宽度) / 2);
				ushort 上偏移 = (ushort)((输出位图.Height - 输出高度) / 2);
				画布.DrawBitmap(位图对象, new SKRect(左偏移, 上偏移, 输出宽度 + 左偏移, 输出高度 + 上偏移));
			}
			return OrtValue.CreateTensorValueFromMemory(输出位图.Bytes, [4, 输出宽度, 输出高度]);
		}
		private static readonly string 模型路径 = Path.Combine(FileSystem.CacheDirectory, "模型v1.onnx");
		private static 中间变量 范围放缩(变量 张量)
		{
			中间变量 全局最小 = 张量.Min();
			中间变量 全局最大 = 张量.Max();
		}
		private void Generate_Clicked(object sender, EventArgs e)
		{
			SKBitmap 表图对象 = SKBitmap.Decode(表图流);
			SKBitmap 里图对象 = SKBitmap.Decode(里图流);
			ushort 输出高度 = (ushort)Math.Max(表图对象.Height, 里图对象.Height);
			ushort 输出宽度 = (ushort)Math.Max(表图对象.Width, 里图对象.Width);
			if (!File.Exists(模型路径))
			{
				中间变量 表里图 = new 输入变量("表里图", TensorProto.Types.DataType.Uint8, [4, -1, -1, 2]).Cast(TensorProto.Types.DataType.Float);
				中间变量 背景色 = new 输入变量("背景色", TensorProto.Types.DataType.Uint8, [3, 1, 1, 2]).Cast(TensorProto.Types.DataType.Float);
				中间变量 透明通道 = 表里图.Slice(3, 4, 0);
				中间变量 表里亮度 = ((255f - 透明通道) * 背景色 + 透明通道 * 表里图.Slice(0, 3, 0)) / 255f;
				中间变量 背景色差 = 背景色.Slice(0, 1, 3) - 背景色.Slice(1, 2, 3);
				常量 三色权重 = new([0.299f, 0.587f, 0.114f], [3]);
				中间变量 加权背景差 = 背景色差 * 三色权重;
				中间变量 表图 = 表里亮度.Slice(0, 1, 3);
				中间变量 里图 = 表里亮度.Slice(1, 2, 3);
				中间变量 表里色差 = (加权背景差 * 三色权重 * (表图-里图)).ReduceSum(0) / (加权背景差 * 加权背景差).Sum();
				表里色差 = 变量.Where(表里色差.IsNaN(), 0, 表里色差) * 背景色差;
				中间变量 表里色和 = 表图 + 里图;
			}
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
