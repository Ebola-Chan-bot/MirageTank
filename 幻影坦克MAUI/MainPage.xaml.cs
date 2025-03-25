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
		private static ValueInfoProto 值信息原型(string 名称, TensorProto.Types.DataType 数据类型, int[] 维度)
		{
			return new()
			{
				Name = 名称,
				Type = new()
				{
					TensorType = new()
					{
						ElemType = (int)数据类型,
						Shape = new()
						{
							Dim = { from 维度值 in 维度 select new TensorShapeProto.Types.Dimension { DimValue = 维度值 } }
						}
					}
				}
			};
		}
		private static TensorProto 标量字节原型(string 名称, byte 值)
		{
			return new()
			{
				Name = 名称,
				Dims = { 1 },
				DataType = (int)TensorProto.Types.DataType.Uint8,
				Int32Data = { 值 }
			};
		}
		private static TensorProto 标量浮点原型(string 名称,float 值)
		{
			return new()
			{
				Name = 名称,
				Dims = { 1 },
				DataType = (int)TensorProto.Types.DataType.Float,
				FloatData = { 值 }
			};
		}
		private static TensorProto 一维浮点向量(string 名称, float[] 值)
		{
			return new()
			{
				Name = 名称,
				Dims = { 值.Length },
				DataType = (int)TensorProto.Types.DataType.Float,
				FloatData = { 值 }
			};
		}
		private static NodeProto 转Float(string 输入, string 输出)
		{
			return new()
			{
				OpType = "Cast",
				Input = { 输入 },
				Output = { 输出 },
				Attribute =
				{
					new AttributeProto()
					{
						Name = "to",
						Type = AttributeProto.Types.AttributeType.Int,
						I = (long)TensorProto.Types.DataType.Float
					}
				}
			};
		}
		private static NodeProto 一维切片(string 输入, string 输出, string 起始, string 结束)
		{
			return new()
			{
				OpType = "Slice",
				Input = { 输入, 起始, 结束, "0" },
				Output = { 输出 }
			};
		}
		private static NodeProto 四维切片(string 输入, string 输出, string 起始, string 结束)
		{
			return new()
			{
				OpType = "Slice",
				Input = { 输入, 起始, 结束, "3" },
				Output = { 输出 }
			};
		}
		private static NodeProto 加法(string 加数A, string 加数B, string 结果)
		{
			return new()
			{
				OpType = "Add",
				Input = { 加数A, 加数B },
				Output = { 结果 }
			};
		}
		private static NodeProto 减法(string 被减数, string 减数, string 结果)
		{
			return new()
			{
				OpType = "Sub",
				Input = { 被减数, 减数 },
				Output = { 结果 }
			};
		}
		private static NodeProto 乘法(string 乘数A, string 乘数B, string 结果)
		{
			return new()
			{
				OpType = "Mul",
				Input = { 乘数A, 乘数B },
				Output = { 结果 }
			};
		}
		private static NodeProto 除法(string 被除数, string 除数, string 结果)
		{
			return new()
			{
				OpType = "Div",
				Input = { 被除数, 除数 },
				Output = { 结果 }
			};
		}
		private static NodeProto 求和(string 张量, string 维度, string 结果)
		{
			return new()
			{
				OpType = "ReduceSum",
				Input = { 张量, 维度 },
				Output = { 结果 }
			};
		}
		private static NodeProto 全求和(string 张量,string 结果)
		{
			return new()
			{
				OpType = "Sum",
				Input = { 张量 },
				Output = { 结果 }
			};
		}
		private static NodeProto 检查NaN(string 输入, string 输出)
		{
			return new NodeProto
			{
				OpType = "IsNaN",
				Input = { 输入 },
				Output = { 输出 }
			};
		}
		private static NodeProto NaN归零(string 条件, string 输入, string 输出)
		{
			return new NodeProto
			{
				OpType = "Where",
				Input = { 条件, "0", 输入 },
				Output = { 输出 }
			};
		}
		private void Generate_Clicked(object sender, EventArgs e)
		{
			SKBitmap 表图对象 = SKBitmap.Decode(表图流);
			SKBitmap 里图对象 = SKBitmap.Decode(里图流);
			ushort 输出高度 = (ushort)Math.Max(表图对象.Height, 里图对象.Height);
			ushort 输出宽度 = (ushort)Math.Max(表图对象.Width, 里图对象.Width);
			if (!File.Exists(模型路径))
			{
				new ModelProto
				{
					IrVersion = (long)Onnx.Version.IrVersion,
					OpsetImport = { new OperatorSetIdProto { Version = 22 } },
					Graph = new()
					{
						Name = "幻影坦克",
						Input =
						{
							值信息原型("表里图u8", TensorProto.Types.DataType.Uint8, [4, -1, -1, 2]),
							值信息原型("背景色u8", TensorProto.Types.DataType.Uint8, [3, 2])
						},
						Output =
						{
							值信息原型("输出图", TensorProto.Types.DataType.Uint8, [4, -1, -1])
						},
						Initializer =
						{
							标量字节原型("0", 0),
							标量字节原型("1", 1),
							标量字节原型("3", 3),
							标量字节原型("4", 4),
							标量浮点原型("255",255),
							一维浮点向量("三色权重", [ 0.114f, 0.587f, 0.2989f ]),
							一维浮点向量("三色权重2", [0.114f*0.114f,0.587f*0.587f,0.2989f*0.2989f]),
						},
						Node =
						{
							转Float("表里图u8", "表里图f32"),
							转Float("背景色u8", "背景色f32"),
							一维切片("表里图f32","不透明通道","3","4"),
							一维切片("表里图f32","RGB通道","0","3"),
							减法("255","不透明通道","透明通道"),
							乘法("透明通道","背景色f32","背景色透明度"),
							乘法("不透明通道","RGB通道","RGB不透明度"),
							加法("RGB不透明度","背景色透明度","混色叠加16"),
							除法("混色叠加16","255","混色叠加8"),
							四维切片("背景色f32","表图背景色","0","1"),
							四维切片("背景色f32","里图背景色","1","2"),
							减法("表图背景色","里图背景色","背景色差"),
							四维切片("混色叠加8","表图混色叠加","0","1"),
							四维切片("混色叠加8","里图混色叠加","1","2"),
							减法("表图混色叠加","里图混色叠加","混色叠加差"),
							乘法("背景色差","三色权重","加权背景色差"),
							乘法("混色叠加差","三色权重","加权混色叠加差"),
							乘法("加权背景色差","加权混色叠加差","加权背景混色叠加差"),
							求和("加权背景混色叠加差","0","灰度差"),
							乘法("加权背景色差","加权背景色差","加权背景色差平方"),
							全求和("加权背景色差平方","全亮度"),
							除法("灰度差","全亮度","不安全调和灰度差"),
							检查NaN("不安全调和灰度差","是否NaN"),
							NaN归零("是否NaN","不安全调和灰度差","安全调和灰度差"),
							乘法("安全调和灰度差","背景色差","调和灰度差"),
							加法("表图混色叠加","里图混色叠加","混色叠加和"),
						}
					}
				};
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
