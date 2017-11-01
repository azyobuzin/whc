using System.Threading.Tasks;
using MagicOnion;
using MessagePack;

namespace WagahighChoices.Toa.Grpc.Internal
{
    public interface IToaMagicOnionService : IService<IToaMagicOnionService>
    {
        UnaryResult<Argb32Image> CaptureContent();
        UnaryResult<Nil> SetCursorPosition(short x, short y);
        UnaryResult<Nil> MouseClick();
        UnaryResult<Argb32Image> GetCursorImage();
        Task<ServerStreamingResult<string>> LogStream();
    }
}
