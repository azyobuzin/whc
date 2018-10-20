using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace WagahighChoices.Ashe
{
    internal class ConsoleSearchDirector : SearchDirector
    {
        public override Task<SeekDirectionResult> SeekDirectionAsync()
        {
            return Task.Factory.StartNew(
                () =>
                {
                    Console.WriteLine("新しい指示\n0: Ok\n1: NotAvailable\n2: Finished");
                    var kind = (SeekDirectionResultKind)Prompt.GetInt("種類 > ", 0);

                    if (kind != SeekDirectionResultKind.Ok)
                        return new SeekDirectionResult(kind);

                    while (true)
                    {
                        Console.WriteLine("選択肢を指定してください（ex. 0101）\n0: 上, 1: 下");
                        var selectionStr = Prompt.GetString("選択肢 > ").Trim();

                        if (selectionStr.Any(x => x != '0' && x != '1')) continue;

                        var actions = selectionStr.Select(x => (ChoiceAction)(x - '0')).ToArray();
                        return new SeekDirectionResult(kind, Guid.NewGuid(), actions);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            );
        }

        public override Task ReportResultAsync(Guid jobId, Heroine heroine, IReadOnlyList<int> selectionIds)
        {
            Console.WriteLine(
                "[{0}] ジョブ {1} が完了\nルート: {2}\n通った選択肢: {3}",
                DateTime.Now,
                jobId,
                heroine,
                string.Join(" → ", selectionIds)
            );

            return Task.CompletedTask;
        }

        public override Task LogAsync(string message, bool isError, DateTimeOffset timestamp)
        {
            // ログは Logger が stdout に吐いてくれているので、ここでは何もしない
            return Task.CompletedTask;
        }
    }
}
