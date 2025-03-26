using Google.Protobuf.Collections;

namespace Onnx
{
	abstract class 变量
	{
		protected GraphProto 计算图;
		//使用If子图时，需要设置变量名称，与子图的输入名称匹配
		public abstract string 名称 { get; set; }
		protected 变量(GraphProto 计算图)
		{
			this.计算图 = 计算图;
		}
		//使用此方法取得输出变量后，原来的变量将不再可用
		public virtual 输出变量 Identity(string 输出名称, TensorProto.Types.DataType 数据类型, IEnumerable<long> 各维尺寸)
		{
			计算图.Name = 输出名称;
			计算图.Output.Add(new ValueInfoProto()
			{
				Name = 输出名称,
				Type = new()
				{
					TensorType = new()
					{
						ElemType = (int)数据类型,
						Shape = new() { Dim = { from 维度值 in 各维尺寸 select new TensorShapeProto.Types.Dimension { DimValue = 维度值 } } }
					}
				}
			});
			计算图.Node.Add(new NodeProto()
			{
				OpType = "Identity",
				Input = { 计算图.Name },
				Output = { 输出名称 }
			});
			return new(计算图);
		}
		public static IEnumerable<中间变量> 运算(string 运算名称, IEnumerable<变量> 输入, IEnumerable<AttributeProto> 特性, int 输出个数)
		{
			IEnumerable<string> 变量名 = Enumerable.Range(0, 输出个数).Select(_ => Guid.NewGuid().ToString());
			return 变量名.Select(名称 => new 中间变量(new()
			{
				Name = 名称,
				Input = { 输入.SelectMany(值 => 值.计算图.Input).Distinct() },
				Initializer = { 输入.SelectMany(值 => 值.计算图.Initializer).Distinct() },
				Node = { 输入.SelectMany(值 => 值.计算图.Node),new NodeProto()
				{
					OpType = 运算名称,
					Input = { 输入.Select(值=>值.计算图.Name) },
					Output = { 变量名 },
					Attribute = { 特性 }
				} }
			}));
		}
		public static 中间变量 运算(string 运算名称, IEnumerable<变量> 输入, IEnumerable<AttributeProto> 特性)
		{
			string 变量名 = Guid.NewGuid().ToString();
			return new(new()
			{
				Name = 变量名,
				Input = { 输入.SelectMany(值 => 值.计算图.Input).Distinct() },
				Initializer = { 输入.SelectMany(值 => 值.计算图.Initializer).Distinct() },
				Node = { 输入.SelectMany(值 => 值.计算图.Node),new NodeProto()
				{
					OpType = 运算名称,
					Input = { 输入.Select(值=>值.计算图.Name) },
					Output = { 变量名 },
					Attribute = { 特性 }
				} }
			});
		}
		public static 中间变量 运算(string 运算名称, IEnumerable<变量> 输入)
		{
			string 变量名 = Guid.NewGuid().ToString();
			return new(new()
			{
				Name = 变量名,
				Input = { 输入.SelectMany(值 => 值.计算图.Input).Distinct() },
				Initializer = { 输入.SelectMany(值 => 值.计算图.Initializer).Distinct() },
				Node = { 输入.SelectMany(值 => 值.计算图.Node),new NodeProto()
				{
					OpType = 运算名称,
					Input = { 输入.Select(值=>值.计算图.Name) },
					Output = { 变量名 }
				} }
			});
		}
		public 中间变量 Cast(TensorProto.Types.DataType 数据类型)
		{
			return 运算("Cast", [this], [new()
			{
				Name = "to",
				Type = AttributeProto.Types.AttributeType.Int,
				I = (int)数据类型
			}]);
		}
		public static implicit operator 变量(long 值) => (常量)值;
		public static implicit operator 变量(float 值) => (常量)值;
		//接受2~4个参数，分别为开始、结束、轴、步长
		public 中间变量 Slice(变量 包含0开始, 变量 不含结束, params 变量[] 轴_步长)
		{
			return 运算("Slice", Enumerable.Concat([this, 包含0开始, 不含结束], 轴_步长));
		}
		public static 中间变量 operator +(变量 加数A, 变量 加数B)
		{
			return 运算("Add", [加数A, 加数B]);
		}
		public static 中间变量 operator-(变量 被减数,变量 减数)
		{
			return 运算("Sub", [被减数, 减数]);
		}
		public static 中间变量 operator*(变量 乘数A,变量 乘数B)
		{
			return 运算("Mul", [乘数A, 乘数B]);
		}
		public static 中间变量 operator /(变量 被除数, 变量 除数)
		{
			return 运算("Div", [被除数, 除数]);
		}
		public 中间变量 ReduceSum(变量 轴)
		{
			return 运算("ReduceSum", [this, 轴]);
		}
		public 中间变量 Sum()
		{
			return 运算("Sum", [this]);
		}
		public 中间变量 IsNaN()
		{
			return 运算("IsNaN", [this]);
		}
		public static 中间变量 Where(变量 条件,变量 真值, 变量 假值)
		{
			return 运算("Where", [条件, 真值, 假值]);
		}
		public 中间变量 Min()
		{
			return 运算("Min", [this]);
		}
		public 中间变量 Max()
		{
			return 运算("Max", [this]);
		}
		public 中间变量 IsInf(bool 检测正无穷, bool 检测负无穷)
		{
			return 运算("IsInf", [this],
			[
				new()
				{
					Name = "detect_positive",
					Type = AttributeProto.Types.AttributeType.Int,
					I = 检测正无穷 ? 1 : 0
				},
				new()
				{
					Name = "detect_negative",
					Type = AttributeProto.Types.AttributeType.Int,
					I = 检测负无穷 ? 1 : 0
				}
			]);
		}
		public IEnumerable<中间变量> If(GraphProto 如果真, GraphProto 如果假)
		{
			return 运算("If", [this],
			[
				new()
				{
					Name = "then_branch",
					Type = AttributeProto.Types.AttributeType.Graph,
					G = 如果真
				},
				new()
				{
					Name = "else_branch",
					Type = AttributeProto.Types.AttributeType.Graph,
					G = 如果假
				}
			], 如果真.Output.Count);
		}
		public static 中间变量 Concat(int 轴, params 变量[] 输入)
		{
			return 运算("Concat", 输入, [new()
			{
				Name = "axis",
				Type = AttributeProto.Types.AttributeType.Int,
				I = 轴
			}]);
		}
		public 中间变量 Atan()
		{
			return 运算("Atan", [this]);
		}
		public static 中间变量 operator &(变量 左, 变量 右)
		{
			return 运算("And", [左, 右]);
		}
		public static 中间变量 operator |(变量 左, 变量 右)
		{
			return 运算("Or", [左, 右]);
		}
		public static 中间变量 operator!(变量 操作数)
		{
			return 运算("Not", [操作数]);
		}
		public static 中间变量 operator==(变量 左, 变量 右)
		{
			return 运算("Equal", [左, 右]);
		}
		public static 中间变量 operator !=(变量 左, 变量 右)
		{
			return !(左 == 右);
		}
		public static 中间变量 operator <(变量 左, 变量 右)
		{
			return 运算("Less", [左, 右]);
		}
		public static 中间变量 operator >(变量 左, 变量 右)
		{
			return 运算("Greater", [左, 右]);
		}
		public static 中间变量 operator <=(变量 左, 变量 右)
		{
			return 运算("LessOrEqual", [左, 右]);
		}
		public static 中间变量 operator >=(变量 左, 变量 右)
		{
			return 运算("GreaterOrEqual", [左, 右]);
		}
		public 中间变量 ReduceMean(变量 轴)
		{
			return 运算("ReduceMean", [this, 轴]);
		}

	}
	class 输出变量:变量
	{
		//使用Identity方法获取此类对象，而不是直接构造
		internal 输出变量(GraphProto 计算图) : base(计算图) { }
		public override string 名称 
		{ 
			get => 计算图.Name;
			set 
			{
				计算图.Output.Single().Name = value;
				计算图.Name = value;
			}
		}
		//仅需传入所有输出变量即可生成最小计算模型
		public static GraphProto 生成计算图(params 输出变量[] 所有输出)
		{
			GraphProto 返回值 = new()
			{
				Input = { 所有输出.SelectMany(输出 => 输出.计算图.Input).Distinct() },
				Output = { 所有输出.SelectMany(输出 => 输出.计算图.Output).Distinct() },
				Initializer = { 所有输出.SelectMany(输出 => 输出.计算图.Initializer).Distinct() },
			};
			foreach (输出变量 输出 in 所有输出)
				foreach (NodeProto 节点 in 输出.计算图.Node)
					if (!返回值.Node.Contains(节点))
						返回值.Node.Add(节点);
			return 返回值;
		}
	}
	class 输入变量(string 输入名称, TensorProto.Types.DataType 数据类型, IEnumerable<long> 各维尺寸) : 变量(new()
		{
			Name = 输入名称,
			Input ={new ValueInfoProto()
				{
					Name = 输入名称,
					Type = new(){TensorType = new()
					{
						ElemType = (int)数据类型,
						Shape = new(){Dim = { from 维度值 in 各维尺寸 select new TensorShapeProto.Types.Dimension { DimValue = 维度值 } }}
					}}
				}}
		})
	{
		public override string 名称
		{
			get => 计算图.Name;
			set
			{
				计算图.Input.Single().Name = value;
				计算图.Name = value;
			}
		}
	}
	class 常量 : 变量
	{
		public 常量(TensorProto 原型) : base(new()
		{
			Name = 原型.Name,
			Initializer = { 原型 }
		})
		{ }
		static readonly Dictionary<object, 常量> 常量表 = [];
		public static implicit operator 常量(long 值)
		{
			if (常量表.ContainsKey(值))
				return 常量表[值];
			return 常量表[值] = new(new TensorProto()
			{
				Name = Guid.NewGuid().ToString(),
				Dims = { 1 },
				DataType = (int)TensorProto.Types.DataType.Int64,
				Int64Data = { 值 }
			});
		}
		public static implicit operator 常量(float 值)
		{
			if (常量表.ContainsKey(值))
				return 常量表[值];
			return 常量表[值] = new(new TensorProto()
			{
				Name = Guid.NewGuid().ToString(),
				Dims = { 1 },
				DataType = (int)TensorProto.Types.DataType.Float,
				FloatData = { 值 }
			});
		}
		public 常量(IEnumerable<float> 值, IEnumerable<long> 各维尺寸) : base(new()
		{
			Name = Guid.NewGuid().ToString(),
			Initializer = { new TensorProto()
			{
				Name = Guid.NewGuid().ToString(),
				Dims = { 各维尺寸 },
				DataType = (int)TensorProto.Types.DataType.Float,
				FloatData = { 值 }
			} }
		})
		{ }
		public override string 名称
		{
			get => 计算图.Name;
			set
			{
				计算图.Initializer.Single().Name = value;
				计算图.Name = value;
			}
		}
	}
	class 中间变量:变量
	{
		//使用各种运算方法获取此类对象，而不是直接构造
		internal 中间变量(GraphProto 计算图) : base(计算图) { }
		public override 输出变量 Identity(string 输出名称, TensorProto.Types.DataType 数据类型, IEnumerable<long> 各维尺寸)
		{
			计算图.Name = 输出名称;
			计算图.Output.Add(new ValueInfoProto()
			{
				Name = 输出名称,
				Type = new()
				{
					TensorType = new()
					{
						ElemType = (int)数据类型,
						Shape = new() { Dim = { from 维度值 in 各维尺寸 select new TensorShapeProto.Types.Dimension { DimValue = 维度值 } } }
					}
				}
			});
			RepeatedField<string> 最终输出 = 计算图.Node.Last().Output;
			最终输出.Remove(计算图.Name);
			最终输出.Add(输出名称);
			return new(计算图);
		}
		//取得和此中间变量同名的输出变量。那之后，此中间变量将不再可用。
		public 输出变量 Identity(TensorProto.Types.DataType 数据类型, IEnumerable<long> 各维尺寸)
		{
			计算图.Output.Add(new ValueInfoProto()
			{
				Name = 名称,
				Type = new()
				{
					TensorType = new()
					{
						ElemType = (int)数据类型,
						Shape = new() { Dim = { from 维度值 in 各维尺寸 select new TensorShapeProto.Types.Dimension { DimValue = 维度值 } } }
					}
				}
			});
			return new(计算图);
		}
		public override string 名称
		{
			get => 计算图.Name;
			set
			{
				RepeatedField<string> 节点输出 = 计算图.Node.Last().Output;
				节点输出[节点输出.IndexOf(计算图.Name)] = value;
				计算图.Name = value;
			}
		}
	}
}