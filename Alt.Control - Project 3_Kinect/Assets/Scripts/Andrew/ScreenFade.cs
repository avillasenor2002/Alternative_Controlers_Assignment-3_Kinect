using UnityEngine;
using UnityEngine.UI;

public class ScreenFade : MonoBehaviour
{
    [Header("Assign the fade Image here (must be a UI Image)")]
    public Image fadeImage;

    public float fadeSpeed = 2f;
    bool fadeOut = false;

    public void FadeOut()
    {
        fadeOut = true;
    }

    private void Update()
    {
        if (fadeOut && fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = Mathf.MoveTowards(c.a, 1f, fadeSpeed * Time.deltaTime);
            fadeImage.color = c;
        }
    }
}
