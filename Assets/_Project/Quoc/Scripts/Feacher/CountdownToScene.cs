using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CountdownToScene : MonoBehaviour
{
    public float countdownTime = 5f;   
    public string sceneName;           
    public Text countdownText;         

    private float currentTime;

    void Start()
    {
        currentTime = countdownTime;
    }

    void Update()
    {
        currentTime -= Time.deltaTime;

        if (countdownText != null)
        {
            countdownText.text = Mathf.Ceil(currentTime).ToString();
        }

        if (currentTime <= 0)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}