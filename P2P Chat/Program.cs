using System;

namespace P2P_Chat
{
    class Program
    {
        public static void Log(string message, bool newLine = true)
        {
            if (newLine)
            {
                Console.WriteLine($"\r[!]: {message}");
                Console.Write("> ");
            }
            else
                Console.Write($"[!]: {message} ");
        }

        static void Main(string[] args)
        {
            Log("Enter username >", newLine: false);
            var username = Console.ReadLine();
            var me = new Peer(username);

            while (true)
            {
                var msg = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(msg))
                    me.SendMessage(msg);
                Console.Write("\r> ");
            }
        }
    }
}
