using System;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.EntityFrameworkCore;
using WagahighChoices.Ashe;
using WagahighChoices.Kaoruko.Models;

namespace Kaoruko
{
    [Command(Name = "kaoruko", FullName = "Kaoruko", Description = "探索状況管理サーバー")]
    public class Program
    {
        public static int Main(string[] args)
        {
            return CommandLineApplication.Execute<Program>(args);
        }

        [Option("--ashe-port <port>", Description = "Ashe からの接続を受け入れるポート番号（デフォルト: 50222）")]
        public int AshePort { get; set; } = GrpcAsheServer.DefaultPort;

        [Option("--web-port <port>", Description = "管理 Web のポート番号（デフォルト: 50416）")]
        public int WebPort { get; set; } = 50416;

        [Option("--db <path>", Description = "データベースのパス（デフォルト: ./kaoruko.sqlite3）")]
        public string DatabasePath { get; set; } = "./kaoruko.sqlite3";

        private int OnExecute()
        {
            // TODO
            return 0;
        }
    }
}
