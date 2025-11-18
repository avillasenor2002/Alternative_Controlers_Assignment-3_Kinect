using UnityEngine;
using TMPro;

public class TimeCounter : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI currentText;   // Shows the live counter
    public TextMeshProUGUI bestText;      // Shows the saved high score

    [Header("Speed Settings")]
    public float startingRate = 5f;       // Starting speed (feet per second)
    public float acceleration = 0.5f;     // How much faster it gets every second
    public float multiplier = 1f;         // Extra multiplier (powerups, boosts, etc.)

    private float currentRate;            // Internal speed that increases over time
    private float currentValue = 0f;      // Current counter
    private float bestValue = 0f;         // Saved highest counter

    void Start()
    {
        // Set starting rate
        currentRate = startingRate;

        // Load best score from PlayerPrefs
        bestValue = PlayerPrefs.GetFloat("BestFeet", 0f);

        UpdateBestUI();
    }

    void Update()
    {
        // Increase speed continuously over time
        currentRate += acceleration * Time.deltaTime;

        // Count upward
        currentValue += currentRate * multiplier * Time.deltaTime;

        // Update UI text
        currentText.text = Mathf.FloorToInt(currentValue).ToString() + " ft";

        // Check for new high score
        if (currentValue > bestValue)
        {
            bestValue = currentValue;

            PlayerPrefs.SetFloat("BestFeet", bestValue);
            PlayerPrefs.Save();

            UpdateBestUI();
        }
    }

    void UpdateBestUI()
    {
        bestText.text = "Best: " + Mathf.FloorToInt(bestValue) + " ft";
    }
}
