using UnityEngine;
using UnityEngine.Playables;

public class SkipTimelineByKey : MonoBehaviour
{
    public PlayableDirector timeline;
    public GameObject cutsceneObject; 
    public KeyCode skipKey = KeyCode.E;

    void Update()
    {
        if (Input.GetKeyDown(skipKey))
        {
            timeline.Stop();

            if (cutsceneObject != null)
            {
                cutsceneObject.SetActive(false);
            }
        }
    }
}