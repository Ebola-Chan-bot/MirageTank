using Google.Protobuf;
using Maui.ColorPicker;
using Microsoft.Maui.Storage;
using Microsoft.ML.OnnxRuntime;
using Onnx;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using System.Threading.Tasks;

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
		private static byte[] 位图转字节(SKBitmap 位图对象, int 输出高度, int 输出宽度, ColorPicker 颜色)
		{
			SKBitmap 输出位图 = new(输出宽度, 输出高度, SKColorType.Rgba8888, SKAlphaType.Unpremul);
			using (SKCanvas 画布 = new(输出位图))
			{
				画布.Clear(颜色.PickedColor.ToSKColor());
				输出高度 = Math.Min(输出高度, 位图对象.Height * 输出宽度 / 位图对象.Width);
				输出宽度 = Math.Min(输出宽度, 位图对象.Width * 输出高度 / 位图对象.Height);
				float 左偏移 = ((输出位图.Width - 输出宽度) / 2);
				float 上偏移 = ((输出位图.Height - 输出高度) / 2);
				画布.DrawBitmap(位图对象, new SKRect(左偏移, 上偏移, 输出宽度 + 左偏移, 输出高度 + 上偏移));
			}
			return 输出位图.Bytes;
		}
		private static readonly string 模型路径 = Path.Combine(FileSystem.CacheDirectory, "模型v1.onnx");
#if DEBUG
		private static 中间变量 验证模型(params 中间变量[] 要验证的输出)
		{
			GraphProto 计算图 = 输出变量.生成计算图(from 变量 V in 要验证的输出 select V.Identity().Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1]));
			ModelProto 模型 = new()
			{
				IrVersion = 10,
				OpsetImport = { new OperatorSetIdProto() { Version = 22 } },
				Graph = 计算图
			};
			模型.WriteToFile(模型路径);
			new InferenceSession(模型.ToByteArray());
			return 要验证的输出[0];
		}
#endif
		private static 中间变量 范围放缩(变量 张量)
		{
			中间变量 全局最小 = 张量.ReduceMin();
			中间变量 全局最大 = 张量.ReduceMax();
			中间变量 中间最小 = 变量.Where(全局最小 < 0f, 全局最小, 0f);
			return 全局最小.IsInf(false, true).If
			(
				[全局最大.IsInf(true,false).If
				(
					[((((张量 / 255f - 0.5f) * MathF.PI).Atan() / MathF.PI + 0.5f) * 255f).Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1])],
					[(65025f / (255f + 全局最大 - 张量)).Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1])]
				).Single().Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1])],
				[全局最大.IsInf(true, false).If
				(
					[(65025f / (全局最小 - 255f - 张量) + 255f).Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1])],
					[(全局最大 <= 255f & 全局最小 >= 0f).If
					(
						[张量.Identity().Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1])],
						[(255f / (变量.Where(全局最大 > 255f, 全局最大, 255f) - 中间最小) * (张量 - 中间最小)).Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1])]
					).Single().Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1])]
				).Single().Identity(TensorProto.Types.DataType.Float, [-1, -1, -1, -1])]
			).Single();
		}
		private SKData 预览图;
		private static byte[] 更新模型会话(long 输出宽度,long 输出高度)
		{
			中间变量 表里图 = new 输入变量("表里图", TensorProto.Types.DataType.Uint8, [4, -1, -1, 2]).Cast(TensorProto.Types.DataType.Float);
			中间变量 背景色 = new 输入变量("背景色", TensorProto.Types.DataType.Uint8, [3, 1, 1, 2]).Cast(TensorProto.Types.DataType.Float);
			中间变量[] 表里背景色 = [.. 背景色.Split(3, 2, new 常量([1, 1]))];
			中间变量[] 颜色透明通道 = [.. 表里图.Split(0, 2, new 常量([3, 1]))];
			表里图 = ((255f - 颜色透明通道[1]) * 背景色 + 颜色透明通道[1] * 颜色透明通道[0]) / 255f;
			背景色 = 表里背景色[0] - 表里背景色[1];
			常量 三色权重 = new([0.299f, 0.587f, 0.114f], [3,1,1,1]);
			中间变量 加权背景差 = 背景色 * 三色权重;
			中间变量[] 表里图拆分 = [.. 表里图.Split(3, 2, new 常量([1, 1]))];
			表里图 = (加权背景差 * 三色权重 * (表里图拆分[0] - 表里图拆分[1])).ReduceSum(0) / (加权背景差 * 加权背景差).Sum();
			表里图 = 变量.Where(表里图.IsNaN(), 0f, 表里图) * 背景色;
			中间变量 表里色和 = 表里图拆分[0] + 表里图拆分[1];
			表里色和 = 范围放缩(变量.Concat(3, (表里色和 + 表里图) / 2f, (表里色和 - 表里图) / 2f));
			表里图拆分 = [.. 表里色和.Split(3, 2, new 常量([1, 1]))];
			颜色透明通道[1] = 范围放缩(255f * (1f - (表里图拆分[0] - 表里图拆分[1]) / 背景色));
			颜色透明通道[1] = 变量.Where(颜色透明通道[1].IsNaN(), 255f, 颜色透明通道[1]).ReduceMean(0);
			背景色 = 表里背景色[0] + 表里背景色[1];
			表里图 = 范围放缩((255f * (表里图拆分[0] + 表里图拆分[1] - 背景色) / 颜色透明通道[1] + 背景色) / 2f);
			ModelProto 模型原型 = new()
			{
				IrVersion = 10,
				OpsetImport = { new OperatorSetIdProto() { Version = 22 } },
				Graph = 输出变量.生成计算图(变量.Concat(0, 表里图, 颜色透明通道[1]).Cast(TensorProto.Types.DataType.Uint8).Identity("幻影坦克", TensorProto.Types.DataType.Uint8, [4, 输出宽度, 输出高度]))
			};
			模型原型.WriteToFile(模型路径);
			return 模型原型.ToByteArray();
		}
		private static MemoryStream 流拷贝(Stream 源流)
		{
			MemoryStream 目标流 = new();
			源流.CopyTo(目标流);
			源流.Position = 0;
			目标流.Position = 0;
			return 目标流;
		}
		private void Generate_Clicked(object sender, EventArgs e)
		{
			SKBitmap 表图对象 = SKBitmap.Decode(流拷贝(表图流));
			SKBitmap 里图对象 = SKBitmap.Decode(流拷贝(里图流));
			int 输出高度 = Math.Max(表图对象.Height, 里图对象.Height);
			int 输出宽度 = Math.Max(表图对象.Width, 里图对象.Width);
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
			try
			{
				if (File.Exists(模型路径))
					try
					{
						推理会话 = new(模型路径, 会话选项);
					}
					catch (OnnxRuntimeException)
					{
						推理会话 = new(更新模型会话(输出宽度, 输出高度), 会话选项);
					}
				else
					推理会话 = new(更新模型会话(输出宽度, 输出高度), 会话选项);
			}
			catch (OnnxRuntimeException 异常)
			{
				变量.标识符计数 = 0;
				常量.常量表.Clear();
				return;
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
			表图.Source = ImageSource.FromStream(() => 流拷贝(表图流));
			里图.Source = ImageSource.FromStream(() => 流拷贝(里图流));
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
				表图.Source = ImageSource.FromStream(() => 流拷贝(表图流));
			}
		}
		private async void 里图_Tapped(object sender, TappedEventArgs e)
		{
			FileResult? result = await FilePicker.Default.PickAsync(options);
			if (result != null)
			{
				里图流 = await result.OpenReadAsync();
				里图.Source = ImageSource.FromStream(() => 流拷贝(里图流));
			}
		}
	}
}
