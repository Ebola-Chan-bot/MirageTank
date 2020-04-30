' https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板
Imports Windows.Storage, Windows.Graphics.Imaging, Windows.UI, MATLAB, Windows.ApplicationModel.DataTransfer, Windows.Storage.Streams
''' <summary>
''' 可用于自身或导航至 Frame 内部的空白页。
''' </summary>
Public NotInheritable Class MainPage
	Inherits Page
	Private 资产文件夹 As StorageFolder, 表图解码器 As BitmapDecoder, 里图解码器 As BitmapDecoder, 生成位图 As SoftwareBitmap, 表图流 As IRandomAccessStream, 里图流 As IRandomAccessStream, 生成位图流 As RandomAccessStreamReference
	Shared ReadOnly 文件保存器 As New Pickers.FileSavePicker, 文件打开器 As New Pickers.FileOpenPicker
	Private Async Function 获取软件位图(解码器 As BitmapDecoder) As Task(Of SoftwareBitmap)
		Dim b As SoftwareBitmap = Await 解码器.GetSoftwareBitmapAsync
		If b.BitmapPixelFormat <> BitmapPixelFormat.Bgra8 OrElse b.BitmapAlphaMode <> BitmapAlphaMode.Premultiplied Then b = SoftwareBitmap.Convert(b, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
		Return b
	End Function
	Private Async Sub 异步初始化()
		资产文件夹 = Await Package.Current.InstalledLocation.GetFolderAsync("Assets")
		载入表图(Await 资产文件夹.GetFileAsync("表图.png"))
		载入里图(Await 资产文件夹.GetFileAsync("里图.png"))
	End Sub
	Private Async Sub 载入表图(数据源 As IRandomAccessStreamReference)
		表图流 = Await 数据源.OpenReadAsync
		表图解码器 = Await BitmapDecoder.CreateAsync(表图流)
		Call 原表图.SetBitmapAsync(Await 获取软件位图(表图解码器))
	End Sub
	Private Async Sub 表图_Tapped(sender As Object, e As TappedRoutedEventArgs) Handles 表图.Tapped
		Dim a As StorageFile = Await 文件打开器.PickSingleFileAsync
		If a IsNot Nothing Then 载入表图(a)
	End Sub
	Private Async Sub 载入里图(数据源 As IRandomAccessStreamReference)
		里图流 = Await 数据源.OpenReadAsync
		里图解码器 = Await BitmapDecoder.CreateAsync(里图流)
		Call 原里图.SetBitmapAsync(Await 获取软件位图(里图解码器))
	End Sub

	Private Async Sub 里图_Tapped(sender As Object, e As TappedRoutedEventArgs) Handles 里图.Tapped
		Dim a As StorageFile = Await 文件打开器.PickSingleFileAsync
		If a IsNot Nothing Then 载入里图(a)
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
		If 生成位图 Is Nothing Then
			未生成图像.ShowAt(Generate)
		Else
			Dim a As IAsyncOperation(Of StorageFile) = 文件保存器.PickSaveFileAsync, b As StorageFile = Await a
			If b IsNot Nothing Then
				Dim g As Stream = Await b.OpenStreamForWriteAsync
				g.SetLength(0)
				g.Close()
				Dim f As IRandomAccessStream = Await b.OpenAsync(FileAccessMode.ReadWrite), d As BitmapEncoder = Await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, f)
				d.SetSoftwareBitmap(生成位图)
				Await d.FlushAsync()
				f.Dispose()
			End If
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

	Private Sub Image_DragOver(sender As Object, e As DragEventArgs) Handles 表图.DragOver, 里图.DragOver
		If e.DataView.Contains(StandardDataFormats.Bitmap) Then
			e.AcceptedOperation = DataPackageOperation.Copy
		ElseIf e.DataView.Contains(StandardDataFormats.StorageItems) Then
			e.AcceptedOperation = DataPackageOperation.Link
		End If
	End Sub
	Private Async Sub 数据包表图(数据包 As DataPackageView)
		If 数据包.Contains(StandardDataFormats.Bitmap) Then
			载入表图(Await 数据包.GetBitmapAsync)
		ElseIf 数据包.Contains(StandardDataFormats.StorageItems) Then
			Dim c As IStorageFile = (Await 数据包.GetStorageItemsAsync()).First(Function(d As IStorageItem) d.IsOfType(StorageItemTypes.File))
			If c IsNot Nothing Then 载入表图(c)
		End If
	End Sub
	Private Async Sub 数据包里图(数据包 As DataPackageView)
		If 数据包.Contains(StandardDataFormats.Bitmap) Then
			载入里图(Await 数据包.GetBitmapAsync)
		ElseIf 数据包.Contains(StandardDataFormats.StorageItems) Then
			Dim c As IStorageFile = (Await 数据包.GetStorageItemsAsync()).First(Function(d As IStorageItem) d.IsOfType(StorageItemTypes.File))
			If c IsNot Nothing Then 载入里图(c)
		End If
	End Sub
	Private Sub 表图_Drop(sender As Object, e As DragEventArgs) Handles 表图.Drop
		数据包表图(e.DataView)
	End Sub

	Private Sub 里图_DragStarting(sender As UIElement, args As DragStartingEventArgs) Handles 里图.DragStarting
		args.Data.SetBitmap(RandomAccessStreamReference.CreateFromStream(里图流))
	End Sub

	Private Sub 复制表图_Click(sender As Object, e As RoutedEventArgs) Handles 复制表图.Click
		Dim a As New DataPackage
		a.SetBitmap(RandomAccessStreamReference.CreateFromStream(表图流))
		Clipboard.SetContent(a)
	End Sub

	Private Sub 粘贴表图_Click(sender As Object, e As RoutedEventArgs) Handles 粘贴表图.Click
		数据包表图(Clipboard.GetContent)
	End Sub

	Private Sub 复制里图_Click(sender As Object, e As RoutedEventArgs) Handles 复制里图.Click
		Dim a As New DataPackage
		a.SetBitmap(RandomAccessStreamReference.CreateFromStream(里图流))
		Clipboard.SetContent(a)
	End Sub

	Private Sub 粘贴里图_Click(sender As Object, e As RoutedEventArgs) Handles 粘贴里图.Click
		数据包里图(Clipboard.GetContent)
	End Sub
	Private Sub 复制生成图()
		Dim a As New DataPackage
		a.SetBitmap(生成位图流)
		Clipboard.SetContent(a)
	End Sub
	Private Sub 场预览_RightTapped(sender As Object, e As RightTappedRoutedEventArgs) Handles 明场预览.RightTapped, 暗场预览.RightTapped
		复制生成图()
		已复制.ShowAt(sender)
	End Sub

	Private Sub 场预览_DragStarting(sender As UIElement, args As DragStartingEventArgs) Handles 明场预览.DragStarting, 暗场预览.DragStarting
		args.Data.SetBitmap(生成位图流)
	End Sub

	Private Sub 里图_Drop(sender As Object, e As DragEventArgs) Handles 里图.Drop
		数据包里图(e.DataView)
	End Sub

	Private Sub 表图_DragStarting(sender As UIElement, args As DragStartingEventArgs) Handles 表图.DragStarting
		args.Data.SetBitmap(RandomAccessStreamReference.CreateFromStream(表图流))
	End Sub

	Shared ReadOnly 透明索引 As IntegerColon() = {Colon(3, 3), Colon(0, Nothing), Colon(0, Nothing)}, 颜色索引 As IntegerColon() = {Colon(0, 2), Colon(0, Nothing), Colon(0, Nothing)}, 三色权重 As New SingleArray({3, 1, 1}, {0.114, 0.587, 0.2989}), Cs1索引 As IntegerColon() = {Colon(0, Nothing), Colon(0, Nothing), Colon(0, Nothing), Colon(0, 0)}, Cs2索引 As IntegerColon() = {Colon(0, Nothing), Colon(0, Nothing), Colon(0, Nothing), Colon(1, 1)}

	Private Async Function 单图处理(解码器 As BitmapDecoder, 背景 As SingleArray, 高度 As Integer, 宽度 As Integer) As Task(Of SingleArray)
		Dim 像素高度 As UInteger = 解码器.PixelHeight, 像素宽度 As UInteger = 解码器.PixelWidth, 放大倍数 As Single = Math.Min(高度 / 像素高度, 宽度 / 像素宽度), 放大宽度 As Integer = 像素宽度 * 放大倍数, 放大高度 As Integer = 像素高度 * 放大倍数, 位图变换 As New BitmapTransform With {.ScaledHeight = 放大高度, .ScaledWidth = 放大宽度}, c As New ByteArray((Await 解码器.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, 位图变换, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage)).DetachPixelData)
		c.Reshape(4, 放大宽度, 放大高度)
		If 高度 > 放大高度 OrElse 宽度 > 放大宽度 Then
			Dim f As New ByteArray({4, 宽度, 高度}), g As Integer = (宽度 - 放大宽度) / 2, h As Integer = (高度 - 放大高度) / 2
			f({Colon(0, Nothing), Colon(g, g + 放大宽度 - 1), Colon(h, h + 放大高度 - 1)}) = c
			c = f
		End If
		Dim a As SingleArray = ToSingle(c), b As SingleArray = a(透明索引)
		Return BsxFun(Function(d As Single, e As Single) (d + e) / 255, b * a(颜色索引), BsxFun(Function(d As Single, e As Single) (255 - d) * e, b, 背景))
	End Function
	Private Function 颜色校正(Cs As SingleArray, dCs As SingleArray, Plus As Boolean) As Task(Of SingleArray)
		If Plus Then
			Return Task.Run(Function() BsxFun(Function(a As Single, b As Single) (a + b) / 2, Cs, dCs))
		Else
			Return Task.Run(Function() BsxFun(Function(a As Single, b As Single) (a - b) / 2, Cs, dCs))
		End If
	End Function
	Private Async Sub Generate_Click(sender As Object, e As RoutedEventArgs) Handles Generate.Click
		进度环.IsActive = True
		Dim g As Color = 前景色.Color, h As Color = 背景色.Color, 最终字节 = Await Task.Run(Async Function() As Task(Of ByteArray)
																					  Dim Cb1 As SingleArray = New ByteArray({g.B, g.G, g.R}).ToSingle, Cb2 As SingleArray = New ByteArray({h.B, h.G, h.R}).ToSingle, 宽度 As Integer = Math.Max(表图解码器.PixelWidth, 里图解码器.PixelWidth), 高度 As Integer = Math.Max(表图解码器.PixelHeight, 里图解码器.PixelHeight), Image1Task As Task(Of SingleArray) = 单图处理(表图解码器, Cb1, 高度, 宽度), Image2Task As Task(Of SingleArray) = 单图处理(里图解码器, Cb2, 高度, 宽度), Cs1 As SingleArray = Await Image1Task, Cs2 As SingleArray = Await Image2Task, dCb As SingleArray = Cb1 - Cb2, dCs As SingleArray = Sum(三色权重 ^ 2 * dCb * (Cs1 - Cs2), 0) / Sum(BsxFun(Function(a As Single, b As Single) CSng((a * b) ^ 2), 三色权重, dCb))
																					  dCs(IsNan(dCs)) = 0
																					  dCs *= dCb
																					  Dim Cs As SingleArray = Cs1 + Cs2
																					  Cs = 范围放缩(Cat(3, Await 颜色校正(Cs, dCs, True), Await 颜色校正(Cs, dCs, False)))
																					  Cs1 = Cs(Cs1索引)
																					  Cs2 = Cs(Cs2索引)
																					  Dim Alpha As SingleArray = 范围放缩(ArrayFun(Function(a As Single) 255 * (1 - a), (Cs1 - Cs2) / dCb))
																					  Alpha(IsNan(Alpha)) = 255
																					  Alpha = Mean(Alpha, 0)
																					  Dim Cb As SingleArray = Cb1 + Cb2, Color As SingleArray = 范围放缩(BsxFun(Function(a As Single, b As Single) (a + b) / 2, BsxFun(Function(a As Single, b As Single) 255 * a / b, Cs1 + Cs2 - Cb, Alpha), Cb))
																					  Return Cat(0, ToByte(Color), ToByte(Alpha))
																				  End Function)
		Dim k As New WriteableBitmap(最终字节.Size(1), 最终字节.Size(2)), c As IBuffer = k.PixelBuffer
		c.AsStream.Write(最终字节.本体.ToArray, 0, 最终字节.NumEl)
		Dim j As New InMemoryRandomAccessStream, d As BitmapEncoder = Await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, j)
		生成位图 = SoftwareBitmap.CreateCopyFromBuffer(c, BitmapPixelFormat.Bgra8, k.PixelWidth, k.PixelHeight, BitmapAlphaMode.Straight)
		d.SetSoftwareBitmap(生成位图)
		Await d.FlushAsync()
		生成位图流 = RandomAccessStreamReference.CreateFromStream(j)
		明场预览.Source = k
		暗场预览.Source = k
		进度环.IsActive = False
	End Sub

	Private Ctrl As Boolean
	Private Sub MainPage_KeyUp(sender As Object, e As KeyRoutedEventArgs) Handles Me.KeyUp
		Select Case e.Key
			Case Windows.System.VirtualKey.Control
				Ctrl = False
			Case Windows.System.VirtualKey.C
				If Ctrl Then 复制生成图()
		End Select
	End Sub

	Private Sub MainPage_KeyDown(sender As Object, e As KeyRoutedEventArgs) Handles Me.KeyDown
		If e.Key = Windows.System.VirtualKey.Control Then Ctrl = True
	End Sub
End Class
