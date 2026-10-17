using UnityEngine;
using UnityEngine.Playables;

[System.Serializable]
public class TimelineInputBehaviour : PlayableBehaviour
{
    [Header("Input")]
    public KeyCode requiredKey = KeyCode.Space;

    [Header("Action Object")]
    public GameObject actionObject;

    [Header("QTE UI - hiện khi Timeline đang chờ input")]
    public ExposedReference<GameObject> qteUI;

    [Header("Continue UI - hiện sau khi bấm đúng phím")]
    [Tooltip("Kéo GameObject UI muốn hiện sau khi người chơi bấm phím để chạy Timeline tiếp.")]
    public ExposedReference<GameObject> continueUI;

    private bool hasStarted;

    public override void OnBehaviourPlay(
        Playable playable,
        FrameData info)
    {
        // Tránh gọi nhiều lần
        if (hasStarted)
            return;

        hasStarted = true;

        PlayableDirector director =
            playable.GetGraph().GetResolver()
            as PlayableDirector;

        if (director == null)
        {
            Debug.LogError(
                "TimelineInputBehaviour: Không tìm thấy PlayableDirector."
            );

            return;
        }

        TimelineInputController controller =
            TimelineInputController.GetOrCreate();

        GameObject resolvedQteUI = qteUI.Resolve(director);
        GameObject resolvedContinueUI = continueUI.Resolve(director);

        controller.StartWaitingForInput(
            director,
            requiredKey,
            actionObject,
            resolvedQteUI,
            resolvedContinueUI
        );
    }
}
