using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MagicOnion;
using MagicOnion.Server;
using MessagePack;
using SQLite;
using WagahighChoices.Ashe;
using WagahighChoices.GrpcUtils;
using WagahighChoices.Kaoruko.Models;
using WagahighChoices.Toa.Imaging;

namespace WagahighChoices.Kaoruko.GrpcServer
{
    public class AsheMagicOnionService : ServiceBase<IAsheMagicOnionService>, IAsheMagicOnionService
    {
        private SQLiteConnection _connection;
        private SQLiteConnection Connection => this._connection ?? (this._connection = this.Context.GetRequiredService<SQLiteConnection>());

        public async UnaryResult<SeekDirectionResult> SeekDirection()
        {
            var workerId = await this.WorkerInitializeAsync().ConfigureAwait(false);

            return await this.RetryWhenLocked(() =>
            {
                return this.RunInImmediateTransaction(conn =>
                {
                    // 未着手のジョブを取得する
                    var job = conn.FindWithQuery<WorkerJob>(
                        "SELECT Id, Choices FROM WorkerJob WHERE WorkerId IS NULL LIMIT 1");

                    ChoiceAction[] actions;

                    if (job == null)
                    {
                        var incompletedJobCount = conn.ExecuteScalar<int>(
                            "SELECT Count(*) FROM WorkerJob WHERE SearchResultId IS NULL");

                        if (incompletedJobCount > 0)
                        {
                            // 未完了のジョブがあるが、現在着手可能なジョブはない
                            return new SeekDirectionResult(SeekDirectionResultKind.NotAvailable);
                        }

                        if (conn.Table<SearchResult>().Count() > 0)
                        {
                            // 探索結果が揃っているので、探索完了とみなす
                            return new SeekDirectionResult(SeekDirectionResultKind.Finished);
                        }

                        // 初回ジョブを作成
                        job = new WorkerJob()
                        {
                            Id = Guid.NewGuid(),
                            Choices = "",
                            WorkerId = workerId,
                            EnqueuedAt = DateTimeOffset.Now,
                        };
                        conn.Insert(job);

                        actions = Array.Empty<ChoiceAction>();
                    }
                    else
                    {
                        actions = ModelUtils.ParseChoices(job.Choices);

                        conn.Execute(
                            "UPDATE WorkerJob SET WorkerId = ? WHERE Id = ?",
                            workerId, job.Id);
                    }

                    Utils.Log.WriteMessage($"ワーカー #{workerId} にジョブ {job.Id} を指示");

                    return new SeekDirectionResult(SeekDirectionResultKind.Ok, job.Id, actions);
                });
            }).ConfigureAwait(false);
        }

        public async UnaryResult<Nil> ReportResult(Guid jobId, Heroine heroine, IReadOnlyList<int> selectionIds)
        {
            var workerId = await this.WorkerInitializeAsync().ConfigureAwait(false);

            await this.RetryWhenLocked(() =>
            {
                this.RunInImmediateTransaction(conn =>
                {
                    var job = conn.FindWithQuery<WorkerJob>(
                        "SELECT Choices, SearchResultId FROM WorkerJob WHERE Id = ?",
                        jobId);

                    if (job.SearchResultId.HasValue)
                    {
                        // すでに結果報告済み
                        Utils.Log.WriteMessage($"報告が重複しています (Worker: {workerId})");
                        return;
                    }

                    var jobChoices = ModelUtils.ParseChoices(job.Choices);

                    // 実際に選択したものは、 job.Choices + ずっと上
                    var choices = jobChoices.Concat(
                        Enumerable.Repeat(ChoiceAction.SelectUpper, selectionIds.Count - jobChoices.Length));

                    var searchResult = new SearchResult()
                    {
                        Selections = ModelUtils.ToSelectionsString(selectionIds),
                        Choices = ModelUtils.ToChoicesString(choices),
                        Heroine = heroine,
                        Timestamp = DateTimeOffset.Now,
                    };

                    conn.Insert(searchResult);

                    conn.Execute(
                        "UPDATE WorkerJob SET WorkerId = ?, SearchResultId = ? WHERE Id = ?",
                        workerId, searchResult.Id, jobId);

                    // このレポートで発見された未探索のジョブを作成
                    for (var i = jobChoices.Length; i < selectionIds.Count; i++)
                    {
                        // 未探索の下を選ぶ
                        var newChoices = choices.Take(i).Append(ChoiceAction.SelectLower);

                        var newJob = new WorkerJob()
                        {
                            Id = Guid.NewGuid(),
                            Choices = ModelUtils.ToChoicesString(newChoices),
                            EnqueuedAt = DateTimeOffset.Now,
                        };

                        conn.Insert(newJob);
                    }
                });
            }).ConfigureAwait(false);

            return Nil.Default;
        }

        public async UnaryResult<Nil> Log(string message, bool isError, DateTimeOffset timestamp)
        {
            var workerId = await this.WorkerInitializeAsync().ConfigureAwait(false);

            await this.RetryWhenLocked(() =>
            {
                this.Connection.Insert(new WorkerLog()
                {
                    WorkerId = workerId,
                    Message = message,
                    IsError = isError,
                    TimestampOnWorker = timestamp,
                    TimestampOnServer = DateTimeOffset.Now,
                });
            }).ConfigureAwait(false);

            return Nil.Default;
        }

        public async UnaryResult<Nil> ReportScreenshot(Bgra32Image screenshot, DateTimeOffset timestamp)
        {
            using (screenshot)
            {
                var workerId = await this.WorkerInitializeAsync().ConfigureAwait(false);

                await this.RetryWhenLocked(() =>
                {
                    this.Connection.InsertOrReplace(new WorkerScreenshot()
                    {
                        WorkerId = workerId,
                        Width = screenshot.Width,
                        Height = screenshot.Height,
                        Data = screenshot.Data.ToArray(),
                        TimestampOnWorker = timestamp,
                        TimestampOnServer = DateTimeOffset.Now,
                    });
                }).ConfigureAwait(false);
            }

            return Nil.Default;
        }

        private void RunInImmediateTransaction(Action<SQLiteConnection> action)
        {
            var connection = this.Connection;
            connection.Execute("BEGIN IMMEDIATE");

            try
            {
                action(connection);
                connection.Execute("COMMIT");
            }
            catch (Exception ex)
            {
                try
                {
                    connection.Execute("ROLLBACK");
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(ex, rollbackException);
                }

                throw;
            }
        }

        private T RunInImmediateTransaction<T>(Func<SQLiteConnection, T> action)
        {
            var connection = this.Connection;
            connection.Execute("BEGIN IMMEDIATE");

            try
            {
                var result = action(connection);
                connection.Execute("COMMIT");
                return result;
            }
            catch (Exception ex)
            {
                try
                {
                    connection.Execute("ROLLBACK");
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(ex, rollbackException);
                }

                throw;
            }
        }

        /// <summary>
        /// 接続してきたワーカーの情報を保存
        /// </summary>
        /// <returns><see cref="Worker.Id"/></returns>
        private async ValueTask<int> WorkerInitializeAsync()
        {
            var connectionId = this.GetConnectionContext().ConnectionId;
            if (string.IsNullOrEmpty(connectionId)) throw new InvalidOperationException("ConnectionId が指定されていません。");

            if (this.GetConnectionContext().ConnectionStatus.IsCancellationRequested)
                throw new InvalidOperationException($"コネクション {connectionId} はすでに切断処理を行いました。");

            var hostName = this.Context.CallContext.RequestHeaders.GetValue(GrpcAsheServerContract.HostNameHeader, false);

            var setDisconnectAction = false;
            var workerId = await this.RetryWhenLocked(() =>
            {
                return this.RunInImmediateTransaction(conn =>
                {
                    var worker = conn.FindWithQuery<Worker>(
                        "SELECT Id, DisconnectedAt FROM Worker WHERE ConnectionId = ?",
                        connectionId);

                    if (worker == null)
                    {
                        Utils.Log.WriteMessage("新規接続 " + connectionId);

                        worker = new Worker()
                        {
                            ConnectionId = connectionId,
                            HostName = hostName,
                            ConnectedAt = DateTimeOffset.Now,
                        };
                        conn.Insert(worker);

                        setDisconnectAction = true;
                    }
                    else if (!worker.IsAlive)
                    {
                        throw new InvalidOperationException($"コネクション {connectionId} はすでに切断処理を行いました。");
                    }

                    return worker.Id;
                });
            }).ConfigureAwait(false);

            if (setDisconnectAction)
            {
                var databaseActivator = this.Context.GetRequiredService<DatabaseActivator>();
                this.GetConnectionContext().ConnectionStatus.Register(async () =>
                {
                    Utils.Log.WriteMessage("切断 " + connectionId);

                    // どこから呼び出されるかわからないものなので、別の SQLiteConnection を作成
                    using (var conn = databaseActivator.CreateConnection())
                    {
                        await this.RetryWhenLocked(() =>
                        {
                            conn.Execute("BEGIN IMMEDIATE");

                            // DisconnectedAt をセット
                            conn.Execute(
                                "UPDATE Worker SET DisconnectedAt = ? WHERE Id = ? AND DisconnectedAt IS NULL",
                                DateTimeOffset.Now, workerId);

                            // 担当ジョブを放棄
                            conn.Execute(
                                "UPDATE WorkerJob SET WorkerId = NULL WHERE WorkerId = ? AND SearchResultId IS NULL",
                                workerId);

                            conn.Execute("COMMIT");
                        }).ConfigureAwait(false);
                    }
                });
            }

            return workerId;
        }

        private async Task RetryWhenLocked(Action action)
        {
            while (true)
            {
                try
                {
                    action();
                    return;
                }
                catch (SQLiteException ex)
                {
                    switch (ex.Result)
                    {
                        case SQLite3.Result.Busy:
                        case SQLite3.Result.Locked:
                            break;
                        default:
                            throw;
                    }
                }

                await Task.Delay(100).ConfigureAwait(false);
            }
        }

        private async ValueTask<T> RetryWhenLocked<T>(Func<T> action)
        {
            while (true)
            {
                try
                {
                    return action();
                }
                catch (SQLiteException ex)
                {
                    switch (ex.Result)
                    {
                        case SQLite3.Result.Busy:
                        case SQLite3.Result.Locked:
                            break;
                        default:
                            throw;
                    }
                }

                await Task.Delay(100).ConfigureAwait(false);
            }
        }
    }
}
