using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

public class PlayerRevealMixer : PlayableBehaviour
{
    // ========================================================
    // REFERENCES
    // ========================================================

    private Transform player;

    private Rigidbody2D playerBody;

    private RigidbodyType2D originalBodyType;

    private float originalGravityScale;

    private bool playerPhysicsCached;

    private AnimationSpine playerAnimation;

    private bool cutsceneMovementActive;

    private Camera mainCamera;


    // ========================================================
    // RUNTIME OVERLAY
    // ========================================================

    private GameObject canvasObject;

    private GameObject overlayObject;

    private Image overlayImage;

    private Material material;


    private bool overlayCreated;

    private bool revealFinished;


    // ========================================================
    // SHADER IDs
    // ========================================================

    private static readonly int CenterID =
        Shader.PropertyToID("_Center");


    private static readonly int RadiusID =
        Shader.PropertyToID("_Radius");


    private static readonly int InsideColorID =
        Shader.PropertyToID("_InsideColor");


    private static readonly int InsideAlphaID =
        Shader.PropertyToID("_InsideAlpha");


    private static readonly int EdgeSoftnessID =
        Shader.PropertyToID("_EdgeSoftness");


    private static readonly int EdgeIntensityID =
        Shader.PropertyToID("_EdgeIntensity");


    // ========================================================
    // PROCESS FRAME
    // ========================================================

    public override void ProcessFrame(
        Playable playable,
        FrameData info,
        object playerData)
    {
        // ====================================================
        // PLAYER
        // ====================================================

        player =
            playerData as Transform;


        if (player == null)
        {
            RestorePlayerPhysics();
            CleanupOverlay();
            return;
        }


        // ====================================================
        // CAMERA
        // ====================================================

        if (mainCamera == null)
        {
            mainCamera =
                Camera.main;
        }


        if (mainCamera == null)
        {
            CleanupOverlay();
            return;
        }


        // ====================================================
        // ACTIVE CLIP
        // ====================================================

        PlayerRevealBehaviour activeBehaviour =
            null;


        float highestWeight = 0f;

        double activeTime = 0.0;

        double activeDuration = 0.0;


        int inputCount =
            playable.GetInputCount();


        for (int i = 0; i < inputCount; i++)
        {
            float weight =
                playable.GetInputWeight(i);


            if (weight <= 0f)
                continue;


            ScriptPlayable<PlayerRevealBehaviour> input =
                (ScriptPlayable<PlayerRevealBehaviour>)
                playable.GetInput(i);


            PlayerRevealBehaviour behaviour =
                input.GetBehaviour();


            if (weight > highestWeight)
            {
                highestWeight =
                    weight;


                activeBehaviour =
                    behaviour;


                activeTime =
                    input.GetTime();


                activeDuration =
                    input.GetDuration();
            }
        }


        // ====================================================
        // NO ACTIVE CLIP
        // ====================================================

        if (activeBehaviour == null)
        {
            RestorePlayerPhysics();
            CleanupOverlay();
            return;
        }


        // ====================================================
        // CHECK A / B
        // ====================================================

        if (activeBehaviour.startPoint == null ||
            activeBehaviour.endPoint == null)
        {
            RestorePlayerPhysics();
            CleanupOverlay();

            Debug.LogWarning(
                "PlayerRevealClip: " +
                "Start Point hoặc End Point chưa được set."
            );

            return;
        }


        // ====================================================
        // CREATE OVERLAY
        // ====================================================

        CreateOverlay();


        if (material == null)
            return;


        // ====================================================
        // TIME
        // ====================================================

        float duration =
            Mathf.Max(
                0.001f,
                (float)activeDuration
            );


        float time =
            Mathf.Clamp(
                (float)activeTime,
                0f,
                duration
            );


        // ====================================================
        // MOVEMENT DURATION
        // ====================================================

        float revealDuration =
            Mathf.Clamp(
                activeBehaviour.revealDuration,
                0f,
                duration
            );


        float movementDuration =
            Mathf.Max(
                0.001f,
                duration -
                revealDuration
            );


        // ====================================================
        // PLAYER MOVEMENT
        // ====================================================

        float movementT;


        if (time < movementDuration)
        {
            movementT =
                time /
                movementDuration;
        }
        else
        {
            movementT =
                1f;
        }


        movementT =
            Mathf.Clamp01(
                movementT
            );


        // Curve

        if (activeBehaviour.movementCurve != null)
        {
            movementT =
                activeBehaviour.movementCurve.Evaluate(
                    movementT
                );
        }


        // ====================================================
        // START / END POSITION
        // ====================================================

        Vector3 startPosition =
            activeBehaviour.startPoint.position;


        Vector3 endPosition =
            activeBehaviour.endPoint.position;


        // ====================================================
        // MOVE PLAYER
        // ====================================================

        Vector3 targetPosition =
            Vector3.Lerp(
                startPosition,
                endPosition,
                movementT
            );

        MovePlayer(targetPosition);

        SetPlayerWalkAnimation(time < movementDuration);


        // ====================================================
        // CIRCLE FOLLOW PLAYER
        // ====================================================

        Vector3 circleWorldPosition =
            player.position +
            activeBehaviour.circleFollowOffset;


        // ====================================================
        // WORLD → SCREEN
        // ====================================================

        Vector3 screenPosition =
            mainCamera.WorldToScreenPoint(
                circleWorldPosition
            );


        Vector2 screenCenter =
            new Vector2(
                screenPosition.x /
                Screen.width,

                screenPosition.y /
                Screen.height
            );


        // ====================================================
        // CIRCLE RADIUS
        // ====================================================

        float radius;


        // ----------------------------------------------------
        // PLAYER CHƯA TỚI B
        // ----------------------------------------------------

        if (time < movementDuration)
        {
            radius =
                activeBehaviour.startRadius;
        }


        // ----------------------------------------------------
        // PLAYER TỚI B
        // BẮT ĐẦU REVEAL
        // ----------------------------------------------------

        else
        {
            float revealT =
                Mathf.InverseLerp(
                    movementDuration,
                    duration,
                    time
                );


            revealT =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    revealT
                );


            radius =
                Mathf.Lerp(
                    activeBehaviour.startRadius,
                    activeBehaviour.finalRadius,
                    revealT
                );
        }


        // ====================================================
        // SEND TO SHADER
        // ====================================================

        material.SetVector(
            CenterID,
            screenCenter
        );


        material.SetFloat(
            RadiusID,
            radius
        );


        material.SetColor(
            InsideColorID,
            activeBehaviour.revealColor
        );


        material.SetFloat(
            InsideAlphaID,
            activeBehaviour.revealColorAlpha
        );


        material.SetFloat(
            EdgeSoftnessID,
            activeBehaviour.edgeSoftness
        );


        material.SetFloat(
            EdgeIntensityID,

            activeBehaviour.showEdge
                ? activeBehaviour.edgeIntensity
                : 0f
        );


        // ====================================================
        // REVEAL FINISHED
        // ====================================================

        /*
         * Khi clip gần kết thúc:
         *
         * Player chắc chắn ở B.
         *
         * Circle đã phủ toàn màn hình.
         *
         * Destroy overlay.
         */

        if (time >= duration - 0.001f)
        {
            MovePlayer(endPosition);

            SetPlayerWalkAnimation(false);

            RestorePlayerPhysics();


            FinishReveal();

            return;
        }
    }


    // ========================================================
    // PLAYER MOVEMENT
    // ========================================================

    private void MovePlayer(Vector3 targetPosition)
    {
        CachePlayerComponents();

        if (playerBody == null)
        {
            player.position = targetPosition;
            return;
        }

        BeginCutsceneMovement();

        playerBody.position = new Vector2(
            targetPosition.x,
            targetPosition.y
        );
        playerBody.linearVelocity = Vector2.zero;
        playerBody.angularVelocity = 0f;
    }

    private void CachePlayerComponents()
    {
        if (player == null)
            return;

        if (playerBody == null)
        {
            playerBody = player.GetComponent<Rigidbody2D>();
        }

        if (playerAnimation == null)
        {
            playerAnimation = player.GetComponentInChildren<AnimationSpine>();
        }
    }

    private void BeginCutsceneMovement()
    {
        if (cutsceneMovementActive || playerBody == null)
            return;

        originalBodyType = playerBody.bodyType;
        originalGravityScale = playerBody.gravityScale;
        playerPhysicsCached = true;

        playerBody.bodyType = RigidbodyType2D.Kinematic;
        playerBody.gravityScale = 0f;
        playerBody.linearVelocity = Vector2.zero;
        playerBody.angularVelocity = 0f;

        cutsceneMovementActive = true;
    }

    private void RestorePlayerPhysics()
    {
        SetPlayerWalkAnimation(false);

        if (!cutsceneMovementActive || playerBody == null)
            return;

        playerBody.linearVelocity = Vector2.zero;
        playerBody.angularVelocity = 0f;

        if (playerPhysicsCached)
        {
            playerBody.bodyType = originalBodyType;
            playerBody.gravityScale = originalGravityScale;
        }

        playerPhysicsCached = false;
        cutsceneMovementActive = false;
    }

    private void SetPlayerWalkAnimation(bool shouldWalk)
    {
        CachePlayerComponents();

        if (playerAnimation == null)
            return;

        if (shouldWalk)
        {
            if (playerAnimation.HasStateAnimation(AnimationSpine.State.Walk))
            {
                playerAnimation.Play(AnimationSpine.State.Walk, true);
            }

            return;
        }

        if (playerAnimation.CurrentState == AnimationSpine.State.Walk &&
            playerAnimation.HasStateAnimation(AnimationSpine.State.Idle))
        {
            playerAnimation.Play(AnimationSpine.State.Idle, true);
        }
    }

    // ========================================================
    // CREATE OVERLAY
    // ========================================================

    private void CreateOverlay()
    {
        if (overlayCreated)
            return;


        if (revealFinished)
            return;


        // ----------------------------------------------------
        // CANVAS
        // ----------------------------------------------------

        canvasObject =
            new GameObject(
                "Timeline_PlayerReveal_Runtime"
            );


        Canvas canvas =
            canvasObject.AddComponent<Canvas>();


        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;


        canvas.sortingOrder =
            32767;


        // ----------------------------------------------------
        // CANVAS SCALER
        // ----------------------------------------------------

        CanvasScaler scaler =
            canvasObject.AddComponent<CanvasScaler>();


        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;


        scaler.referenceResolution =
            new Vector2(
                1920f,
                1080f
            );


        // ----------------------------------------------------
        // OVERLAY
        // ----------------------------------------------------

        overlayObject =
            new GameObject(
                "PlayerRevealOverlay"
            );


        overlayObject.transform.SetParent(
            canvasObject.transform,
            false
        );


        RectTransform rect =
            overlayObject.AddComponent<RectTransform>();


        rect.anchorMin =
            Vector2.zero;


        rect.anchorMax =
            Vector2.one;


        rect.offsetMin =
            Vector2.zero;


        rect.offsetMax =
            Vector2.zero;


        // ----------------------------------------------------
        // IMAGE
        // ----------------------------------------------------

        overlayImage =
            overlayObject.AddComponent<Image>();


        overlayImage.raycastTarget =
            false;


        // ----------------------------------------------------
        // SHADER
        // ----------------------------------------------------

        Shader shader =
            Shader.Find(
                "Hidden/Timeline/PlayerReveal"
            );


        if (shader == null)
        {
            Debug.LogError(
                "PlayerReveal: Không tìm thấy shader " +
                "'Hidden/Timeline/PlayerReveal'."
            );


            CleanupOverlay();

            return;
        }


        material =
            new Material(shader);


        overlayImage.material =
            material;


        overlayCreated =
            true;
    }


    // ========================================================
    // FINISH REVEAL
    // ========================================================

    private void FinishReveal()
    {
        if (revealFinished)
            return;


        revealFinished =
            true;


        DestroyRuntimeOverlay();
    }


    // ========================================================
    // DESTROY RUNTIME OBJECT
    // ========================================================

    private void DestroyRuntimeOverlay()
    {
        if (canvasObject != null)
        {
            Object.Destroy(
                canvasObject
            );
        }


        canvasObject = null;

        overlayObject = null;

        overlayImage = null;

        material = null;

        overlayCreated = false;
    }


    // ========================================================
    // CLEANUP
    // ========================================================

    private void CleanupOverlay()
    {
        RestorePlayerPhysics();

        DestroyRuntimeOverlay();

        revealFinished = false;
    }


    // ========================================================
    // GRAPH STOP
    // ========================================================

    public override void OnGraphStop(
        Playable playable)
    {
        /*
         * Timeline stop / PlayableDirector destroy /
         * Scene unload:
         *
         * Runtime overlay sẽ bị destroy.
         */

        RestorePlayerPhysics();

        DestroyRuntimeOverlay();

        revealFinished = false;
    }
}
