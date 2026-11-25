namespace Shared
{
    public static class MapConfig
    {
        public const int TotalZones = 5;
        public const float ZoneOffsetX = 1000f; // 1번: 0, 2번: 1000, 3번: 2000, 4번: 3000, 5번: 4000

        // NetLOD 거리 기준 (제곱 거리로 비교해 Math.Sqrt 연산 절약)
        public const float NearDistSq = 15f * 15f; // 반경 15m 이내: 근거리 (20Hz)
        public const float MidDistSq = 40f * 40f;  // 반경 40m 이내: 중거리 (5Hz), 초과 시 원거리 (0Hz)

        public const float PortalInteractRangeSq = 2.5f * 2.5f; // 포탈 상호작용 허용 반경 2.5m

        // 각 구역의 기본 스폰 위치 (입구)
        public static (float x, float y) GetSpawnPos(int zone)
        {
            float baseX = (zone - 1) * ZoneOffsetX;
            return (baseX - 8f, 0f); // 맵 왼쪽 입구
        }

        // 각 구역의 다음 맵으로 가는 출구 포탈 위치
        public static (float x, float y) GetPortalPos(int zone)
        {
            float baseX = (zone - 1) * ZoneOffsetX;
            return (baseX + 8f, 0f); // 맵 오른쪽 출구
        }
    }
}