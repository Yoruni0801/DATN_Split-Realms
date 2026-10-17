using UnityEngine;

public class PlayButton : MonoBehaviour
{
    public BallController ball;

    public void Play()
    {
        ball.PlayBall();
    }
}