using System;
using System.Net.Sockets;

namespace WagahighChoices.Toa.Utils
{
    internal static class Log
    {
        public static void WriteMessage(string message)
        {
            Console.WriteLine("[{0}] {1}", DateTime.Now, message);
        }

        public static void WriteMessage(string message, TcpClient client)
        {
            WriteMessage(client.Client.RemoteEndPoint + "\n" + message);
        }

        public static void LogException(Exception ex)
        {
            WriteMessage(ex.ToString());
        }

        public static void LogException(Exception ex, TcpClient client)
        {
            WriteMessage(ex.ToString(), client);
        }
    }
}
