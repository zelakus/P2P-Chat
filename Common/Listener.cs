using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Linq;
using System.Threading;

namespace Common
{
    public class Listener : IDisposable
    {
        private const int MAX_QUEUE_SIZE = 200;
        struct PacketInf
        {
            public long senderId;
            public uint packetId;
            public IPEndPoint target;
            public Opcode opcode;
            public byte[] data;
        }
        
        private readonly UdpClient _client;
        private readonly IdGenerator<long> _dictionaryId = new IdGenerator<long>();
        private readonly IdCounter _packetId = new IdCounter(0xFFFF);
        private readonly ConcurrentDictionary<long, PacketInf> _lastPackets = new ConcurrentDictionary<long, PacketInf>();
        private readonly ConcurrentDictionary<long, PacketInf> _resendQueue = new ConcurrentDictionary<long, PacketInf>();
        private Thread _resendThread;
        private Thread _heartbeatThread;

        public IPEndPoint HeartbeatTarget = null;

        public Listener()
        {
            _client = new UdpClient(0);
            Init();
        }

        public Listener(int port)
        {
            _client = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            Init();
        }

        private bool _closing = false;
        public void Close()
        {
            _closing = true;
            _resendThread.Abort();
            _heartbeatThread.Abort();
            _client.Close();
        }

        private void Init()
        {
            _resendThread = new Thread(Resend);
            _resendThread.Start();
            _heartbeatThread = new Thread(Beat);
            _heartbeatThread.Start();

            _client.AllowNatTraversal(true);
            _client.DontFragment = true;
            _client.BeginReceive(OnReceiveDatagram, null);
        }

        private void Beat()
        {
            while (true)
            {
                if (_closing)
                    break;

                if (HeartbeatTarget != null)
                    Send(HeartbeatTarget, Opcode.Heartbeat, new byte [0]);
                
                Thread.Sleep(500);
            }
        }

        private void Resend()
        {
            while (true)
            {
                if (_closing)
                    break;

                var queue = _resendQueue.Values.ToArray();
                foreach (var inf in queue)
                {
                    Send(inf.target, inf.opcode, inf.data, inf.packetId);
                }

                Thread.Sleep(200);
            }
        }

        private void Send(IPEndPoint target, Opcode opcode, byte[] data, uint id)
        {
            //Write packet inf
            MemoryStream ms = new MemoryStream(7);
            ms.Write(new byte[] { (byte)opcode }, 0, 1); //Opcode 
            ms.Write(BitConverter.GetBytes((ushort)data.Length), 0, 2); //length
            ms.Write(BitConverter.GetBytes(id), 0, 4); //packetId
            ms.Write(data, 0, data.Length); //data

            //Get & Resize buffer
            var buffer = ms.GetBuffer();
            Array.Resize(ref buffer, (int)ms.Position);
            ms.Dispose();

            //Send
            _client.Send(buffer, buffer.Length, target);
        }

        public void Send(IPEndPoint target, Opcode opcode, byte[] data)
        {
            if (_resendQueue.Count > MAX_QUEUE_SIZE)
                _resendQueue.TryRemove(_resendQueue.Keys.First(), out _);

            uint packetId = _packetId.Get();
            _resendQueue.TryAdd(_dictionaryId.Get(), new PacketInf()
            {
                target = target,
                opcode = opcode,
                data = data,
                packetId = packetId,
                senderId = target.ToLong()
            });
            //Add to resend list
            Send(target, opcode, data, packetId);
        }

        private void Reply(IPEndPoint target, uint id)
        {
            //Write packet inf
            MemoryStream ms = new MemoryStream(7);
            ms.Write(new byte[] { 1 << 7 }, 0, 1); //Reply | Opcode 
            ms.Write(BitConverter.GetBytes((ushort)0), 0, 2); //length
            ms.Write(BitConverter.GetBytes(id), 0, 2); //packetId

            //Get & Resize buffer
            var buffer = ms.GetBuffer();
            if (buffer.Length != 7)
                Array.Resize(ref buffer, 7);

            //Send
            _client.Send(buffer, 7, target);
        }

        private void HasReply(long senderId, uint packetId)
        {
            var queue = _resendQueue.ToArray();
            foreach (var inf in queue)
                if (inf.Value.senderId == senderId && inf.Value.packetId == packetId)
                {
                    _resendQueue.TryRemove(inf.Key, out _);
                    return;
                }
        }

        //packet: { [IsReply | Opcode:1], [Length:2], [PacketId:4] }
        private void OnReceiveDatagram(IAsyncResult result)
        {
            try
            {
                IPEndPoint sender = null;
                var rawData = _client.EndReceive(result, ref sender);
                if (rawData.Length >= 7)
                {
                    //Get length and resize raw data
                    var length = BitConverter.ToUInt16(rawData, 1);
                    Array.Resize(ref rawData, length + 7);

                    var packetId = BitConverter.ToUInt32(rawData, 3);
                    var senderId = sender.ToLong();

                    //Is this a known message
                    bool ignore = false;
                    var queue = _lastPackets.Values.ToArray();
                    foreach (var message in queue)
                        if (message.senderId == senderId && message.packetId == packetId)
                        {
                            ignore = true;
                            break;
                        }

                    if (!ignore)
                    {
                        //Is this a reply
                        if (rawData[0] >> 7 == 1)
                        {
                            HasReply(senderId, packetId);
                        }
                        else
                        {
                            //Set as known
                            if (_lastPackets.Count > MAX_QUEUE_SIZE)
                                _lastPackets.TryRemove(_lastPackets.Keys.First(), out _);
                            _lastPackets.TryAdd(_dictionaryId.Get(), new PacketInf()
                            {
                                senderId = senderId,
                                packetId = packetId
                            });

                            //Reply as received
                            Reply(sender, packetId);

                            //Create stream for handler to process
                            var data = new MemoryStream(length);
                            data.Write(rawData, 7, length);
                            data.Seek(0, SeekOrigin.Begin);

                            Handle(sender, (Opcode)rawData[0], data);
                        }
                    }
                }
                else
                {
                    //Malformed datagram, expecting at least 7 bytes (opcode + message length + packetId)
                }
            }
            catch { }

            try
            {
                _client.BeginReceive(OnReceiveDatagram, null);
            }
            catch
            {
                _client.BeginReceive(OnReceiveDatagram, null);
            }
        }

        public virtual void Handle(IPEndPoint sender, Opcode opcode, MemoryStream stream)
        {

        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Close();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
