using System;

namespace WagahighChoices.Utils
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
    }
}
