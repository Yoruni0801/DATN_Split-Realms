using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[RequireComponent(typeof(RawImage))]
[RequireComponent(typeof(VideoPlayer))]
[ExecuteAlways]
public class UIVideoLoop : MonoBehaviour
{
    [Header("Video")]
    [SerializeField] private VideoClip videoClip;

    [Header("Settings")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool muteAudio = true;

    private RawImage rawImage;
    private VideoPlayer videoPlayer;
    private RenderTexture renderTexture;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
        videoPlayer = GetComponent<VideoPlayer>();

        SetupVideo();
    }

    private void Start()
    {
        if (playOnStart)
        {
            PlayVideo();
        }
    }

    private void SetupVideo()
    {
        renderTexture = new RenderTexture(1920, 1080, 0);
        renderTexture.Create();

        rawImage.texture = renderTexture;

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTexture;

        videoPlayer.audioOutputMode = muteAudio
            ? VideoAudioOutputMode.None
            : VideoAudioOutputMode.Direct;

        if (videoClip != null)
        {
            videoPlayer.clip = videoClip;
        }
    }

    public void PlayVideo()
    {
        if (videoPlayer.clip == null)
        {
            Debug.LogWarning("[UIVideoLoop] Chưa gắn Video Clip.");
            return;
        }

        videoPlayer.Stop();
        videoPlayer.time = 0;
        videoPlayer.Play();
    }

    public void StopVideo()
    {
        videoPlayer.Stop();
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
        }
    }
}
