using System;

namespace WagahighChoices.Toa.Utils
{
    internal static class Log
    {
        public static void WriteMessage(string message)
        {
            try
            {
                Console.WriteLine("[{0}] {1}", DateTime.Now, message);
            }
            catch { }
        }

        public static void WriteMessage(string message, Client client)
        {
            WriteMessage(client.RemoteEndPoint + "\n" + message);
        }

        public static void LogException(Exception ex)
        {
            WriteMessage(ex.ToString());
        }

        public static void LogException(Exception ex, Client client)
        {
            WriteMessage(ex.ToString(), client);
        }
    }
}
