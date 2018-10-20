using System;
using System.Collections.Generic;
using MagicOnion;
using MessagePack;

namespace WagahighChoices.Ashe
{
    public interface IAsheMagicOnionService : IService<IAsheMagicOnionService>
    {
        UnaryResult<SeekDirectionResult> SeekDirection();
        UnaryResult<Nil> ReportResult(Guid jobId, Heroine heroine, IReadOnlyList<int> selectionIds);
        UnaryResult<Nil> Log(string message, DateTimeOffset timestamp);
    }
}
