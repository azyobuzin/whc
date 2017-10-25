using System;
using MagicOnion;
using MagicOnion.Server;
using MessagePack;

namespace WagahighChoices.Toa.Grpc.Internal
{
    public class ToaMagicOnionService : ServiceBase<IToaMagicOnionService>, IToaMagicOnionService
    {
        public WagahighOperator WagahighOperator => this.Context.Items[InjectWagahighOperatorFilterAttribute.ItemKey] as WagahighOperator
            ?? throw new InvalidOperationException(nameof(InjectWagahighOperatorFilterAttribute) + " が指定されていません。");

        public UnaryResult<Argb32Image> CaptureContent()
        {
            return new UnaryResult<Argb32Image>(this.WagahighOperator.CaptureContentAsync());
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

        public UnaryResult<Argb32Image> GetCursorImage()
        {
            return new UnaryResult<Argb32Image>(this.WagahighOperator.GetCursorImageAsync());
        }
    }
}
