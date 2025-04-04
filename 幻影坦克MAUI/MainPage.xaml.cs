﻿using CommunityToolkit.Maui.Storage;
using Google.Protobuf;
using Maui.ColorPicker;
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
		static readonly string 模型路径 = Path.Combine(FileSystem.CacheDirectory, "模型v2.onnx");
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
			SessionOptions 会话选项 = new();
#if ANDROID
				会话选项.AppendExecutionProvider_Nnapi();
#endif
			try
			{
				new InferenceSession(模型.ToByteArray(), 会话选项);
			}
			catch(Exception 异常)
			{
				FileSaver.SaveAsync("验证模型.onnx", new MemoryStream(模型.ToByteArray()));
				throw;
			}
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
		private SKData? 预览图;
		private static MemoryStream 流拷贝(Stream 源流)
		{
			MemoryStream 目标流 = new();
			源流.Position = 0;
			源流.CopyTo(目标流);
			目标流.Position = 0;
			return 目标流;
		}
		readonly InferenceSession 推理会话 = new Func<InferenceSession>(() =>
		{
			SessionOptions 会话选项 = new();
			if (File.Exists(模型路径))
				try
				{
#if ANDROID
					try
					{
						会话选项.AppendExecutionProvider("QNN");
						return new(模型路径, 会话选项);
					}
					catch (Exception)//可能是EntryPointNotFoundException或OnnxRuntimeException
					{ }
					会话选项 = new();
					try
					{
						会话选项.AppendExecutionProvider("SNPE");
						return new(模型路径, 会话选项);
					}
					catch (Exception)
					{ }
					会话选项 = new();
					try
					{
						会话选项.AppendExecutionProvider_Nnapi();
						return new(模型路径, 会话选项);
					}
					catch (Exception)
					{ }
					会话选项 = new();
					try
					{
						会话选项.AppendExecutionProvider("XNNPACK");
						return new(模型路径, 会话选项);
					}
					catch (Exception)
					{ }
#endif
#if WINDOWS
					try
					{
						会话选项.AppendExecutionProvider_Tensorrt();
						return new(模型路径, 会话选项);
					}
					catch (Exception)
					{ }
					会话选项 = new();
					try
					{
						会话选项.AppendExecutionProvider_CUDA();
						return new(模型路径, 会话选项);
					}
					catch (Exception)
					{ }
					会话选项 = new();
					try
					{
						会话选项.AppendExecutionProvider_Dnnl();
						return new(模型路径, 会话选项);
					}
					catch (Exception)
					{ }
					会话选项 = new();
					try
					{
						会话选项.AppendExecutionProvider_OpenVINO();
						return new(模型路径, 会话选项);
					}
					catch (Exception)
					{ }
					会话选项 = new();
					try
					{
						会话选项.AppendExecutionProvider_MIGraphX();
						return new(模型路径, 会话选项);
					}
					catch (Exception)
					{ }
					会话选项 = new();
					try
					{
						会话选项.AppendExecutionProvider_ROCm();
						return new(模型路径, 会话选项);
					}
					catch (Exception)
					{ }
					会话选项 = new();
					try
					{
						会话选项.AppendExecutionProvider_DML();
						return new(模型路径, 会话选项);
					}
					catch (Exception)
					{ }
					会话选项 = new();
#endif
					会话选项.AppendExecutionProvider_CPU();
					return new(模型路径, 会话选项);
				}
				catch (OnnxRuntimeException)
				{ }
			变量 表里图 = new 输入变量("表里图", TensorProto.Types.DataType.Uint8, [2, -1, -1, 4]).Cast(TensorProto.Types.DataType.Float);
			变量 背景色 = new 输入变量("背景色", TensorProto.Types.DataType.Uint8, [2, 1, 1, 3]).Cast(TensorProto.Types.DataType.Float);
			中间变量[] 表里背景色 = [.. 背景色.Split(0, 2, new 常量([1, 1]))];
			中间变量[] 颜色透明通道 = [.. 表里图.Split(3, 2, new 常量([3, 1]))];
			表里图 = ((255f - 颜色透明通道[1]) * 背景色 + 颜色透明通道[1] * 颜色透明通道[0]) / 255f;
			背景色 = 表里背景色[0] - 表里背景色[1];
			常量 三色权重 = new([0.299f, 0.587f, 0.114f], [1, 1, 1, 3]);
			中间变量 加权背景差 = 背景色 * 三色权重;
			中间变量[] 表里图拆分 = [.. 表里图.Split(0, 2, new 常量([1, 1]))];
			表里图 = (加权背景差 * 三色权重 * (表里图拆分[0] - 表里图拆分[1])).ReduceSum(3) / (加权背景差 * 加权背景差).ReduceSum();
			表里图 = 变量.Where(表里图.IsNaN(), 0f, 表里图) * 背景色;
			中间变量 表里色和 = 表里图拆分[0] + 表里图拆分[1];
			表里色和 = 范围放缩(变量.Concat(0, (表里色和 + 表里图) / 2f, (表里色和 - 表里图) / 2f));
			表里图拆分 = [.. 表里色和.Split(0, 2, new 常量([1, 1]))];
			颜色透明通道[1] = 范围放缩(255f * (1f - (表里图拆分[0] - 表里图拆分[1]) / 背景色));
			颜色透明通道[1] = 变量.Where(颜色透明通道[1].IsNaN(), 255f, 颜色透明通道[1]).ReduceMean(3);
			背景色 = 表里背景色[0] + 表里背景色[1];
			表里图 = 范围放缩((255f * (表里图拆分[0] + 表里图拆分[1] - 背景色) / 颜色透明通道[1] + 背景色) / 2f);
			ModelProto 模型原型 = new()
			{
				IrVersion = 10,
				OpsetImport = { new OperatorSetIdProto() { Version = 22 } },
				Graph = 输出变量.生成计算图([变量.Concat(3, 表里图, 颜色透明通道[1]).Cast(TensorProto.Types.DataType.Uint8).Identity("幻影坦克", TensorProto.Types.DataType.Uint8, [-1, -1, 4])])
			};
			模型原型.WriteToFile(模型路径);
#if ANDROID
			会话选项 = new();
			try
			{
				会话选项.AppendExecutionProvider("QNN");
				return new(模型原型.ToByteArray(), 会话选项);
			}
			catch (Exception)//可能是EntryPointNotFoundException或OnnxRuntimeException
			{ }
			会话选项 = new();
			try
			{
				会话选项.AppendExecutionProvider("SNPE");
				return new(模型原型.ToByteArray(), 会话选项);
			}
			catch (Exception)
			{ }
			会话选项 = new();
			try
			{
				会话选项.AppendExecutionProvider_Nnapi();
				return new(模型原型.ToByteArray(), 会话选项);
			}
			catch (Exception)
			{ }
			会话选项 = new();
			try
			{
				会话选项.AppendExecutionProvider("XNNPACK");
				return new(模型原型.ToByteArray(), 会话选项);
			}
			catch (Exception)
			{ }
#endif
#if WINDOWS
			会话选项 = new();
			try
			{
				会话选项.AppendExecutionProvider_Tensorrt();
				return new(模型原型.ToByteArray(), 会话选项);
			}
			catch (Exception)
			{ }
			会话选项 = new();
			try
			{
				会话选项.AppendExecutionProvider_CUDA();
				return new(模型原型.ToByteArray(), 会话选项);
			}
			catch (Exception)
			{ }
			会话选项 = new();
			try
			{
				会话选项.AppendExecutionProvider_Dnnl();
				return new(模型原型.ToByteArray(), 会话选项);
			}
			catch (Exception)
			{ }
			会话选项 = new();
			try
			{
				会话选项.AppendExecutionProvider_OpenVINO();
				return new(模型原型.ToByteArray(), 会话选项);
			}
			catch (Exception)
			{ }
			会话选项 = new();
			try
			{
				会话选项.AppendExecutionProvider_MIGraphX();
				return new(模型原型.ToByteArray(), 会话选项);
			}
			catch (Exception)
			{ }
			会话选项 = new();
			try
			{
				会话选项.AppendExecutionProvider_ROCm();
				return new(模型原型.ToByteArray(), 会话选项);
			}
			catch (Exception)
			{ }
			会话选项 = new();
			try
			{
				会话选项.AppendExecutionProvider_DML();
				return new(模型原型.ToByteArray(), 会话选项);
			}
			catch (Exception)
			{ }
#endif
			会话选项 = new();
			会话选项.AppendExecutionProvider_CPU();
			return new(模型原型.ToByteArray(), 会话选项);
		})();
		readonly SKBitmap 幻影坦克 = new();
		readonly RunOptions 运行选项 = new();
		readonly Dictionary<string, OrtValue> 运行输入 = new();
		private async void Generate_Clicked(object sender, EventArgs e)
		{
			生成.IsVisible = false;
			保存.IsVisible = false;
			进度环.IsRunning = true;
			异常文本.Text = "";
			try
			{
				string 预览路径 = await Task.Run(() =>
				{
					SKBitmap 表图对象 = SKBitmap.Decode(流拷贝(表图流));
					SKBitmap 里图对象 = SKBitmap.Decode(流拷贝(里图流));
					int 输出高度 = Math.Max(表图对象.Height, 里图对象.Height);
					int 输出宽度 = Math.Max(表图对象.Width, 里图对象.Width);
					运行输入["表里图"] = OrtValue.CreateTensorValueFromMemory(位图转字节(表图对象, 输出高度, 输出宽度, 表图选色器).Concat(位图转字节(里图对象, 输出高度, 输出宽度, 里图选色器)).ToArray(), [2, 输出高度, 输出宽度, 4]);
					运行输入["背景色"] = OrtValue.CreateTensorValueFromMemory(
					[
						(byte)(表图选色器.PickedColor.Red * 255), (byte)(表图选色器.PickedColor.Green * 255), (byte)(表图选色器.PickedColor.Blue * 255),
					(byte)(里图选色器.PickedColor.Red * 255), (byte)(里图选色器.PickedColor.Green * 255), (byte)(里图选色器.PickedColor.Blue * 255)
					], [2, 1, 1, 3]);
					幻影坦克.InstallPixels(new SKImageInfo(输出宽度, 输出高度, SKColorType.Rgba8888, SKAlphaType.Unpremul), SKData.CreateCopy(推理会话.Run(运行选项, 运行输入, ["幻影坦克"])[0].GetTensorDataAsSpan<byte>()).Data);
					预览图 = 幻影坦克.Encode(SKEncodedImageFormat.Png, 100);
					string 预览路径 = Path.Combine(FileSystem.CacheDirectory, Guid.NewGuid() + ".png");//不能使用分配标识符或者任何可能已存在的文件名。Android的缓存机制存在问题，覆盖创建的文件不会被读取方感知到。
					预览图.SaveTo(new FileStream(预览路径, FileMode.Create));//Android的ImageSource.FromStream无法处理大数据，需要先输出到文件
					return 预览路径;
				});
				明场预览.Source = ImageSource.FromFile(预览路径);
				暗场预览.Source = ImageSource.FromFile(预览路径);
			}
			catch (Exception 异常)
			{
				异常文本.Text = $"{异常.GetType()}：{异常.Message}";
			}
			进度环.IsRunning = false;
			生成.IsVisible = true;
			保存.IsVisible = true;
		}
		private static Stream 图流可复用化(Stream 图流)
		{
#if ANDROID
			//Android文件流只能读一次，因此需要先拷贝到内存流才能复用
			MemoryStream 内存流 = new();
			图流.CopyTo(内存流);
			内存流.Position = 0;
			return 内存流;
#else
			return 图流;
#endif
		}
		private async void ContentPage_Loaded(object sender, EventArgs e)
		{
			表图流 = 图流可复用化(await FileSystem.OpenAppPackageFileAsync("surface_raw.jpg"));
			里图流 = 图流可复用化(await FileSystem.OpenAppPackageFileAsync("hidden_raw.jpg"));

			//ImageSource会析构流，因此需要拷贝
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
				表图流 = 图流可复用化(await result.OpenReadAsync());
				表图.Source = ImageSource.FromFile(result.FullPath);//FromStream在Android上不支持较大的文件
			}
		}
		private async void 里图_Tapped(object sender, TappedEventArgs e)
		{
			FileResult? result = await FilePicker.Default.PickAsync(options);
			if (result != null)
			{
				里图流 = 图流可复用化(await result.OpenReadAsync());
				里图.Source = ImageSource.FromFile(result.FullPath);//FromStream在Android上不支持较大的文件
			}
		}
		private void 复制生成图(object sender, EventArgs e)
		{
#if WINDOWS
			if(预览图 == null)
			{
				异常文本.Text = "请先生成幻影坦克";
				return;
			}
			复制SK图(预览图);
#endif
		}
		private ColorPicker? 焦点;
		private void 明场预览_Tapped(object sender, TappedEventArgs e)
		{
			清理焦点();
			表图选色器.IsVisible = true;
			焦点 = 表图选色器;
		}
		private void 暗场预览_Tapped(object sender, TappedEventArgs e)
		{
			清理焦点();
			里图选色器.IsVisible = true;
			焦点 = 里图选色器;
		}
		private void 清理焦点()
		{
			if(焦点 != null)
			{
				焦点.IsVisible = false;
				焦点 = null;
			}
		}
		private void 清理焦点(object sender, TappedEventArgs e)
		{
			清理焦点();
		}

		private void 保存_Clicked(object sender, EventArgs e)
		{
			if (预览图 == null)
			{
				异常文本.Text = "请先生成幻影坦克";
				return;
			}
			FileSaver.SaveAsync("幻影坦克.png", 预览图.AsStream());
		}

		private void 复制表图_Clicked(object sender, EventArgs e)
		{
#if WINDOWS
			表图流.Position = 0;
			复制SK图(SKBitmap.Decode(SKData.Create(表图流)).Encode(SKEncodedImageFormat.Png, 100));
#endif
		}

		private async void 粘贴表图_Clicked(object sender, EventArgs e)
		{
#if WINDOWS
			Stream? 流 = await 粘贴(表图);
			if (流 != null)
				表图流 = 流;
#endif
		}

		private void 复制里图_Clicked(object sender, EventArgs e)
		{
#if WINDOWS
			里图流.Position = 0;
			复制SK图(SKBitmap.Decode(SKData.Create(里图流)).Encode(SKEncodedImageFormat.Png, 100));
#endif
		}

		private async void 粘贴里图_Clicked(object sender, EventArgs e)
		{
#if WINDOWS
			Stream? 流 = await 粘贴(里图);
			if (流 != null)
				里图流 = 流;
#endif
		}
	}
}
