using Google.Protobuf;
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
		private static byte[] 位图转字节(SKBitmap 位图对象, ushort 输出高度, ushort 输出宽度, ColorPicker 颜色)
		{
			SKBitmap 输出位图 = new(输出宽度, 输出高度, SKColorType.Rgba8888, SKAlphaType.Unpremul);
			using (SKCanvas 画布 = new(输出位图))
			{
				画布.Clear(颜色.PickedColor.ToSKColor());
				输出高度 = (ushort)Math.Min(输出高度, 位图对象.Height * 输出宽度 / 位图对象.Width);
				输出宽度 = (ushort)Math.Min(输出宽度, 位图对象.Width * 输出高度 / 位图对象.Height);
				ushort 左偏移 = (ushort)((输出位图.Width - 输出宽度) / 2);
				ushort 上偏移 = (ushort)((输出位图.Height - 输出高度) / 2);
				画布.DrawBitmap(位图对象, new SKRect(左偏移, 上偏移, 输出宽度 + 左偏移, 输出高度 + 上偏移));
			}
			return 输出位图.Bytes;
		}
		private static readonly string 模型路径 = Path.Combine(FileSystem.CacheDirectory, "模型v1.onnx");
		private static 中间变量 范围放缩(变量 张量)
		{
			中间变量 全局最小 = 张量.Min();
			中间变量 全局最大 = 张量.Max();
			输入变量 最小输入 = new(全局最小.名称, TensorProto.Types.DataType.Float, [1]);
			输入变量 最大输入 = new(全局最大.名称, TensorProto.Types.DataType.Float, [1]);
			输入变量 数组输入 = new(张量.名称, TensorProto.Types.DataType.Float, [-1, -1, -1, -1]);
			中间变量 中间最大 = 变量.Where(最大输入 > 255f, 最大输入, 255f);
			中间变量 中间最小 = 变量.Where(最小输入 < 0f, 最小输入, 0f);
			return 全局最小.IsInf(false, true).If
			(
				输出变量.生成计算图(最大输入.IsInf(true, false).If
				(
					输出变量.生成计算图(((((数组输入 / 255f - 0.5f) * MathF.PI).Atan() / MathF.PI + 0.5f) * 255f).Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1])),
					输出变量.生成计算图((65025f / (255f + 最大输入 - 数组输入)).Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1]))
				).Single().Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1])),
				输出变量.生成计算图(最大输入.IsInf(true, false).If
				(
					输出变量.生成计算图((65025f / (最小输入 - 255f - 数组输入) + 255f).Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1])),
					输出变量.生成计算图((最大输入 <= 255f & 最小输入 >= 0).If
					(
						输出变量.生成计算图(数组输入.Identity(Guid.NewGuid().ToString(), TensorProto.Types.DataType.Float, [-1, -1, -1, -1])),
						输出变量.生成计算图((255f / (中间最大 - 中间最小) * (数组输入 - 中间最小)).Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1]))
					).Single().Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1]))
				).Single().Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1]))
			).Single();
		}
		private SKData 预览图;
		private void Generate_Clicked(object sender, EventArgs e)
		{
			SKBitmap 表图对象 = SKBitmap.Decode(表图流);
			SKBitmap 里图对象 = SKBitmap.Decode(里图流);
			ushort 输出高度 = (ushort)Math.Max(表图对象.Height, 里图对象.Height);
			ushort 输出宽度 = (ushort)Math.Max(表图对象.Width, 里图对象.Width);
			SessionOptions 会话选项 = new();
#if ANDROID
			会话选项.AppendExecutionProvider_Nnapi();
#endif
#if WINDOWS
			try
			{
				会话选项.AppendExecutionProvider_CUDA();
			}
			catch (OnnxRuntimeException)
			{
				会话选项.AppendExecutionProvider_CPU();
			}
#endif
			InferenceSession 推理会话;
			if (File.Exists(模型路径))
				推理会话 = new(模型路径, 会话选项);
			else
			{
				中间变量 表里图 = new 输入变量("表里图", TensorProto.Types.DataType.Uint8, [4, -1, -1, 2]).Cast(TensorProto.Types.DataType.Float);
				中间变量 背景色 = new 输入变量("背景色", TensorProto.Types.DataType.Uint8, [3, 1, 1, 2]).Cast(TensorProto.Types.DataType.Float);
				中间变量 透明通道 = 表里图.Slice(3, 4, 0);
				中间变量 表里亮度 = ((255f - 透明通道) * 背景色 + 透明通道 * 表里图.Slice(0, 3, 0)) / 255f;
				中间变量 表背景色 = 背景色.Slice(0, 1, 3);
				中间变量 里背景色 = 背景色.Slice(1, 2, 3);
				中间变量 背景色差 = 表背景色 - 里背景色;
				常量 三色权重 = new([0.299f, 0.587f, 0.114f], [3]);
				中间变量 加权背景差 = 背景色差 * 三色权重;
				中间变量 表图 = 表里亮度.Slice(0, 1, 3);
				中间变量 里图 = 表里亮度.Slice(1, 2, 3);
				中间变量 表里色差 = (加权背景差 * 三色权重 * (表图 - 里图)).ReduceSum(0) / (加权背景差 * 加权背景差).Sum();
				表里色差 = 变量.Where(表里色差.IsNaN(), 0, 表里色差) * 背景色差;
				中间变量 表里色和 = 表图 + 里图;
				表里色和 = 范围放缩(变量.Concat(3, (表里色和 + 表里色差) / 2f, (表里色和 - 表里色差) / 2f));
				表图 = 表里色和.Slice(0, 1, 3);
				里图 = 表里色和.Slice(1, 2, 3);
				透明通道 = 范围放缩(255f * (1f - (表图 - 里图) / 背景色差));
				透明通道 = 变量.Where(透明通道.IsNaN(), 255f, 透明通道).ReduceMean(0);
				中间变量 背景色和 = 表背景色 + 里背景色;
				中间变量 颜色通道 = 范围放缩((255f * (表图 + 里图 - 背景色和) / 透明通道 + 背景色和) / 2);
				ModelProto 模型原型 = new()
				{
					IrVersion = (long)Onnx.Version.IrVersion,
					OpsetImport = { new OperatorSetIdProto() { Version = 22 } },
					Graph = 输出变量.生成计算图(变量.Concat(0, 颜色通道, 透明通道).Cast(TensorProto.Types.DataType.Uint8).Identity("幻影坦克", TensorProto.Types.DataType.Uint8, [4, 输出宽度, 输出高度]))
				};
				模型原型.WriteToFile(模型路径);
				推理会话 = new(模型原型.ToByteArray(), 会话选项);
			}
			SKBitmap 幻影坦克 = new();
			幻影坦克.InstallPixels(new SKImageInfo(输出宽度, 输出高度, SKColorType.Rgba8888, SKAlphaType.Unpremul), SKData.CreateCopy(推理会话.Run(new RunOptions(), new Dictionary<string, OrtValue>
			{
				{ "表里图",OrtValue.CreateTensorValueFromMemory(位图转字节(表图对象, 输出高度, 输出宽度, HiddenColor).Concat(位图转字节(里图对象,输出高度,输出宽度,SurfaceColor)).ToArray(),[4,输出宽度,输出高度,2]) },
				{ "背景色",OrtValue.CreateTensorValueFromMemory<byte>(
					[
						(byte)(SurfaceColor.PickedColor.Red*255),(byte)(SurfaceColor.PickedColor.Green*255),(byte)(SurfaceColor.PickedColor.Blue*255),
						(byte)(HiddenColor.PickedColor.Red*255),(byte)(HiddenColor.PickedColor.Green*255),(byte)(HiddenColor.PickedColor.Blue*255)
					],[3,1,1,2]) }
			}, ["幻影坦克"]).Single().GetTensorDataAsSpan<byte>()).Data);
			预览图 = SKImage.FromBitmap(幻影坦克).Encode(SKEncodedImageFormat.Png, 100);
			明场预览.Source = ImageSource.FromStream(() => 预览图.AsStream());
			暗场预览.Source = ImageSource.FromStream(() => 预览图.AsStream());
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
