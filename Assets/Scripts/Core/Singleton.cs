using UnityEngine;

/// <summary>
/// 单例基类。任何 Manager 继承它就能全局访问。
/// 用法：public class GameManager : Singleton<GameManager> { }
///       然后任何地方写 GameManager.Instance 就能拿到它。
/// </summary>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                // 场景里找
                _instance = FindObjectOfType<T>();

                // 找不到就新建一个
                if (_instance == null)
                {
                    GameObject obj = new GameObject(typeof(T).Name);
                    _instance = obj.AddComponent<T>();
                }
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        // 已经有一个了，销毁自己
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this as T;
    }
}
