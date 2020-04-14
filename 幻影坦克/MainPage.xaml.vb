' https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板
Imports Windows.Storage, Windows.Graphics.Imaging, Windows.UI, MATLAB
''' <summary>
''' 可用于自身或导航至 Frame 内部的空白页。
''' </summary>
Public NotInheritable Class MainPage
	Inherits Page
	Private 资产文件夹 As StorageFolder, 表图解码器 As BitmapDecoder, 里图解码器 As BitmapDecoder, 生成位图 As WriteableBitmap
	Shared ReadOnly 文件保存器 As New Pickers.FileSavePicker, 文件打开器 As New Pickers.FileOpenPicker
	Private Async Function 获取位图解码器(路径 As String) As Task(Of BitmapDecoder)
		Return Await BitmapDecoder.CreateAsync(Await (Await 资产文件夹.GetFileAsync(路径)).OpenReadAsync)
	End Function
	Private Async Function 获取软件位图(解码器 As BitmapDecoder) As Task(Of SoftwareBitmap)
		Dim b As SoftwareBitmap = Await 解码器.GetSoftwareBitmapAsync
		If b.BitmapPixelFormat <> BitmapPixelFormat.Bgra8 OrElse b.BitmapAlphaMode <> BitmapAlphaMode.Premultiplied Then b = SoftwareBitmap.Convert(b, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
		Return b
	End Function
	Private Async Sub 异步初始化()
		资产文件夹 = Await Package.Current.InstalledLocation.GetFolderAsync("Assets")
		表图解码器 = Await 获取位图解码器("表图.png")
		里图解码器 = Await 获取位图解码器("里图.png")
		Call 原表图.SetBitmapAsync(Await 获取软件位图(表图解码器))
		Call 原里图.SetBitmapAsync(Await 获取软件位图(里图解码器))
	End Sub

	Private Async Sub 表图_Tapped(sender As Object, e As TappedRoutedEventArgs) Handles 表图.Tapped
		Dim a As StorageFile = Await 文件打开器.PickSingleFileAsync
		If a IsNot Nothing Then
			表图解码器 = Await BitmapDecoder.CreateAsync(Await a.OpenReadAsync)
			Call 原表图.SetBitmapAsync(Await 获取软件位图(表图解码器))
		End If
	End Sub

	Private Async Sub 里图_Tapped(sender As Object, e As TappedRoutedEventArgs) Handles 里图.Tapped
		Dim a As StorageFile = Await 文件打开器.PickSingleFileAsync
		If a IsNot Nothing Then
			里图解码器 = Await BitmapDecoder.CreateAsync(Await a.OpenReadAsync)
			Call 原里图.SetBitmapAsync(Await 获取软件位图(里图解码器))
		End If
	End Sub
	Sub New()

		' 此调用是设计器所必需的。
		InitializeComponent()

		' 在 InitializeComponent() 调用之后添加任何初始化。
		异步初始化()
		文件保存器.FileTypeChoices.Add("PNG图像", {".png"})
		With 文件打开器.FileTypeFilter
			.Add(".png")
			.Add(".jpg")
			.Add(".bmp")
			.Add(".gif")
			.Add(".tif")
			.Add(".tiff")
			.Add(".jpeg")
			.Add(".ico")
		End With
	End Sub

	Private Async Sub 保存_Click(sender As Object, e As RoutedEventArgs) Handles 保存.Click
		Dim a As IAsyncOperation(Of StorageFile) = 文件保存器.PickSaveFileAsync()
		Dim c As SoftwareBitmap = SoftwareBitmap.CreateCopyFromBuffer(生成位图.PixelBuffer, BitmapPixelFormat.Bgra8, 生成位图.PixelWidth, 生成位图.PixelHeight, BitmapAlphaMode.Straight)
		Dim b As StorageFile = Await a
		If b IsNot Nothing Then
			Dim d As BitmapEncoder = Await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Await b.OpenAsync(FileAccessMode.ReadWrite))
			d.SetSoftwareBitmap(c)
			Call d.FlushAsync()
		End If
	End Sub

	Private Sub 明场Grid_Tapped(sender As Object, e As TappedRoutedEventArgs) Handles 明场Grid.Tapped
		明场背景.ShowAt(sender)
	End Sub

	Private Sub 暗场Grid_Tapped(sender As Object, e As TappedRoutedEventArgs) Handles 暗场Grid.Tapped
		暗场背景.ShowAt(sender)
	End Sub

	Private Function 范围放缩(数组 As SingleArray) As SingleArray
		Dim ArrayMin As Single = Min(数组), ArrayMax As Single = Max(数组)
		If Single.IsNegativeInfinity(ArrayMin) Then
			If Single.IsPositiveInfinity(ArrayMax) Then
				Return ArrayFun(Function(a As Single) 255 * (Math.Atan(Math.PI * (a / 255 - 1 / 2)) / Math.PI + 1 / 2), 数组)
			Else
				Return ArrayFun(Function(a As Single) 65025 / (255 + ArrayMax - a), 数组)
			End If
		Else
			If Single.IsPositiveInfinity(ArrayMax) Then
				Return ArrayFun(Function(a As Single) 65025 / (ArrayMin - 255 - a) + 255, 数组)
			Else
				If ArrayMax <= 255 AndAlso ArrayMin >= 0 Then
					Return 数组
				Else
					ArrayMax = Math.Max(ArrayMax, 255)
					ArrayMin = Math.Min(ArrayMin, 0)
					Return ArrayFun(Function(a As Single) 255 / (ArrayMax - ArrayMin) * (a - ArrayMin), 数组)
				End If
			End If
		End If
	End Function

	Shared ReadOnly 透明索引 As IntegerColon() = {Colon(3, 3), Colon(0, Nothing), Colon(0, Nothing)}, 颜色索引 As IntegerColon() = {Colon(0, 2), Colon(0, Nothing), Colon(0, Nothing)}, 位图变换 As New BitmapTransform

	Private Async Function 单图处理(解码器 As BitmapDecoder, 背景 As SingleArray) As Task(Of SingleArray)
		Dim c As New ByteArray((Await 解码器.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, 位图变换, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage)).DetachPixelData)
		Dim a As SingleArray = ToSingle(c)
		a.Reshape(4, 解码器.PixelWidth, 解码器.PixelHeight)
		Dim b As SingleArray = a(透明索引)
		Return (b * a(颜色索引) + (255 - b) * 背景) / 255
	End Function
	Private Async Sub Generate_Click(sender As Object, e As RoutedEventArgs) Handles Generate.Click
		进度环.IsActive = True
		Dim g As Color = 前景色.Color, h As Color = 背景色.Color, 最终字节 As ByteArray = Await Task.Run(Async Function()
																								   Dim Cb1 As SingleArray = New ByteArray({g.B, g.G, g.R}).ToSingle, Cb2 As SingleArray = New ByteArray({h.B, h.G, h.R}).ToSingle, Image1Task As Task(Of SingleArray) = 单图处理(表图解码器, Cb1), Image2Task As Task(Of SingleArray) = 单图处理(里图解码器, Cb2), Cs1 As SingleArray = Await Image1Task, Cs2 As SingleArray = Await Image2Task, dCb As SingleArray = Cb1 - Cb2, dCs As SingleArray = Sum(dCb * (Cs1 - Cs2), 0) / Sum(dCb ^ 2)
																								   dCs(IsNan(dCs)) = 0
																								   dCs *= dCb
																								   Dim Cs As SingleArray = Cs1 + Cs2
																								   Cs1 = 范围放缩((Cs + dCs) / 2)
																								   Cs2 = 范围放缩((Cs - dCs) / 2)
																								   Dim Alpha As SingleArray = Mean(范围放缩(ArrayFun(Function(a As Single) If(Single.IsNaN(a), 255, 255 * (1 - a)), (Cs1 - Cs2) / dCb)), 0), Color As SingleArray = 范围放缩(255 * (Cs1 - Cb1 + Cs2 - Cb2) / (2 * Alpha) + (Cb1 + Cb2) / 2)
																								   Return Cat(0, ToByte(Color), ToByte(Alpha))
																							   End Function)
		生成位图 = New WriteableBitmap(最终字节.Size(1), 最终字节.Size(2))
		生成位图.PixelBuffer.AsStream.Write(最终字节.原型, 0, 最终字节.NumEl)
		明场预览.Source = 生成位图
		暗场预览.Source = 生成位图
		进度环.IsActive = False
	End Sub
End Class
