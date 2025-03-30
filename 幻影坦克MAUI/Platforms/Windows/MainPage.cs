using SkiaSharp;
using Windows.Storage.Streams;

namespace 幻影坦克MAUI
{
	partial class MainPage
	{
		Windows.ApplicationModel.DataTransfer.DataPackage 数据包 = new();
		InMemoryRandomAccessStream 内存随机访问流 = new();
		private void 复制SK图(SKData SK图)
		{
			内存随机访问流.Seek(0);
			SK图.AsStream().CopyTo(内存随机访问流.AsStreamForWrite());
			数据包.SetBitmap(RandomAccessStreamReference.CreateFromStream(内存随机访问流));
			Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(数据包);
		}
		private async Task<Stream> 粘贴(Image 控件)
		{
			Windows.ApplicationModel.DataTransfer.DataPackageView 数据包视图 = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
			if (数据包视图.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Bitmap))
			{
				Stream 流 = (await (await 数据包视图.GetBitmapAsync()).OpenReadAsync()).AsStreamForRead();
				控件.Source = ImageSource.FromStream(() => 流拷贝(流));
				return 流;
			}
			return null;
		}
	}
}
