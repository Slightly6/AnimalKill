using UnityEngine;

/// <summary>
/// 地面装饰：随机散布草丛和蘑菇剪影，只做剪影不抢视线。
/// 挂在空物体上，运行时自动生成。
/// </summary>
public class GroundDecorations : MonoBehaviour
{
    [Header("范围")]
    public float innerRadius = 7f;
    public float outerRadius = 16f;

    [Header("草丛")]
    public int grassCount = 40;
    public Color grassColor = new Color(0.04f, 0.08f, 0.04f, 1f);
    public float grassHeightMin = 0.3f;
    public float grassHeightMax = 0.8f;

    [Header("蘑菇")]
    public int mushroomCount = 12;
    public Color mushroomColor = new Color(0.12f, 0.04f, 0.04f, 1f);
    public float mushroomScaleMin = 0.4f;
    public float mushroomScaleMax = 1.0f;

    void Start()
    {
        var grassMat = CreateMaterial(grassColor, "GrassSilhouette");
        var mushroomMat = CreateMaterial(mushroomColor, "MushroomSilhouette");

        for (int i = 0; i < grassCount; i++)
            CreateGrass(grassMat);

        for (int i = 0; i < mushroomCount; i++)
            CreateMushroom(mushroomMat);
    }

    Material CreateMaterial(Color c, string name)
    {
        var m = new Material(Shader.Find("Standard"));
        m.name = name;
        m.color = c;
        m.SetFloat("_Glossiness", 0f);
        m.SetFloat("_Metallic", 0f);
        m.hideFlags = HideFlags.HideAndDontSave;
        return m;
    }

    Vector3 RandomPosOnRing()
    {
        float angle = Random.value * Mathf.PI * 2f;
        float r = Random.Range(innerRadius, outerRadius);
        return new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
    }

    void CreateGrass(Material mat)
    {
        var go = new GameObject("Grass_" + Random.Range(0, 9999));
        go.transform.SetParent(transform, false);
        go.transform.position = RandomPosOnRing();

        int blades = Random.Range(3, 6);
        for (int i = 0; i < blades; i++)
        {
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            blade.transform.SetParent(go.transform, false);
            float h = Random.Range(grassHeightMin, grassHeightMax);
            blade.transform.localPosition = new Vector3(Random.Range(-0.1f, 0.1f), h * 0.5f, Random.Range(-0.1f, 0.1f));
            blade.transform.localScale = new Vector3(0.03f, h * 0.5f, 0.03f);
            blade.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
            Destroy(blade.GetComponent<Collider>());
            blade.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    void CreateMushroom(Material mat)
    {
        var go = new GameObject("Mushroom_" + Random.Range(0, 9999));
        go.transform.SetParent(transform, false);
        go.transform.position = RandomPosOnRing();
        float s = Random.Range(mushroomScaleMin, mushroomScaleMax);

        var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.name = "Stem";
        stem.transform.SetParent(go.transform, false);
        stem.transform.localPosition = new Vector3(0, 0.3f * s, 0);
        stem.transform.localScale = new Vector3(0.08f * s, 0.3f * s, 0.08f * s);
        Destroy(stem.GetComponent<Collider>());
        stem.GetComponent<Renderer>().sharedMaterial = mat;

        var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cap.name = "Cap";
        cap.transform.SetParent(go.transform, false);
        cap.transform.localPosition = new Vector3(0, 0.7f * s, 0);
        cap.transform.localScale = new Vector3(0.5f * s, 0.3f * s, 0.5f * s);
        Destroy(cap.GetComponent<Collider>());
        cap.GetComponent<Renderer>().sharedMaterial = mat;
    }
}
