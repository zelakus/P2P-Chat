using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Common;

namespace Lobby
{
    struct User
    {
        public string username;
        public IPEndPoint endpoint;
    }

    class LobbyService : Listener
    {
        User waiting;

        public LobbyService(): base(27091) //Random port
        {
            waiting.username = null;
            waiting.endpoint = null;
        }

        public override void Handle(IPEndPoint sender, Opcode opcode, MemoryStream stream)
        {
            var binary = new BinaryReader(stream);
            
            switch (opcode)
            {
                case Opcode.PresenceInf:
                    {
                        SendLobbyInfo(sender);

                        if (waiting.endpoint == null)
                        {
                            waiting.username = binary.ReadString();
                            waiting.endpoint = sender;
                            Console.WriteLine($"[!]: `{waiting.username}` is waiting on lobby.");
                        }
                        else
                        {
                            ConnectPeers(sender, binary.ReadString());
                        }
                        break;
                    }
            }

            //Dispose stream
            binary.Dispose();
            stream.Dispose();
        }

        private void SendLobbyInfo(IPEndPoint target)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                if (waiting.endpoint != null)
                {
                    writer.Write(true);
                    writer.Write(waiting.username);
                }
                else
                    writer.Write(false);

                var data = stream.GetBuffer();
                Array.Resize(ref data, (int)stream.Position);
                Send(target, Opcode.LobbyInf, data);
            }
        }

        private async Task ConnectPeers(IPEndPoint sender, string name)
        {
            await Task.Delay(500);
            Console.WriteLine($"[!]: Connecting `{name}` to `{waiting.username}`.");
            //Send waiter
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(name);
                writer.Write(sender.Address.GetAddressBytes(), 0, 4);
                writer.Write(sender.Port);

                var data = stream.GetBuffer();
                Array.Resize(ref data, (int)stream.Position);
                Send(waiting.endpoint, Opcode.ConnectPeerInf, data);
            }

            //Send newcomer
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(waiting.username);
                writer.Write(waiting.endpoint.Address.GetAddressBytes(), 0, 4);
                writer.Write(waiting.endpoint.Port);

                var data = stream.GetBuffer();
                Array.Resize(ref data, (int)stream.Position);
                Send(sender, Opcode.ConnectPeerInf, data);
            }

            //Reset lobby
            waiting.username = null;
            waiting.endpoint = null;

            Console.WriteLine("[!]: Lobby is empty now.");
        }
    }
}
