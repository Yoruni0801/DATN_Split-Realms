using UnityEngine;

[ExecuteAlways]
public class SPHFluidSimulator2D : MonoBehaviour
{
    struct Particle {
        public Vector2 position;
        public Vector2 velocity;
        public Vector2 force;
        public float density;
        public float pressure;
    }

    [Header("Simulation Settings")]
    public int particleCount = 2000;
    public float particleRadius = 0.1f;
    public float smoothingLength = 0.3f;
    public float mass = 1f;
    public float restDensity = 1000f;
    public float gasConstant = 2000f;
    public float viscosity = 200f;
    public float damping = 0.5f;
    public Vector2 gravity = new Vector2(0, -9.81f);
    public Vector4 bounds = new Vector4(-8, -4, 8, 4); // minX, minY, maxX, maxY

    [Header("Interaction")]
    public float interactionRadius = 2f;
    public float interactionForce = 50f;
    public Transform interactionTarget;

    [Header("References")]
    public ComputeShader computeShader;
    public Material particleMaterial;

    private ComputeBuffer particlesBuffer;
    private int kernelDensity, kernelForces, kernelIntegrate;

    void OnEnable() {
        InitSimulation();
    }

    void InitSimulation() {
        if (computeShader == null || particleMaterial == null) return;

        if (particlesBuffer != null) particlesBuffer.Release();
        particlesBuffer = new ComputeBuffer(particleCount, 32); 
        
        Particle[] particles = new Particle[particleCount];
        int side = Mathf.CeilToInt(Mathf.Sqrt(particleCount));
        float step = particleRadius * 1.5f;
        int idx = 0;
        for (int y = 0; y < side; y++) {
            for (int x = 0; x < side; x++) {
                if (idx < particleCount) {
                    particles[idx].position = new Vector2(x * step + bounds.x + 0.5f, y * step + bounds.y + 0.5f);
                    particles[idx].velocity = Vector2.zero;
                    idx++;
                }
            }
        }
        particlesBuffer.SetData(particles);

        kernelDensity = computeShader.FindKernel("DensityPressure");
        kernelForces = computeShader.FindKernel("Forces");
        kernelIntegrate = computeShader.FindKernel("Integrate");
    }

    void Update() {
        if (particlesBuffer == null || computeShader == null || particleMaterial == null) return;

        float h = smoothingLength;
        float h2 = h * h;
        float pi = Mathf.PI;
        float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
        
        computeShader.SetInt("numParticles", particleCount);
        computeShader.SetFloat("smoothingLength", h);
        computeShader.SetFloat("smoothingLengthSq", h2);
        computeShader.SetFloat("mass", mass);
        computeShader.SetFloat("restDensity", restDensity);
        computeShader.SetFloat("gasConstant", gasConstant);
        computeShader.SetFloat("viscosity", viscosity);
        computeShader.SetFloat("damping", damping);
        computeShader.SetVector("gravity", gravity);
        computeShader.SetVector("bounds", bounds);
        computeShader.SetFloat("deltaTime", dt);

        computeShader.SetFloat("Poly6Constant", 4f / (pi * Mathf.Pow(h, 8)));
        computeShader.SetFloat("SpikyGradConstant", -30f / (pi * Mathf.Pow(h, 5)));
        computeShader.SetFloat("ViscLapConstant", 20f / (3f * pi * Mathf.Pow(h, 5)));

        if (interactionTarget != null) {
            computeShader.SetVector("interactionPos", interactionTarget.position);
            computeShader.SetFloat("interactionRadius", interactionRadius);
            computeShader.SetFloat("interactionForce", interactionForce);
        } else {
            computeShader.SetFloat("interactionRadius", 0);
        }

        int threadGroups = Mathf.CeilToInt(particleCount / 256f);

        computeShader.SetBuffer(kernelDensity, "Particles", particlesBuffer);
        computeShader.Dispatch(kernelDensity, threadGroups, 1, 1);

        computeShader.SetBuffer(kernelForces, "Particles", particlesBuffer);
        computeShader.Dispatch(kernelForces, threadGroups, 1, 1);

        computeShader.SetBuffer(kernelIntegrate, "Particles", particlesBuffer);
        computeShader.Dispatch(kernelIntegrate, threadGroups, 1, 1);

        // Rendering
        particleMaterial.SetBuffer("Particles", particlesBuffer);
        particleMaterial.SetFloat("_ParticleRadius", particleRadius);
        
        Bounds boundsBox = new Bounds(Vector3.zero, Vector3.one * 100f);
        Graphics.DrawProcedural(particleMaterial, boundsBox, MeshTopology.Triangles, 6, particleCount, null, null, UnityEngine.Rendering.ShadowCastingMode.Off, false, gameObject.layer);
    }

    void OnDisable() {
        if (particlesBuffer != null) {
            particlesBuffer.Release();
            particlesBuffer = null;
        }
    }
}
