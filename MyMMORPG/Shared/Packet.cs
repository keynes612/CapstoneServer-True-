using System;
using System.Collections.Generic;
using System.IO;

namespace Shared
{
    public enum PacketId : ushort
    {
        S_EnterGame = 1,      // 서버 -> 클라: 내 PlayerId 및 초기 위치 부여
        C_Move = 2,           // 클라 -> 서버: 내 위치 및 속도 전송
        S_WorldSnapshot = 3,  // 서버 -> 클라: 거리별 필터링된 주변 유저 목록 일괄 전송
        C_Interact = 4,       // 클라 -> 서버: C키 상호작용(포탈) 요청
        S_ZoneChange = 5,     // 서버 -> 클라: 구역 이동 확정 및 새 좌표
        S_PlayerLeft = 6,     // 서버 -> 클라: 접속 종료 유저 제거
        S_BroadcastMove = 7   // 서버 -> 클라: 개별 플레이어 이동 브로드캐스트
    }

    public abstract class Packet
    {
        public abstract PacketId Id { get; }
        public abstract byte[] Serialize();
        public abstract void Deserialize(BinaryReader reader);

        protected byte[] WrapHeader(Action<BinaryWriter> writeBody)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write((ushort)0);
                writer.Write((ushort)Id);
                writeBody(writer);

                ushort totalLength = (ushort)ms.Position;
                ms.Position = 0;
                writer.Write(totalLength);

                return ms.ToArray();
            }
        }
    }

    public class S_EnterGamePacket : Packet
    {
        public override PacketId Id => PacketId.S_EnterGame;
        public int PlayerId;
        public int Zone;
        public float X;
        public float Y;

        public override byte[] Serialize() => WrapHeader(w =>
        {
            w.Write(PlayerId);
            w.Write(Zone);
            w.Write(X);
            w.Write(Y);
        });

        public override void Deserialize(BinaryReader r)
        {
            PlayerId = r.ReadInt32();
            Zone = r.ReadInt32();
            X = r.ReadSingle();
            Y = r.ReadSingle();
        }
    }

    public class C_MovePacket : Packet
    {
        public override PacketId Id => PacketId.C_Move;
        public float X;
        public float Y;
        public float DirX;
        public float DirY;
        public bool IsRunning;

        public override byte[] Serialize() => WrapHeader(w =>
        {
            w.Write(X);
            w.Write(Y);
            w.Write(DirX);
            w.Write(DirY);
            w.Write(IsRunning);
        });

        public override void Deserialize(BinaryReader r)
        {
            X = r.ReadSingle();
            Y = r.ReadSingle();
            DirX = r.ReadSingle();
            DirY = r.ReadSingle();
            IsRunning = r.ReadBoolean();
        }
    }

    public struct PlayerStateInfo
    {
        public int PlayerId;
        public float X;
        public float Y;
        public float DirX;
        public float DirY;
        public bool IsRunning;
    }

    // 한 틱에 수집된 주변 유저 상태를 배열로 한 번에 전송 (패킷 헤더 오버헤드 최소화)
    public class S_WorldSnapshotPacket : Packet
    {
        public override PacketId Id => PacketId.S_WorldSnapshot;
        public List<PlayerStateInfo> Players = new List<PlayerStateInfo>();

        public override byte[] Serialize() => WrapHeader(w =>
        {
            w.Write((ushort)Players.Count);
            foreach (var p in Players)
            {
                w.Write(p.PlayerId);
                w.Write(p.X);
                w.Write(p.Y);
                w.Write(p.DirX);
                w.Write(p.DirY);
                w.Write(p.IsRunning);
            }
        });

        public override void Deserialize(BinaryReader r)
        {
            Players.Clear();
            ushort count = r.ReadUInt16();
            for (int i = 0; i < count; i++)
            {
                Players.Add(new PlayerStateInfo
                {
                    PlayerId = r.ReadInt32(),
                    X = r.ReadSingle(),
                    Y = r.ReadSingle(),
                    DirX = r.ReadSingle(),
                    DirY = r.ReadSingle(),
                    IsRunning = r.ReadBoolean()
                });
            }
        }
    }

    public class C_InteractPacket : Packet
    {
        public override PacketId Id => PacketId.C_Interact;
        public byte InteractType; // 1: Portal

        public override byte[] Serialize() => WrapHeader(w =>
        {
            w.Write(InteractType);
        });

        public override void Deserialize(BinaryReader r)
        {
            InteractType = r.ReadByte();
        }
    }

    public class S_ZoneChangePacket : Packet
    {
        public override PacketId Id => PacketId.S_ZoneChange;
        public int PlayerId;
        public int NewZone;
        public float NewX;
        public float NewY;

        public override byte[] Serialize() => WrapHeader(w =>
        {
            w.Write(PlayerId);
            w.Write(NewZone);
            w.Write(NewX);
            w.Write(NewY);
        });

        public override void Deserialize(BinaryReader r)
        {
            PlayerId = r.ReadInt32();
            NewZone = r.ReadInt32();
            NewX = r.ReadSingle();
            NewY = r.ReadSingle();
        }
    }

    public class S_PlayerLeftPacket : Packet
    {
        public override PacketId Id => PacketId.S_PlayerLeft;
        public int PlayerId;

        public override byte[] Serialize() => WrapHeader(w => w.Write(PlayerId));
        public override void Deserialize(BinaryReader r) => PlayerId = r.ReadInt32();
    }

    public class S_BroadcastMovePacket : Packet
    {
        public override PacketId Id => PacketId.S_BroadcastMove;
        public int PlayerId;
        public float X;
        public float Y;
        public float DirX;
        public float DirY;
        public bool IsRunning;

        public override byte[] Serialize() => WrapHeader(w =>
        {
            w.Write(PlayerId);
            w.Write(X);
            w.Write(Y);
            w.Write(DirX);
            w.Write(DirY);
            w.Write(IsRunning);
        });

        public override void Deserialize(BinaryReader r)
        {
            PlayerId = r.ReadInt32();
            X = r.ReadSingle();
            Y = r.ReadSingle();
            DirX = r.ReadSingle();
            DirY = r.ReadSingle();
            IsRunning = r.ReadBoolean();
        }
    }
}