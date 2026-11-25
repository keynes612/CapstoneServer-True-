using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared;

namespace Server
{
    public class GameServer
    {
        private readonly TcpListener _listener;
        private readonly ConcurrentDictionary<int, ClientSession> _sessions = new();
        private int _nextSessionId = 1;
        private long _tickCount = 0;

        public GameServer(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
        }

        public async Task StartAsync()
        {
            _listener.Start();
            Console.WriteLine("MMORPG 서버 시작 (Port: 7777, Base Tick: 20Hz)...");

            // 1. 월드 틱 루프 (백그라운드 태스크)
            _ = Task.Run(WorldTickLoopAsync);

            // 2. 신규 클라이언트 접속 수락 루프
            while (true)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                int id = Interlocked.Increment(ref _nextSessionId);
                var session = new ClientSession(id, client, this);

                _sessions.TryAdd(id, session);
                Console.WriteLine($"[접속] Player {id} (총 인원: {_sessions.Count}명)");

                // 접속 즉시 본인 ID와 1번 맵 초기 좌표 전송
                var enterPkt = new S_EnterGamePacket
                {
                    PlayerId = id,
                    Zone = session.CurrentZone,
                    X = session.X,
                    Y = session.Y
                };
                _ = session.SendAsync(enterPkt.Serialize());

                _ = Task.Run(() => session.StartReceiveLoopAsync());
            }
        }

        // 50ms(20Hz) 간격으로 실행되는 가변 틱레이트(NetLOD) 브로드캐스트 루프
        private async Task WorldTickLoopAsync()
        {
            const int tickIntervalMs = 50; // 20Hz
            var sw = new Stopwatch();

            while (true)
            {
                sw.Restart();
                _tickCount++;
                bool isMidRangeTick = (_tickCount % 4 == 0); // 4틱마다 한 번 = 5Hz

                var allSessions = _sessions.Values.ToArray();

                // 구역(1~5)별로 유저를 그룹화 (다른 맵 유저와의 거리 계산 자체를 생략)
                for (int zone = 1; zone <= MapConfig.TotalZones; zone++)
                {
                    var zonePlayers = allSessions.Where(s => s.CurrentZone == zone).ToArray();
                    if (zonePlayers.Length <= 1) continue; // 혼자 있으면 동기화 불필요

                    foreach (var receiver in zonePlayers)
                    {
                        var snapshot = new S_WorldSnapshotPacket();

                        foreach (var target in zonePlayers)
                        {
                            if (receiver.SessionId == target.SessionId) continue;

                            float dx = receiver.X - target.X;
                            float dy = receiver.Y - target.Y;
                            float distSq = dx * dx + dy * dy;

                            // 1) 근거리 (20Hz): 매 틱 전송
                            // 2) 중거리 (5Hz): isMidRangeTick 일 때만 전송
                            // 3) 원거리 (0Hz): 전송 안 함
                            if (distSq <= MapConfig.NearDistSq || (distSq <= MapConfig.MidDistSq && isMidRangeTick))
                            {
                                snapshot.Players.Add(new PlayerStateInfo
                                {
                                    PlayerId = target.SessionId,
                                    X = target.X,
                                    Y = target.Y,
                                    DirX = target.DirX,
                                    DirY = target.DirY,
                                    IsRunning = target.IsRunning
                                });
                            }
                        }

                        if (snapshot.Players.Count > 0)
                        {
                            _ = receiver.SendAsync(snapshot.Serialize());
                        }
                    }
                }

                sw.Stop();
                int delay = tickIntervalMs - (int)sw.ElapsedMilliseconds;
                if (delay > 0) await Task.Delay(delay);
            }
        }

        // 포탈 상호작용 검증 및 순환 이동 (1 -> 2 -> 3 -> 4 -> 5 -> 1)
        public void TryUsePortal(ClientSession session)
        {
            var portalPos = MapConfig.GetPortalPos(session.CurrentZone);
            float dx = session.X - portalPos.x;
            float dy = session.Y - portalPos.y;

            if (dx * dx + dy * dy <= MapConfig.PortalInteractRangeSq)
            {
                int nextZone = (session.CurrentZone % MapConfig.TotalZones) + 1;
                var nextSpawn = MapConfig.GetSpawnPos(nextZone);

                session.CurrentZone = nextZone;
                session.X = nextSpawn.x;
                session.Y = nextSpawn.y;

                Console.WriteLine($"[포탈 이동] Player {session.SessionId}: Zone {nextZone} ({session.X}, {session.Y})");

                var zonePkt = new S_ZoneChangePacket
                {
                    PlayerId = session.SessionId,
                    NewZone = nextZone,
                    NewX = session.X,
                    NewY = session.Y
                };
                _ = session.SendAsync(zonePkt.Serialize());
            }
        }

        public void RemoveSession(int id)
        {
            if (_sessions.TryRemove(id, out _))
            {
                Console.WriteLine($"[퇴장] Player {id} (총 인원: {_sessions.Count}명)");
                var leftPkt = new S_PlayerLeftPacket { PlayerId = id };
                byte[] data = leftPkt.Serialize();
                foreach (var s in _sessions.Values) _ = s.SendAsync(data);
            }
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var server = new GameServer(7777);
            await server.StartAsync();
        }
    }
}