using System;
using MessagePack;

namespace WagahighChoices.Ashe
{
    [MessagePackObject]
    public class SeekDirectionResult
    {
        [Key(0)]
        public SeekDirectionResultKind Kind { get; }

        [Key(1)]
        public Guid JobId { get; set; }

        [Key(2)]
        public ChoiceAction[] Actions { get; }

        [SerializationConstructor]
        public SeekDirectionResult(SeekDirectionResultKind kind, Guid jobId, ChoiceAction[] actions)
        {
            this.Kind = kind;
            this.JobId = jobId;
            this.Actions = actions;
        }
    }

    public enum SeekDirectionResultKind
    {
        /// <summary>
        /// 指示が <see cref="SeekDirectionResult.Actions"/> に代入されています。
        /// </summary>
        Ok,
        /// <summary>
        /// すべてのジョブがワーカーに割り当てられているので、今は手を付けられる新しいジョブがありません。
        /// </summary>
        NotAvailable,
        /// <summary>
        /// 探索は終了しました。
        /// </summary>
        Finished,
    }
}
