using CutsceneEngine;
using UnityEngine;

public class StartCutsceneButton : MonoBehaviour
{
    public Cutscene cutscene;

    public void StartScene()
    {
        cutscene.Play();
    }
}