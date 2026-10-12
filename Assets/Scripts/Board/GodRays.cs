using UnityEngine;

/// <summary>
/// 丁达尔光柱粒子：模拟月光穿过雾气形成的光柱。
/// 用锥形粒子发射体 + additive 混合，粒子沿光源方向扩散。
/// </summary>
public class GodRays : MonoBehaviour
{
    [Header("数量")]
    public int count = 60;

    [Header("光柱方向（朝向）")]
    public Vector3 lightDirection = new Vector3(0.5f, -0.7f, 0.3f);

    [Header("外观")]
    public Color color = new Color(0.7f, 0.82f, 0.9f, 0.15f);
    public float sizeMin = 1.5f;
    public float sizeMax = 3.5f;

    [Header("运动")]
    public float speedMin = 0.1f;
    public float speedMax = 0.3f;

    [Tooltip("可选：自己指定粒子材质。留空则自动用内置粒子材质")]
    public Material particleMaterialOverride;

    private ParticleSystem ps;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();
        Configure();
        ps.Play(true);
        ps.Emit(count);
    }

    void Configure()
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 8f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 12f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 30;

        var emission = ps.emission;
        emission.rateOverTime = count / 6f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 1.5f;
        shape.position = Vector3.zero;
        shape.rotation = Quaternion.LookRotation(lightDirection).eulerAngles;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color * 0.8f, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(color.a, 0.3f),
                new GradientAlphaKey(color.a, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.2f;
        noise.scrollSpeed = 0.15f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 40f;
            renderer.sharedMaterial = ResolveMaterial();
        }
    }

    private static Material sharedGodRay;
    private static Texture2D sharedDotTexture;

    static Texture2D GetDotTexture()
    {
        if (sharedDotTexture != null) return sharedDotTexture;
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a;
                pixels[y * size + x] = new Color(1, 1, 1, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        sharedDotTexture = tex;
        return tex;
    }

    Material ResolveMaterial()
    {
        if (particleMaterialOverride != null) return particleMaterialOverride;

        if (sharedGodRay == null)
        {
            Shader sh = Shader.Find("Mobile/Particles/Additive");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            sharedGodRay = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            sharedGodRay.mainTexture = GetDotTexture();
        }
        return sharedGodRay;
    }
}
