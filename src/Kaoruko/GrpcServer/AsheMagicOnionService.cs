using System;
using System.Collections.Generic;
using System.Linq;
using MagicOnion;
using MagicOnion.Server;
using MessagePack;
using SQLite;
using WagahighChoices.Ashe;
using WagahighChoices.Kaoruko.Models;

namespace WagahighChoices.Kaoruko.GrpcServer
{
    public class AsheMagicOnionService : ServiceBase<IAsheMagicOnionService>, IAsheMagicOnionService
    {
        private SQLiteConnection _connection;
        private SQLiteConnection Connection => this._connection ?? (this._connection = this.Context.GetRequiredService<SQLiteConnection>());

        public UnaryResult<SeekDirectionResult> SeekDirection()
        {
            this.SaveClientInfo();

            return new UnaryResult<SeekDirectionResult>(
                this.RunInImmediateTransaction(conn =>
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
                            WorkerId = this.GetWorkerId(),
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
                            this.GetWorkerId(), job.Id);
                    }

                    return new SeekDirectionResult(SeekDirectionResultKind.Ok, job.Id, actions);
                }));
        }

        public UnaryResult<Nil> ReportResult(Guid jobId, Heroine heroine, IReadOnlyList<int> selectionIds)
        {
            this.SaveClientInfo();

            this.RunInImmediateTransaction(conn =>
            {
                var job = conn.FindWithQuery<WorkerJob>(
                    "SELECT Choices FROM WorkerJob WHERE Id = ?",
                    jobId);

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
                    "UPDATE WorkerJob SET SearchResultId = ? WHERE Id = ?",
                    searchResult.Id, jobId);

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

            return new UnaryResult<Nil>(Nil.Default);
        }

        public UnaryResult<Nil> Log(string message, bool isError, DateTimeOffset timestamp)
        {
            this.SaveClientInfo();

            // トランザクションで守る必要なし
            var workerId = this.GetWorkerId();
            this.Connection.Insert(new WorkerLog()
            {
                WorkerId = workerId,
                Message = message,
                IsError = isError,
                TimestampOnWorker = timestamp,
                TimestampOnServer = DateTimeOffset.Now,
            });

            return new UnaryResult<Nil>(Nil.Default);
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

        private void SaveClientInfo()
        {
            var connectionId = this.GetConnectionContext().ConnectionId;
            if (string.IsNullOrEmpty(connectionId)) throw new InvalidOperationException("ConnectionId が指定されていません。");

            var hostName = this.Context.CallContext.RequestHeaders.GetValue(GrpcAsheServerContract.HostNameHeader, false);

            var setDisconnectAction = false;

            this.RunInImmediateTransaction(conn =>
            {
                var worker = conn.FindWithQuery<Worker>(
                    "SELECT Id, DisconnectedAt FROM Worker WHERE ConnectionId = ?",
                    connectionId);

                if (worker == null)
                {
                    // 新しい接続
                    worker = new Worker()
                    {
                        ConnectionId = connectionId,
                        HostName = hostName,
                        ConnectedAt = DateTimeOffset.Now,
                    };
                    var affectedRows = conn.Insert(worker, "OR IGNORE");

                    setDisconnectAction = affectedRows > 0;
                }
                else
                {
                    if (!worker.IsAlive)
                    {
                        // 再接続
                        conn.Execute(
                            "UPDATE Worker SET DisconnectedAt = NULL, HostName = ? WHERE Id = ?",
                            hostName, worker.Id);

                        setDisconnectAction = true;
                    }
                }
            });

            if (setDisconnectAction)
            {
                var databaseActivator = this.Context.GetRequiredService<DatabaseActivator>();
                this.GetConnectionContext().ConnectionStatus.Register(() =>
                {
                    // どこから呼び出されるかわからないものなので、別の SQLiteConnection を作成
                    using (var conn = databaseActivator.CreateConnection())
                    {
                        // DisconnectedAt をセット
                        conn.Execute(
                            "UPDATE Worker SET DisconnectedAt = ? WHERE ConnectionId = ? AND DisconnectedAt IS NULL",
                            DateTimeOffset.Now, connectionId);
                    }
                });
            }
        }

        private int GetWorkerId()
        {
            return this.Connection.ExecuteScalar<int>(
                "SELECT Id FROM Worker WHERE ConnectionId = ?",
                this.GetConnectionContext().ConnectionId);
        }
    }
}
