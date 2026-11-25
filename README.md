# CapstoneServer

MMORPG 서버 구조와 상태 동기화 최적화를 실험하기 위한 캡스톤 시제품입니다. 직접 만든 .NET TCP 서버와 Unity 2D 클라이언트로 구성됩니다. 대규모 상용 MMORPG를 구현한 것은 아닙니다.

## 폴더 구조

| 폴더 | 내용 |
|---|---|
| `MyMMORPG/Server` | .NET 10 콘솔 TCP 서버 (포트 7777, 20Hz 월드 틱) |
| `MyMMORPG/Shared` | 서버·클라이언트 공용 패킷과 맵 설정 (netstandard2.1) |
| `MyMMORPG_Client` | Unity 6000.3.10f1 2D URP 클라이언트 |
| `Docs/` | 프로젝트 문서 |

## 실행 방법

### 준비

- .NET SDK 10
- Unity 6000.3.10f1

### 서버

```bash
dotnet run --project MyMMORPG/Server
```

`MMORPG 서버 시작 (Port: 7777, Base Tick: 20Hz)...`가 출력되면 접속을 기다리는 상태입니다.

### 클라이언트

1. Unity Hub에서 `MyMMORPG_Client` 폴더를 엽니다.
2. `Assets/Scenes/SampleScene`을 열고 Play를 누릅니다.
3. 기본 접속 주소는 `127.0.0.1:7777`입니다. 씬에 있는 `NetworkManager` 오브젝트의 `serverIp`·`serverPort`에서 바꿀 수 있습니다.

여러 클라이언트로 확인하려면 클라이언트를 빌드해 여러 개 실행합니다.

### 공용 코드(Shared)를 바꿨을 때

클라이언트는 `Assets/Plugins/Shared.dll`을 사용합니다. `MyMMORPG/Shared`를 수정했다면 다시 빌드한 DLL로 교체해야 클라이언트에 반영됩니다.

```bash
dotnet build MyMMORPG/Shared
```

빌드 결과(`MyMMORPG/Shared/bin/<구성>/netstandard2.1/Shared.dll`)를 `MyMMORPG_Client/Assets/Plugins/Shared.dll`에 덮어씁니다. `.meta` 파일은 그대로 둡니다.

## 조작

| 키 | 동작 |
|---|---|
| 방향키 | 이동 |
| Z (누르고 있기) | 달리기 |
| C | 포탈 이용 (포탈 2.5m 이내) |

## 현재 구현

- TCP 접속, 접속 시 플레이어 ID와 시작 위치 지급
- 5개 구역. 포탈로 1→2→3→4→5→1 순서로 이동
- 구역별 위치 동기화와 NetLOD: 15m 이내 20Hz, 15~40m 5Hz, 40m 밖은 전송 안 함
- 다른 플레이어 표시 위치 보간과 추측 이동(dead reckoning)
- 퇴장 알림

## 아직 없는 것과 알려진 한계

- 위치를 클라이언트가 정하고 서버는 검증 없이 받습니다.
- 전투·몬스터·체력, 계측·로그, 자동 클라이언트(봇)가 없습니다.
- 40m 밖으로 나간 상대나 다른 구역으로 이동한 상대가 화면에 남을 수 있습니다.
- 성능 측정 결과는 아직 없습니다.

## 문서

- [서버 최적화 판정 기준](Docs/OptimizationCriteria.md)
- [작업 지침 (AGENTS.md)](AGENTS.md)

## 브랜치

작업은 `KDH` 브랜치에서 합니다.

## 검증 기록

- 2026-11-25: `dotnet build MyMMORPG/Server/Server.csproj` 성공 (경고 0, 오류 0). 서버 실행과 클라이언트 접속은 이 문서 작성 때 확인하지 않았습니다.
