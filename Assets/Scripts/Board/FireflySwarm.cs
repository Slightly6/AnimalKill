using UnityEngine;

/// <summary>
/// 黑暗草原的萤火虫群。挂在一个空物体上，自动生成一群缓慢飘动的发光小点。
/// 用 ParticleSystem 驱动，自带呼吸明暗，不占用额外 Update。
/// </summary>
public class FireflySwarm : MonoBehaviour
{
    [Header("数量")]
    public int count = 40;

    [Header("活动范围（相对自身位置）")]
    public Vector3 areaSize = new Vector3(12f, 4f, 12f);

    [Header("外观")]
    public Color color = new Color(0.65f, 1f, 0.27f, 1f);   // 黄绿色
    public float sizeMin = 0.08f;
    public float sizeMax = 0.18f;

    [Header("运动")]
    public float speedMin = 0.3f;
    public float speedMax = 0.8f;

    private ParticleSystem ps;

    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        if (ps == null) ps = gameObject.AddComponent<ParticleSystem>();

        ConfigureParticleSystem();
        ps.Emit(count);
    }

    void ConfigureParticleSystem()
    {
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 5f;
        main.startLifetime = 6f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 20;

        // 发射：在区域内随机出生
        var emission = ps.emission;
        emission.rateOverTime = count / 6f;   // 6 秒内补满

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.scale = areaSize;

        // 颜色随生命周期呼吸（明暗）
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color * 0.3f, 0.5f),
                new GradientColorKey(color, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(1f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        // 大小随生命周期轻微变化
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.5f, 1, 1f));

        // 噪声：让萤火虫飘动更自然
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.2f;
        noise.scrollSpeed = 0.3f;

        // 渲染：用默认粒子材质，发光感靠颜色亮度
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = -10f;   // 让萤火虫显示在卡牌前面
        }
    }
}
