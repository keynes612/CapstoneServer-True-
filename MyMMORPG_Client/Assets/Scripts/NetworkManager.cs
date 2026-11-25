using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using Shared;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    [Header("Server Settings")]
    public string serverIp = "127.0.0.1";
    public int serverPort = 7777;

    [Header("Prefabs")]
    public GameObject localPlayerPrefab;
    public GameObject remotePlayerPrefab;

    private TcpClient _client;
    private NetworkStream _stream;
    private Thread _receiveThread;
    private volatile bool _isRunning;

    private readonly ConcurrentQueue<Packet> _packetQueue = new ConcurrentQueue<Packet>();
    private GameObject _localPlayerObj;
    private readonly Dictionary<int, RemotePlayer> _remotePlayers = new Dictionary<int, RemotePlayer>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        ConnectToServer();
    }

    public void ConnectToServer()
    {
        try
        {
            _client = new TcpClient { NoDelay = true };
            _client.Connect(serverIp, serverPort);
            _stream = _client.GetStream();
            _isRunning = true;

            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            _receiveThread.Start();
            Debug.Log("서버 연결 성공!");
        }
        catch (Exception e)
        {
            Debug.LogError($"서버 연결 실패: {e.Message}");
        }
    }

    public void SendPacket(Packet packet)
    {
        if (_stream == null || !_client.Connected) return;
        byte[] data = packet.Serialize();
        _stream.Write(data, 0, data.Length);
    }

    private void ReceiveLoop()
    {
        byte[] headerBuffer = new byte[4];
        while (_isRunning && _client.Connected)
        {
            try
            {
                if (!ReadExact(headerBuffer, 4)) break;
                ushort totalLength = BitConverter.ToUInt16(headerBuffer, 0);
                PacketId packetId = (PacketId)BitConverter.ToUInt16(headerBuffer, 2);

                int bodyLength = totalLength - 4;
                byte[] bodyBuffer = new byte[bodyLength];
                if (bodyLength > 0 && !ReadExact(bodyBuffer, bodyLength)) break;

                EnqueuePacket(packetId, bodyBuffer);
            }
            catch { break; }
        }
    }

    private bool ReadExact(byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = _stream.Read(buffer, offset, count - offset);
            if (read == 0) return false;
            offset += read;
        }
        return true;
    }

    private void EnqueuePacket(PacketId id, byte[] body)
    {
        using (MemoryStream ms = new MemoryStream(body))
        using (BinaryReader reader = new BinaryReader(ms))
        {
            Packet pkt = id switch
            {
                PacketId.S_EnterGame => new S_EnterGamePacket(),
                PacketId.S_WorldSnapshot => new S_WorldSnapshotPacket(),
                PacketId.S_ZoneChange => new S_ZoneChangePacket(),
                PacketId.S_PlayerLeft => new S_PlayerLeftPacket(),
                _ => null
            };

            if (pkt != null)
            {
                pkt.Deserialize(reader);
                _packetQueue.Enqueue(pkt);
            }
        }
    }

    private void Update()
    {
        while (_packetQueue.TryDequeue(out Packet packet))
        {
            switch (packet)
            {
                case S_EnterGamePacket enter:
                    _localPlayerObj = Instantiate(localPlayerPrefab, new Vector3(enter.X, enter.Y, 0), Quaternion.identity);
                    Debug.Log($"게임 입장 완료! ID: {enter.PlayerId}, Zone: {enter.Zone}");
                    break;

                case S_WorldSnapshotPacket snapshot:
                    foreach (var info in snapshot.Players)
                    {
                        if (!_remotePlayers.TryGetValue(info.PlayerId, out RemotePlayer remote))
                        {
                            var go = Instantiate(remotePlayerPrefab, new Vector3(info.X, info.Y, 0), Quaternion.identity);
                            remote = go.GetComponent<RemotePlayer>();
                            _remotePlayers.Add(info.PlayerId, remote);
                        }
                        remote.UpdateState(info);
                    }
                    break;

                case S_ZoneChangePacket zoneChange:
                    if (_localPlayerObj != null)
                    {
                        _localPlayerObj.transform.position = new Vector3(zoneChange.NewX, zoneChange.NewY, 0);
                        // 이전 맵의 타 유저 오브젝트 제거
                        foreach (var kvp in _remotePlayers) Destroy(kvp.Value.gameObject);
                        _remotePlayers.Clear();
                        Debug.Log($"구역 이동 완료 -> Zone {zoneChange.NewZone}");
                    }
                    break;

                case S_PlayerLeftPacket left:
                    if (_remotePlayers.TryGetValue(left.PlayerId, out RemotePlayer leftObj))
                    {
                        Destroy(leftObj.gameObject);
                        _remotePlayers.Remove(left.PlayerId);
                    }
                    break;
            }
        }
    }

    private void OnApplicationQuit()
    {
        _isRunning = false;
        _stream?.Close();
        _client?.Close();
    }
}