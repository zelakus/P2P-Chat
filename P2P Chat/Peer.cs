using System;
using System.Threading.Tasks;
using System.IO;
using Common;
using System.Net;

namespace P2P_Chat
{
    class Peer : Listener
    {
        private static readonly IPEndPoint _lobby = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 27091);
        public string Username { get; private set; }

        public Peer(string username) : base()
        {
            Username = username;

            //Send presence info to lobby
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(username);
                var data = stream.GetBuffer();
                Array.Resize(ref data, (int)stream.Position);
                Send(_lobby, Opcode.PresenceInf, data);
            }
        }

        string peerNickname;
        IPEndPoint peer = null;
        public void SendMessage(string msg)
        {
            if (peer == null)
                return;

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(msg);
                var data = stream.GetBuffer();
                Array.Resize(ref data, (int)stream.Position);
                Send(peer, Opcode.Message, data);
            }
        }

        public void ReceivedMessage(string msg)
        {
            Console.WriteLine($"\r[{peerNickname}]: {msg}");
            Console.Write("> ");
        }

        async Task Hello()
        {
            for (int i = 0;i<5;i++)
            {
                Send(peer, Opcode.Hello, new byte[0]);
                await Task.Delay(150);
            }
        }

        public override void Handle(IPEndPoint sender, Opcode opcode, MemoryStream stream)
        {
            var binary = new BinaryReader(stream);

            switch (opcode)
            {
                case Opcode.LobbyInf:
                    {
                        if (binary.ReadBoolean())
                        {
                            peerNickname = binary.ReadString();
                            Program.Log($"\rFound `{peerNickname}` on lobby, trying to connect.");

                            //Read peer endpoint
                            var ip = binary.ReadBytes(4);
                            var port = binary.ReadInt32();
                            peer = new IPEndPoint(new IPAddress(ip), port);

                            Program.Log($"Connecting to {peer.ToString()}");

                            Task.Run(Hello);
                        }
                        else
                            Program.Log("\rNobody on lobby, waiting...");

                        break;
                    }
                case Opcode.ConnectPeerInf:
                    {
                        peerNickname = binary.ReadString();

                        //Read peer endpoint
                        var ip = binary.ReadBytes(4);
                        var port = binary.ReadInt32();
                        peer = new IPEndPoint(new IPAddress(ip), port);

                        Program.Log($"\rConnection from {peer.ToString()}");

                        Task.Run(Hello);

                        //Set peer as heartbeat target, so that we don't lose connection
                        HeartbeatTarget = peer;
                        break;
                    }
                case Opcode.Message:
                    ReceivedMessage(binary.ReadString());
                    break;
            }

            //Dispose stream
            binary.Dispose();
            stream.Dispose();
        }
    }
}
