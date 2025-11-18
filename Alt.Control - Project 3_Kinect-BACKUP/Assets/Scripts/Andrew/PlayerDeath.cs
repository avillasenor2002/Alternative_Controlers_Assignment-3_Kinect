using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerDeath : MonoBehaviour
{
    public CameraShake cameraShake;
    public ScreenFade screenFade;
    public float restartDelay = 1.2f;

    private bool hasDied = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!hasDied && other.CompareTag("obsticle"))
        {
            hasDied = true;
            StartCoroutine(HandleDeathSequence());
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