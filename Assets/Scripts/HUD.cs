using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;


public class HUD : MonoBehaviour
{
    [SerializeField] private TMP_Text livesText, scoreText, ammoText, distanceText, roleText, statusText;
    [SerializeField] private GameObject qtePanel;
    [SerializeField] private Slider qteSlider;

    private PlaneController plane;

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        NetworkManager nm = NetworkManager.Singleton;
        if (gm == null || nm == null || !nm.IsListening) return;

        if (plane == null) plane = FindFirstObjectByType<PlaneController>();

        bool isPilot = nm.IsHost;
        roleText.text = isPilot ? "PILOT: A/D move, hold SPACE fire"
                                : "GUNNER: mouse aim, hold LMB fire";

        livesText.text = $"Lives: {gm.planeLives.Value}";
        scoreText.text = $"Score: {gm.score.Value}";

        if (isPilot)
        {
            bool armed = plane != null && plane.frontGunsActive.Value;
            ammoText.text = armed ? "Front guns: ON" : "Front guns: OFF (grab a gun pickup)";
        }
        else
        {
            ammoText.text = $"Ammo: {gm.currentAmmo.Value}";
        }

        distanceText.text = $"Target: {gm.distance.Value}";

        bool bombing = gm.state.Value == GameState.Bombing;
        qtePanel.SetActive(bombing);
        if (bombing)
            qteSlider.value = gm.MarkerAt(nm.ServerTime.Time - gm.qteStartTime);

        switch (gm.state.Value)
        {
            case GameState.Waiting: statusText.text = "Waiting for second player..."; break;
            case GameState.Flying: statusText.text = ""; break;
            case GameState.Bombing:
                statusText.text = isPilot ? "HOLD STEADY! Gunner is bombing..."
                                          : "BOMBS AWAY! Press SPACE in the green!"; break;
            case GameState.GameOver: statusText.text = $"GAME OVER\nScore: {gm.score.Value}"; break;
        }
    }
}