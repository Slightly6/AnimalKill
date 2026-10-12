using UnityEngine;

/// <summary>
/// 空中灰尘微粒：细小漂浮的微光点，让丁达尔光柱更明显。
/// 挂在空物体上，自动配置 ParticleSystem。
/// </summary>
public class DustMotes : MonoBehaviour
{
    [Header("数量")]
    public int count = 80;

    [Header("活动范围")]
    public Vector3 areaSize = new Vector3(16f, 8f, 12f);

    [Header("外观")]
    public Color color = new Color(0.7f, 0.82f, 0.9f, 0.6f);
    public float sizeMin = 0.03f;
    public float sizeMax = 0.08f;

    [Header("运动")]
    public float speedMin = 0.05f;
    public float speedMax = 0.2f;
    public float driftStrength = 0.4f;

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
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 18f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 30;

        var emission = ps.emission;
        emission.rateOverTime = count / 10f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = areaSize;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color * 1.2f, 0.5f),
                new GradientColorKey(color, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(color.a, 0.3f),
                new GradientAlphaKey(color.a * 0.7f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = driftStrength;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.15f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 30f;
            renderer.sharedMaterial = ResolveMaterial();
        }
    }

    private static Material sharedDust;
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

        if (sharedDust == null)
        {
            Shader sh = Shader.Find("Mobile/Particles/Additive");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            sharedDust = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            sharedDust.mainTexture = GetDotTexture();
        }
        return sharedDust;
    }
}
