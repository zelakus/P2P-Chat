using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;

namespace Lobby
{
    class Program
    {
        static void Main(string[] args)
        {
            var lobby = new LobbyService();
            while (true)
            {
                if (Console.ReadLine() == "exit")
                    break;
            }
            lobby.Close();
        }
    }
}
