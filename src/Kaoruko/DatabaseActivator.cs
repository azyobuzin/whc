using System;
using SQLite;
using WagahighChoices.Kaoruko.Models;

namespace WagahighChoices.Kaoruko
{
    public class DatabaseActivator
    {
        public string DatabasePath { get; }

        public DatabaseActivator(string databasePath)
        {
            this.DatabasePath = databasePath;
        }

        public SQLiteConnection CreateConnection()
        {
            return new SQLiteConnection(this.DatabasePath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex);
        }

        public void Initialize()
        {
            using (var connection = this.CreateConnection())
            {
                connection.CreateTables(
                    CreateFlags.None,
                    typeof(SearchResult),
                    typeof(Worker),
                    typeof(WorkerJob),
                    typeof(WorkerLog)
                );

                connection.RunInTransaction(() =>
                {
                    // 起動時に接続済みワーカーは存在しないので、すべて切断済みとしてマークする
                    connection.Execute(
                        "UPDATE Worker SET DisconnectedAt = ? WHERE DisconnectedAt IS NULL",
                        DateTimeOffset.Now);

                    // 未完了のジョブに紐づくワーカーをすべて削除する
                    connection.Execute(
                        "UPDATE WorkerJob SET WorkerId = NULL WHERE SearchResultId IS NULL");
                });
            }
        }
    }
}
