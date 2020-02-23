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

	Private Function 范围放缩(数组 As Array(Of MSingle), 最小值 As MSingle, 最大值 As MSingle) As Array(Of MSingle)
		Dim ArrayMin As MSingle = Min(Of MSingle)(Min(数组), 最小值), ArrayMax As MSingle = Max(Of MSingle)(Max(数组), 最大值)
		If ArrayMin < 最小值 OrElse ArrayMax > 最大值 Then
			Return (数组 - ArrayMin) * (最大值 - 最小值) / (ArrayMax - ArrayMin) + 最小值
		Else
			Return 数组
		End If
	End Function

	Shared ReadOnly 透明索引 As ColonExpression() = {Colon(3, 3), Colon(0, [End]), Colon(0, [End])}, 颜色索引 As ColonExpression() = {Colon(0, 2), Colon(0, [End]), Colon(0, [End])}, 位图变换 As New BitmapTransform
	Private Async Function 单图处理(解码器 As BitmapDecoder, 背景 As Array(Of MSingle)) As Task(Of Array(Of MSingle))
		Dim c As Byte() = (Await 解码器.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, 位图变换, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage)).DetachPixelData
		Dim a As Array(Of MSingle) = [Single](c)
		a.Reshape(4, 解码器.PixelWidth, 解码器.PixelHeight)
		Dim b As Array(Of MSingle) = a(透明索引)
		a = a(颜色索引)
		Return b * (a - 背景)
	End Function
	Private Async Sub Generate_Click(sender As Object, e As RoutedEventArgs) Handles Generate.Click
		进度环.IsActive = True
		Dim g As Color = 前景色.Color, h As Color = 背景色.Color, 最终字节 As Array(Of Byte) = Await Task.Run(Async Function()
																										Dim Background1 As Array(Of MSingle) = {g.B, g.G, g.R}, Background2 As Array(Of MSingle) = {h.B, h.G, h.R}, Image1Task As Task(Of Array(Of MSingle)) = 单图处理(表图解码器, Background1), Image2Task As Task(Of Array(Of MSingle)) = 单图处理(里图解码器, Background2), Image1 As Array(Of MSingle) = Await Image1Task, Image2 As Array(Of MSingle) = Await Image2Task, Color As Array(Of MSingle) = Image2 - Image1, Alpha As Array(Of MSingle) = Color / (Background1 - Background2)
																										Color = (Image2 * Background1 - Image1 * Background2) / Color
																										Dim WeightedColor As Array(Of MSingle) = Alpha * Color / 255
																										Return Cat(0, UInt8(Color * 范围放缩(WeightedColor, 0, 65025) / WeightedColor).NByte, 范围放缩(Mean(Alpha, 0), 0, 255).UInt8.NByte)
																									End Function)
		生成位图 = New WriteableBitmap(最终字节.Size(1), 最终字节.Size(2))
		生成位图.PixelBuffer.AsStream.Write(最终字节, 0, 最终字节.Numel)
		明场预览.Source = 生成位图
		暗场预览.Source = 生成位图
		进度环.IsActive = False
	End Sub
End Class
