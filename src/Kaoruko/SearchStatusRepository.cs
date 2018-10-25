using System.Collections.Generic;
using System.Linq;
using SQLite;
using WagahighChoices.Ashe;
using WagahighChoices.Kaoruko.Models;

namespace WagahighChoices.Kaoruko
{
    public class SearchStatusRepository
    {
        private readonly SQLiteConnection _connection;

        public SearchStatusRepository(SQLiteConnection connection)
        {
            this._connection = connection;
        }

        public IReadOnlyDictionary<Heroine, int> GetRouteCountByHeroine()
        {
            return this._connection
                .Query<RouteCountByHeroine>("SELECT Heroine, COUNT(*) AS Count FROM SearchResult GROUP BY Heroine")
                .ToDictionary(x => x.Heroine, x => x.Count);
        }

        private class RouteCountByHeroine
        {
            public Heroine Heroine { get; set; }
            public int Count { get; set; }
        }

        public WorkerJobStatistics GetWorkerJobStatistics()
        {
            var jobs = this._connection.Query<WorkerJob>(
                "SELECT WorkerId, SearchResultId FROM WorkerJob");

            return new WorkerJobStatistics()
            {
                JobCount = jobs.Count,
                CompletedJobCount = jobs.Count(x => x.SearchResultId.HasValue),
                RunningJobCount = jobs.Count(x => x.WorkerId.HasValue && !x.SearchResultId.HasValue),
                PendingJobCount = jobs.Count(x => !x.WorkerId.HasValue),
            };
        }

        public IReadOnlyList<WorkerSummary> GetWorkerSummaries(bool aliveOnly)
        {
            var sql = "SELECT Id, DisconnectedAt IS NULL AS IsAlive, HostName, ErrorLogCount FROM Worker "
                + "LEFT JOIN (SELECT WorkerId, COUNT(*) AS ErrorLogCount FROM WorkerLog WHERE IsError != 0 GROUP BY WorkerId) "
                + "ON Id = WorkerId";
            if (aliveOnly) sql += " WHERE DisconnectedAt IS NULL";
            sql += " ORDER BY Id DESC";
            return this._connection.Query<WorkerSummary>(sql);
        }

        public Worker GetWorkerById(int id)
        {
            return this._connection.Find<Worker>(id);
        }

        public int CountCompletedJobsByWorker(int workerId)
        {
            return this._connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM WorkerJob WHERE WorkerId = ? AND SearchResultId IS NOT NULL",
                workerId);
        }

        public WorkerJob GetJobByWorker(int workerId)
        {
            return this._connection.FindWithQuery<WorkerJob>(
                "SELECT * FROM WorkerJob WHERE WorkerId = ? AND SearchResultId IS NULL LIMIT 1",
                workerId);
        }

        public IReadOnlyList<WorkerLog> GetLogsByWorker(int workerId)
        {
            return this._connection.Query<WorkerLog>(
                "SELECT * FROM WorkerLog WHERE WorkerId = ?",
                workerId);
        }

        public WorkerScreenshot GetScreenshotByWorker(int workerId)
        {
            return this._connection.Find<WorkerScreenshot>(workerId);
        }
    }

    public class WorkerJobStatistics
    {
        public int JobCount { get; set; }
        public int CompletedJobCount { get; set; }
        public int RunningJobCount { get; set; }
        public int PendingJobCount { get; set; }
    }

    public class WorkerSummary
    {
        public int Id { get; set; }
        public bool IsAlive { get; set; }
        public string HostName { get; set; }
        public int ErrorLogCount { get; set; }
    }
}
