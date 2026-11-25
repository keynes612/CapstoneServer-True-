using UnityEngine;

// 플레이어와 몬스터가 함께 쓰는 체력. 네트워크 동기화 없음 (클라이언트 로컬 테스트용).
public class CombatHealth : MonoBehaviour
{
    public enum Team { Player, Monster }
    [Header("Health")]
    public Team team;
    [Min(1)] public int maxHealth = 100;
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    [Header("Hit flash")]
    public SpriteRenderer body;
    [Min(0)] public float flashDuration = 0.12f;
    [Header("Health bar")]
    public bool showHealthBar = true;
    public Vector3 barOffset = new Vector3(0, 1.2f, 0);
    public Vector2 barSize = new Vector2(90, 12);

    Color normalColor;
    float flashUntil;
    bool flashing;

    void Awake()
    {
        CurrentHealth = maxHealth;
        if (body != null) normalColor = body.color;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        flashUntil = Time.time + flashDuration;
        flashing = true;
        SetColor(Color.white);
    }

    void Update()
    {
        if (flashing && Time.time >= flashUntil)
        {
            flashing = false;
            SetColor(normalColor);
        }
    }

    void SetColor(Color color)
    {
        if (body != null) body.color = color;
    }

    void OnGUI()
    {
        Camera camera = Camera.main;
        if (!showHealthBar || camera == null) return;
        Vector3 screen = camera.WorldToScreenPoint(transform.position + barOffset);
        if (screen.z <= 0) return;
        Rect rect = new Rect(screen.x - barSize.x / 2, Screen.height - screen.y, barSize.x, barSize.y);
        Color previous = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = team == Team.Player ? Color.cyan : Color.red;
        GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1,
            Mathf.Max(0, rect.width - 2) * CurrentHealth / Mathf.Max(1f, maxHealth),
            Mathf.Max(0, rect.height - 2)), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x, rect.y - 22, 130, 22),
            IsDead ? "DEAD" : $"{CurrentHealth} / {maxHealth}");
        GUI.color = previous;
    }
}
