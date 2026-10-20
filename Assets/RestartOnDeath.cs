using UnityEngine;
using UnityEngine.SceneManagement;

/// Put on any object in the scene (for example the HUD object). When the player dies it shows
/// "Press ENTER to play again" and reloads the scene (back to the lobby) when ENTER is pressed.
public class RestartOnDeath : MonoBehaviour
{
    Health playerHealth;
    GUIStyle style;

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerHealth = p.GetComponent<Health>();
        if (playerHealth == null)
            Debug.LogWarning("RestartOnDeath: no Player with a Health component was found.");
    }

    void Update()
    {
        if (playerHealth == null || !playerHealth.IsDead) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Health.PlayerKills = 0;   // the kill counter is static, so reset it by hand
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    void OnGUI()
    {
        if (playerHealth == null || !playerHealth.IsDead) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
        }
        GUI.Label(new Rect(0, Screen.height * 0.62f, Screen.width, 50), "Press ENTER to play again", style);
    }
}
