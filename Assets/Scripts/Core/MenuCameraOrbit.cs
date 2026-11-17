using UnityEngine;

namespace Hunt.Core
{
    /// <summary>시작 화면 배경: 아레나를 천천히 도는 카메라. 살짝 흔들려 불안한 느낌을 준다.</summary>
    public class MenuCameraOrbit : MonoBehaviour
    {
        public float radius = 21f;
        public float height = 4.5f;
        public float speed = 4f; // 도/초

        float angle = 200f;

        void Update()
        {
            angle += speed * Time.unscaledDeltaTime;
            float a = angle * Mathf.Deg2Rad;
            float bob = Mathf.Sin(Time.unscaledTime * 0.4f) * 0.6f;
            transform.position = new Vector3(Mathf.Cos(a) * radius, height + bob, Mathf.Sin(a) * radius);
            var look = new Vector3(0f, 1.5f, 0f) + new Vector3(Mathf.Sin(Time.unscaledTime * 0.23f), 0f, Mathf.Cos(Time.unscaledTime * 0.19f)) * 1.5f;
            transform.rotation = Quaternion.LookRotation(look - transform.position);
        }
    }
}
