using Google.Protobuf.Collections;

namespace Onnx
{
	abstract class 变量(GraphProto 计算图)
	{
		protected GraphProto? 计算图 = 计算图;
		//使用If子图时，需要设置变量名称，与子图的输入名称匹配
		public abstract string 名称 { get; set; }

		//将当前变量转换为指定名称的输出变量。使用此方法取得输出变量后，原来的变量将不再可用
		public virtual 输出变量 Identity(string 输出名称, TensorProto.Types.DataType 数据类型, IEnumerable<long> 各维尺寸)
		{
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

			计算图.Name = 输出名称;
			//必须最后修改，因为前面还用到原始值

			输出变量 返回值 = new(计算图);
			计算图 = null;
			return 返回值;
		}
		//将当前变量转换为同名的输出变量。使用此方法取得输出变量后，原来的变量将不再可用
		public virtual 输出变量 Identity(TensorProto.Types.DataType 数据类型, IEnumerable<long> 各维尺寸)
		{
			return Identity(分配标识符(), 数据类型, 各维尺寸);
		}
		public static ushort 标识符计数 = 0;
		public static string 分配标识符()
		{
			return "_埃博拉酱_" + 标识符计数++;
		}
		public static 中间变量 运算(string 运算名称, IEnumerable<变量> 输入, IEnumerable<AttributeProto> 特性)
		{
			string 变量名 = 分配标识符();

			//IEnumerable重复访问不保证产生相同的值，必须先锁定
			变量[] 锁定输入 = [.. 输入];

			return new(new()
			{
				Name = 变量名,
				Input = { 锁定输入.SelectMany(值 => 值.计算图.Input).Distinct() },
				Initializer = { 锁定输入.SelectMany(值 => 值.计算图.Initializer).Distinct() },
				Node = { 锁定输入.SelectMany(值 => 值.计算图.Node).Distinct(),new NodeProto()
				{
					OpType = 运算名称,
					Input = { 锁定输入.Select(值=>值.计算图.Name) },
					Output = { 变量名 },
					Attribute = { 特性 }
				} }
			});
		}
		public static 中间变量 运算(string 运算名称, IEnumerable<变量> 输入)
		{
			string 变量名 = 分配标识符();

			//IEnumerable重复访问不保证产生相同的值，必须先锁定
			变量[] 锁定输入 = [.. 输入];

			return new(new()
			{
				Name = 变量名,
				Input = { 锁定输入.SelectMany(值 => 值.计算图.Input).Distinct() },
				Initializer = { 锁定输入.SelectMany(值 => 值.计算图.Initializer).Distinct() },
				Node = { 锁定输入.SelectMany(值 => 值.计算图.Node).Distinct(),new NodeProto()
				{
					OpType = 运算名称,
					Input = { 锁定输入.Select(值=>值.计算图.Name) },
					Output = { 变量名 }
				} }
			});
		}
		//将当前变量拷贝到一个新的中间变量
		public 中间变量 Identity()
		{
			return 运算("Identity", [this]);
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
		//返回该张量所有元素的最小值
		public 中间变量 ReduceMin()
		{
			return 运算("ReduceMin", [this]);
		}
		private static readonly AttributeProto[] 空轴不操作 = [new()
		{
			Name = "noop_with_empty_axes",
			Type = AttributeProto.Types.AttributeType.Int,
			I = 1
		}];
		//返回该张量沿指定轴规约的最小值
		public 中间变量 ReduceMin(变量 轴)
		{
			return 运算("ReduceMin", [this, 轴], 空轴不操作);
		}
		//返回每个输入张量的逐元素最小值
		public static 中间变量 Min(params 变量[]输入)
		{
			return 运算("Min", 输入);
		}
		//返回该张量所有元素的最大值
		public 中间变量 ReduceMax()
		{
			return 运算("ReduceMax", [this]);
		}
		//返回该张量沿指定轴规约的最大值
		public 中间变量 ReduceMax(变量 轴)
		{
			return 运算("ReduceMin", [this, 轴], 空轴不操作);
		}
		//返回每个输入张量的逐元素最大值
		public static 中间变量 Max(params 变量[] 输入)
		{
			return 运算("Max", 输入);
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
		//两个条件输出数目必须相同。将根据条件返回其中一组输出，另一组不会被实际计算。
		public IEnumerable<中间变量> If(IEnumerable<输出变量> 如果真, IEnumerable<输出变量> 如果假)
		{
			输出变量[] 锁定如果真 = [.. 如果真];
			输出变量[] 锁定如果假 = [.. 如果假];
			string[] 变量名 = [.. 锁定如果真.Select(_ => 分配标识符())];
			//必须先ToArray确定下来，不能保留IEnumerable，否则每次都会获取新的标识符，不能重用

			NodeProto[] 所有真节点 = [..锁定如果真.SelectMany(值 => 值.计算图.Node)];
			NodeProto[] 所有假节点 = [..锁定如果假.SelectMany(值 => 值.计算图.Node)];
			GraphProto 模板 = new()
			{
				Input = { 锁定如果真.Concat(锁定如果假).Append(this).SelectMany(值 => 值.计算图.Input).Distinct() },
				Initializer = { 锁定如果真.Concat(锁定如果假).Append(this).SelectMany(值 => 值.计算图.Initializer).Distinct() },
				Node = { Enumerable.Intersect(所有真节点,所有假节点).Union(计算图.Node),new NodeProto()
				{
					OpType = "If",
					Input = { 计算图.Name },
					Output = { 变量名 },
					Attribute =
					{
						new AttributeProto()
						{
							Name = "then_branch",
							Type = AttributeProto.Types.AttributeType.Graph,
							G = new ()
							{
								Name = "then_branch",
								Output={ 锁定如果真.SelectMany(值=>值.计算图.Output) },
								Node = { 所有真节点.Except(所有假节点).Except(计算图.Node) }
							}
						},
						new AttributeProto()
						{
							Name = "else_branch",
							Type = AttributeProto.Types.AttributeType.Graph,
							G = new()
							{
								Name = "else_branch",
								Output={ 锁定如果假.SelectMany(值=>值.计算图.Output) },
								Node = { 所有假节点.Except(所有真节点).Except(计算图.Node) }
							}
						}
					}
				} }
			};
			foreach (string 名称 in 变量名)
			{
				GraphProto 实例 = 模板.Clone();
				实例.Name = 名称;
				yield return new(实例);
			}
		}
		//两个条件输出数目必须相同。将根据条件返回其中一组输出，另一组不会被实际计算。
		public IEnumerable<中间变量> If(Func<IEnumerable<输出变量>> 如果真, Func<IEnumerable<输出变量>> 如果假)
		{
			return If(如果真(), 如果假());
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
		public IEnumerable<中间变量> Split(int 轴, int 拆分个数, 变量 拆分长度)
		{
			string[] 变量名 = [.. Enumerable.Range(0, 拆分个数).Select(_ => 分配标识符())];
			//必须先ToArray确定下来，不能保留IEnumerable，否则每次都会获取新的标识符，不能重用

			GraphProto 模板 = new()
			{
				Input = { Enumerable.Union(计算图.Input, 拆分长度.计算图.Input) },
				Initializer = { Enumerable.Union(计算图.Initializer, 拆分长度.计算图.Initializer) },
				Node = { Enumerable.Union(计算图.Node, 拆分长度.计算图.Node),new NodeProto()
				{
					OpType ="Split",
					Input = { 计算图.Name,拆分长度.计算图.Name },
					Output = { 变量名 },
					Attribute = {new AttributeProto()
					{
						Name = "axis",
						Type = AttributeProto.Types.AttributeType.Int,
						I = 轴
					} }
				} }
			};
			foreach (string 名称 in 变量名)
			{
				GraphProto 实例 = 模板.Clone();
				实例.Name = 名称;
				yield return new(实例);
			}
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
		//仅需传入所有输出变量即可生成最小计算模型。输出图的所有输出变量名与输入的所有输出变量名相同，但重复的输出变量名只保留一个，且不保证顺序。
		public static GraphProto 生成计算图(string 名称, IEnumerable<输出变量> 所有输出)
		{
			//IEnumerable重复访问不保证产生相同的值，必须先锁定
			输出变量[] 锁定 = [.. 所有输出];
			return new()
			{
				Name = 名称,
				Input = { 锁定.SelectMany(输出 => 输出.计算图.Input).Distinct() },
				Output = { 锁定.SelectMany(输出 => 输出.计算图.Output).Distinct() },
				Initializer = { 锁定.SelectMany(输出 => 输出.计算图.Initializer).Distinct() },
				Node = { 锁定.SelectMany(输出 => 输出.计算图.Node).Distinct() },
			};
		}
		//仅需传入所有输出变量即可生成最小计算模型。输出图的所有输出变量名与输入的所有输出变量名相同，但重复的输出变量名只保留一个，且不保证顺序。
		public static GraphProto 生成计算图(IEnumerable<输出变量> 所有输出)
		{
			return 生成计算图(分配标识符(), 所有输出);
		}
		//仅需传入所有输出变量即可生成最小计算模型。输出图的所有输出变量名与输入的所有输出变量名相同，但重复的输出变量名只保留一个，且不保证顺序。
		public static GraphProto 生成计算图(string 名称, params 输出变量[] 所有输出)
		{
			return 生成计算图(名称, 所有输出.AsEnumerable());
		}
		//仅需传入所有输出变量即可生成最小计算模型。输出图的所有输出变量名与输入的所有输出变量名相同，但重复的输出变量名只保留一个，且不保证顺序。
		public static GraphProto 生成计算图(params 输出变量[] 所有输出)
		{
			return 生成计算图(分配标识符(), 所有输出.AsEnumerable());
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
		public static readonly Dictionary<object, 常量> 常量表 = [];
		public 常量(IEnumerable<long> 值, IEnumerable<long> 各维尺寸) : base(new Func<GraphProto>(() =>
		{
			string 名称 = 分配标识符();
			return new()
			{
				Name = 名称,
				Initializer = { new TensorProto()
				{
					Name =名称,
					Dims = { 各维尺寸 },
					DataType = (int)TensorProto.Types.DataType.Int64,
					Int64Data = { 值 }
				} }
			};
		})())
		{ }
		public 常量(IEnumerable<long> 值) : this(值, [值.LongCount()])
		{ }
		public static implicit operator 常量(long 值)
		{
			return 常量表.ContainsKey(值) ? 常量表[值] : (常量表[值] = new([值]));
		}
		public 常量(IEnumerable<float> 值, IEnumerable<long> 各维尺寸) : base(new Func<GraphProto>(() =>
		{
			string 名称 = 分配标识符();
			return new()
			{
				Name = 名称,
				Initializer = { new TensorProto()
				{
					Name =名称,
					Dims = { 各维尺寸 },
					DataType = (int)TensorProto.Types.DataType.Float,
					FloatData = { 值 }
				} }
			};
		})())
		{ }
		public 常量(IEnumerable<float> 值) : this(值, [值.LongCount()])
		{ }
		public static implicit operator 常量(float 值)
		{
			return 常量表.ContainsKey(值) ? 常量表[值] : (常量表[值] = new([值]));
		}
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
		//取得和此中间变量同名的输出变量。那之后，此中间变量将不再可用。
		public override 输出变量 Identity(string 输出名称, TensorProto.Types.DataType 数据类型, IEnumerable<long> 各维尺寸)
		{
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
			RepeatedField<string> 最终输出 = (from NodeProto 节点 in 计算图.Node where 节点.Output.Contains(计算图.Name) select 节点.Output).Single();
			最终输出[最终输出.IndexOf(计算图.Name)] = 输出名称;

			//计算图.Name必须最后设置，因为前面还用到原始值
			计算图.Name = 输出名称;

			输出变量 返回值 = new(计算图);
			计算图 = null;
			return 返回值;
		}
		//取得和此中间变量同名的输出变量。那之后，此中间变量将不再可用。
		public override 输出变量 Identity(TensorProto.Types.DataType 数据类型, IEnumerable<long> 各维尺寸)
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
			输出变量 返回值 = new(计算图);
			计算图 = null;
			return 返回值;
		}
		public override string 名称
		{
			get => 计算图.Name;
			set
			{
				RepeatedField<string> 节点输出 = (from NodeProto 节点 in 计算图.Node where 节点.Output.Contains(计算图.Name) select 节点.Output).Single();
				节点输出[节点输出.IndexOf(计算图.Name)] = value;
				计算图.Name = value;
			}
		}
	}
}