using Windows.Storage.Streams;

namespace 幻影坦克MAUI
{
	partial class MainPage
	{
		Windows.ApplicationModel.DataTransfer.DataPackage 数据包 = new();
		InMemoryRandomAccessStream 内存随机访问流 = new();
		private void 复制生成图()
		{
			内存随机访问流.Seek(0);
			预览图.AsStream().CopyTo(内存随机访问流.AsStreamForWrite());
			数据包.SetBitmap(RandomAccessStreamReference.CreateFromStream(内存随机访问流));
			Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(数据包);
		}
	}
}
