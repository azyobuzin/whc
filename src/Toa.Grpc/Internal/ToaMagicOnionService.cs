using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using MagicOnion;
using MagicOnion.Server;
using MessagePack;
using WagahighChoices.Toa.Imaging;

namespace WagahighChoices.Toa.Grpc.Internal
{
    public class ToaMagicOnionService : ServiceBase<IToaMagicOnionService>, IToaMagicOnionService
    {
        public WagahighOperator WagahighOperator => this.Context.Items[InjectWagahighOperatorFilterAttribute.ItemKey] as WagahighOperator
            ?? throw new InvalidOperationException(nameof(InjectWagahighOperatorFilterAttribute) + " が指定されていません。");

        public UnaryResult<Bgra32Image> CaptureContent()
        {
            return new UnaryResult<Bgra32Image>(this.WagahighOperator.CaptureContentAsync());
        }

        public UnaryResult<Size> GetContentSize()
        {
            return new UnaryResult<Size>(this.WagahighOperator.GetContentSizeAsync());
        }

        public async UnaryResult<Nil> SetCursorPosition(short x, short y)
        {
            await this.WagahighOperator.SetCursorPositionAsync(x, y).ConfigureAwait(false);
            return Nil.Default;
        }

        public async UnaryResult<Nil> MouseClick()
        {
            await this.WagahighOperator.MouseClickAsync().ConfigureAwait(false);
            return Nil.Default;
        }

        public UnaryResult<Bgra32Image> GetCursorImage()
        {
            return new UnaryResult<Bgra32Image>(this.WagahighOperator.GetCursorImageAsync());
        }

        public async Task<ServerStreamingResult<string>> LogStream()
        {
            var context = this.GetServerStreamingContext<string>();
            var cancellationToken = this.Context.CallContext.CancellationToken;

            using (var enumerator = this.WagahighOperator.LogStream.ToAsyncEnumerable().GetEnumerator())
            {
                // 1件ずつ取り出して、スレッドセーフにやっていく
                while (await enumerator.MoveNext(cancellationToken).ConfigureAwait(false))
                {
                    await context.WriteAsync(enumerator.Current).ConfigureAwait(false);
                }
            }

            return context.Result();
        }
    }
}
