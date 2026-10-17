using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;

namespace MagicArsenal
{

public class MagicBeamScript : MonoBehaviour {

    [Header("Prefabs")]
    public GameObject[] beamLineRendererPrefab;
    public GameObject[] beamStartPrefab;
    public GameObject[] beamEndPrefab;

    private int currentBeam = 0;

    private GameObject beamStart;
    private GameObject beamEnd;
    private GameObject beam;
    private LineRenderer line;
    private global::Player runtimeOwner;
    private Vector3 runtimeStart;
    private Vector2 runtimeDirection;
    private Collider2D runtimeTarget;
    private LayerMask runtimeTargetLayer;
    private float runtimeMaxDistance;
    private float runtimeDuration;
    private float runtimeTimer;
    private float runtimeDamageTimer;
    private float runtimeDamageInterval;
    private int runtimeDamage;
    private bool runtimeActive;

    [Header("Adjustable Variables")]
    public float beamEndOffset = 1f; //How far from the raycast hit point the end effect is positioned
    public float textureScrollSpeed = 8f; //How fast the texture scrolls along the beam
	public float textureLengthScale = 3; //Length of the beam texture

    [Header("Put Sliders here (Optional)")]
    public Slider endOffSetSlider; //Use UpdateEndOffset function on slider
    public Slider scrollSpeedSlider; //Use UpdateScrollSpeed function on slider

    [Header("Put UI Text object here to show beam name")]
    public Text textBeamName;

    // Use this for initialization
    void Start()
    {
        if (textBeamName && HasBeamPrefabSet())
            textBeamName.text = beamLineRendererPrefab[currentBeam].name;
        if (endOffSetSlider)
            endOffSetSlider.value = beamEndOffset;
        if (scrollSpeedSlider)
            scrollSpeedSlider.value = textureScrollSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        if (runtimeActive)
        {
            UpdateRuntimeBeam();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();

        if (Input.GetMouseButtonDown(0))
        {
            CreateBeamInstances();
        }
        if (Input.GetMouseButtonUp(0))
        {
            DestroyBeamInstances();
        }

        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray.origin, ray.direction, out hit))
            {
                Vector3 tdir = hit.point - transform.position;
                ShootBeamInDir(transform.position, tdir);
            }
        }
		
		if (Input.GetKeyDown(KeyCode.RightArrow)) //4 next if commands are just hotkeys for cycling beams
        {
            nextBeam();
        }

		if (Input.GetKeyDown(KeyCode.D))
		{
			nextBeam();
		}

		if (Input.GetKeyDown(KeyCode.A))
		{
			previousBeam();
		}
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            previousBeam();
        }
		
    }

    public void nextBeam() // Next beam
    {
        if (beamLineRendererPrefab == null || beamLineRendererPrefab.Length == 0)
        {
            return;
        }

        if (currentBeam < beamLineRendererPrefab.Length - 1)
            currentBeam++;
        else
            currentBeam = 0;

        if (textBeamName)
            textBeamName.text = beamLineRendererPrefab[currentBeam].name;
    }
	
	    public void previousBeam() // Previous beam
    {
        if (beamLineRendererPrefab == null || beamLineRendererPrefab.Length == 0)
        {
            return;
        }

        if (currentBeam > 0)
            currentBeam--;
        else
            currentBeam = beamLineRendererPrefab.Length - 1;

        if (textBeamName)
            textBeamName.text = beamLineRendererPrefab[currentBeam].name;
    }
	

    public void UpdateEndOffset()
    {
        beamEndOffset = endOffSetSlider.value;
    }

    public void UpdateScrollSpeed()
    {
        textureScrollSpeed = scrollSpeedSlider.value;
    }

    public void FireFrom(
        global::Player owner,
        Vector3 start,
        Vector2 direction,
        float duration,
        float maxDistance,
        int damage,
        LayerMask targetLayer)
    {
        FireFrom(owner, start, direction, duration, maxDistance, damage, 0f, null, targetLayer);
    }

    public void FireFrom(
        global::Player owner,
        Vector3 start,
        Vector2 direction,
        float duration,
        float maxDistance,
        int damage,
        float damageTickInterval,
        Collider2D target,
        LayerMask targetLayer)
    {
        runtimeOwner = owner;
        runtimeStart = start;
        runtimeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        runtimeTarget = target;
        runtimeDuration = Mathf.Max(0.05f, duration);
        runtimeMaxDistance = Mathf.Max(0.1f, maxDistance);
        runtimeDamage = Mathf.Max(0, damage);
        runtimeDamageInterval = Mathf.Max(0.01f, damageTickInterval);
        runtimeDamageTimer = 0f;
        runtimeTargetLayer = targetLayer;
        runtimeTimer = 0f;
        runtimeActive = true;

        CreateBeamInstances();
        UpdateRuntimeBeam();
    }

    private void UpdateRuntimeBeam()
    {
        runtimeTimer += Time.deltaTime;
        UpdateRuntimeDirection();
        ShootRuntimeBeam(runtimeStart, runtimeDirection);
        TickRuntimeDamage();

        if (runtimeTimer >= runtimeDuration)
        {
            runtimeActive = false;
            DestroyBeamInstances();
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        DestroyBeamInstances();
    }

    private void ApplyRuntimeDamage()
    {
        if (runtimeOwner == null || runtimeDamage <= 0)
        {
            return;
        }

        if (runtimeTarget != null)
        {
            if (FairyBeamSkillExecutor.IsSelfCollider(runtimeTarget, runtimeOwner) ||
                !FairyBeamSkillExecutor.IsValidEnemyTarget(runtimeTarget))
            {
                return;
            }

            global::PlayerHitResolver.TryApplyDamageToEnemy(runtimeTarget, runtimeDamage, runtimeOwner.transform);
            return;
        }

        RaycastHit2D hit = Physics2D.Raycast(runtimeStart, runtimeDirection, runtimeMaxDistance, runtimeTargetLayer);
        Collider2D hitCollider = hit.collider;
        if (hitCollider == null || hitCollider.transform.root == runtimeOwner.transform.root)
        {
            return;
        }

        global::PlayerHitResolver.TryApplyDamageToEnemy(hitCollider, runtimeDamage, runtimeOwner.transform);
    }

    private void UpdateRuntimeDirection()
    {
        if (runtimeTarget == null)
        {
            return;
        }

        Vector2 direction = (Vector2)runtimeTarget.bounds.center - (Vector2)runtimeStart;
        if (direction.sqrMagnitude > 0.0001f)
        {
            runtimeDirection = direction.normalized;
        }
    }

    private void TickRuntimeDamage()
    {
        runtimeDamageTimer += Time.deltaTime;
        while (runtimeDamageTimer >= runtimeDamageInterval)
        {
            runtimeDamageTimer -= runtimeDamageInterval;
            ApplyRuntimeDamage();
        }
    }

    private void CreateBeamInstances()
    {
        if (line != null || !HasBeamPrefabSet())
        {
            return;
        }

        beamStart = Instantiate(beamStartPrefab[currentBeam], Vector3.zero, Quaternion.identity) as GameObject;
        beamEnd = Instantiate(beamEndPrefab[currentBeam], Vector3.zero, Quaternion.identity) as GameObject;
        beam = Instantiate(beamLineRendererPrefab[currentBeam], Vector3.zero, Quaternion.identity) as GameObject;
        line = beam.GetComponent<LineRenderer>();
    }

    private void DestroyBeamInstances()
    {
        Destroy(beamStart);
        Destroy(beamEnd);
        Destroy(beam);
        beamStart = null;
        beamEnd = null;
        beam = null;
        line = null;
    }

    private bool HasBeamPrefabSet()
    {
        return beamLineRendererPrefab != null &&
            beamStartPrefab != null &&
            beamEndPrefab != null &&
            beamLineRendererPrefab.Length > 0 &&
            beamStartPrefab.Length > 0 &&
            beamEndPrefab.Length > 0 &&
            currentBeam >= 0 &&
            currentBeam < beamLineRendererPrefab.Length &&
            currentBeam < beamStartPrefab.Length &&
            currentBeam < beamEndPrefab.Length &&
            beamLineRendererPrefab[currentBeam] != null &&
            beamStartPrefab[currentBeam] != null &&
            beamEndPrefab[currentBeam] != null;
    }

    private void ShootRuntimeBeam(Vector3 start, Vector2 direction)
    {
        if (line == null)
        {
            return;
        }

        #if UNITY_5_5_OR_NEWER
        line.positionCount = 2;
        #else
        line.SetVertexCount(2);
        #endif
        line.SetPosition(0, start);
        beamStart.transform.position = start;

        Vector3 end = start + (Vector3)(direction.normalized * runtimeMaxDistance);
        if (runtimeTarget != null)
        {
            end = runtimeTarget.bounds.center;
        }
        else
        {
            RaycastHit2D hit = Physics2D.Raycast(start, direction, runtimeMaxDistance, runtimeTargetLayer);
            if (hit.collider != null)
            {
                end = hit.point - (direction.normalized * beamEndOffset);
            }
        }

        beamEnd.transform.position = end;
        line.SetPosition(1, end);

        beamStart.transform.LookAt(beamEnd.transform.position);
        beamEnd.transform.LookAt(beamStart.transform.position);

        float distance = Vector3.Distance(start, end);
        line.sharedMaterial.mainTextureScale = new Vector2(distance / textureLengthScale, 1);
        line.sharedMaterial.mainTextureOffset -= new Vector2(Time.deltaTime * textureScrollSpeed, 0);
    }

    void ShootBeamInDir(Vector3 start, Vector3 dir)
    {
        if (line == null)
        {
            return;
        }

        #if UNITY_5_5_OR_NEWER
		line.positionCount = 2;
		#else
		line.SetVertexCount(2); 
		#endif
        line.SetPosition(0, start);
        beamStart.transform.position = start;

        Vector3 end = Vector3.zero;
        RaycastHit hit;
        if (Physics.Raycast(start, dir, out hit))
            end = hit.point - (dir.normalized * beamEndOffset);
        else
            end = transform.position + (dir * 100);

        beamEnd.transform.position = end;
        line.SetPosition(1, end);

        beamStart.transform.LookAt(beamEnd.transform.position);
        beamEnd.transform.LookAt(beamStart.transform.position);

        float distance = Vector3.Distance(start, end);
        line.sharedMaterial.mainTextureScale = new Vector2(distance / textureLengthScale, 1);
        line.sharedMaterial.mainTextureOffset -= new Vector2(Time.deltaTime * textureScrollSpeed, 0);
    }
}
}
