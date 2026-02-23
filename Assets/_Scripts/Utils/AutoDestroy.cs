using UnityEngine;

public class AutoDestroy : MonoBehaviour
{
    public float delay = 1.0f; // 默认 1 秒后销毁

    void Start()
    {
        // 也可以通过获取 Animator 的时长来自动设置，这里先手动填
        Destroy(gameObject, delay);
    }
}