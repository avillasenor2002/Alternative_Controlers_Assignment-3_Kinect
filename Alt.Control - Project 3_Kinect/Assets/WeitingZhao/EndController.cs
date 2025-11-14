using UnityEngine;
using UnityEngine.SceneManagement;

public class EndController : MonoBehaviour
{
    private void Update()
    {

        if (Input.GetKeyDown(KeyCode.Space))
        {
            SceneManager.LoadScene(0);   
        }
    }
}
