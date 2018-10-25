using System;
using System.Collections.Generic;
using MagicOnion;
using MessagePack;
using WagahighChoices.Toa.Imaging;

namespace WagahighChoices.Ashe
{
    public interface IAsheMagicOnionService : IService<IAsheMagicOnionService>
    {
        UnaryResult<SeekDirectionResult> SeekDirection();
        UnaryResult<Nil> ReportResult(Guid jobId, Heroine heroine, IReadOnlyList<int> selectionIds);
        UnaryResult<Nil> Log(string message, bool isError, DateTimeOffset timestamp);
        UnaryResult<Nil> ReportScreenshot(Bgra32Image screenshot, DateTimeOffset timestamp);
    }
}
