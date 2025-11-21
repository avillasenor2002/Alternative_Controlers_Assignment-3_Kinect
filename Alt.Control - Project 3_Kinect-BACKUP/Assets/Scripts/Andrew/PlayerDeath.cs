using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerDeath : MonoBehaviour
{
    public CameraShake cameraShake;
    public ScreenFade screenFade;
    public float restartDelay = 1.2f;
    public float playerhealth = 3;

    private bool hasDied = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!hasDied && other.CompareTag("obsticle") && playerhealth <= 0)
        {
            hasDied = true;
            StartCoroutine(HandleDeathSequence());
        }
        else if (!hasDied && other.CompareTag("obsticle") && playerhealth >= 0)
        {
            playerhealth -= 1;
            cameraShake.Shake(0.5f, 0.5f);  // Duration, intensity
        }
    }

    private IEnumerator HandleDeathSequence()
    {
        cameraShake.Shake(1.0f, 1.0f);  // Duration, intensity
        yield return new WaitForSeconds(0.2f);

        screenFade.FadeOut();

        yield return new WaitForSeconds(restartDelay);

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}