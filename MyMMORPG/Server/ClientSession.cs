using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Shared;

namespace Server
{
    public class ClientSession
    {
        public int SessionId { get; }
        public int CurrentZone { get; set; } = 1;
        public float X { get; set; }
        public float Y { get; set; }
        public float DirX { get; set; }
        public float DirY { get; set; }
        public bool IsRunning { get; set; }

        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly GameServer _server;

        public ClientSession(int id, TcpClient client, GameServer server)
        {
            SessionId = id;
            _client = client;
            _client.NoDelay = true;
            _stream = client.GetStream();
            _server = server;

            var spawn = MapConfig.GetSpawnPos(1);
            X = spawn.x;
            Y = spawn.y;
        }

        public async Task StartReceiveLoopAsync()
        {
            byte[] headerBuffer = new byte[4];

            try
            {
                while (_client.Connected)
                {
                    if (!await ReadExactAsync(headerBuffer, 4)) break;

                    ushort totalLength = BitConverter.ToUInt16(headerBuffer, 0);
                    PacketId packetId = (PacketId)BitConverter.ToUInt16(headerBuffer, 2);

                    int bodyLength = totalLength - 4;
                    byte[] bodyBuffer = new byte[bodyLength];
                    if (bodyLength > 0 && !await ReadExactAsync(bodyBuffer, bodyLength)) break;

                    HandlePacket(packetId, bodyBuffer);
                }
            }
            catch { }
            finally
            {
                _server.RemoveSession(SessionId);
                _client.Close();
            }
        }

        private async Task<bool> ReadExactAsync(byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = await _stream.ReadAsync(buffer, offset, count - offset);
                if (read == 0) return false;
                offset += read;
            }
            return true;
        }

        private void HandlePacket(PacketId id, byte[] body)
        {
            using (MemoryStream ms = new MemoryStream(body))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                if (id == PacketId.C_Move)
                {
                    var pkt = new C_MovePacket();
                    pkt.Deserialize(reader);
                    X = pkt.X;
                    Y = pkt.Y;
                    DirX = pkt.DirX;
                    DirY = pkt.DirY;
                    IsRunning = pkt.IsRunning;
                }
                else if (id == PacketId.C_Interact)
                {
                    _server.TryUsePortal(this);
                }
            }
        }

        public async Task SendAsync(byte[] data)
        {
            try
            {
                if (_client.Connected)
                    await _stream.WriteAsync(data, 0, data.Length);
            }
            catch { }
        }
    }
}