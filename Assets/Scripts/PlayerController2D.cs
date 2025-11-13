using UnityEngine;

namespace GooBlob
{
    [RequireComponent(typeof(GooBody2D))]
    public class PlayerController2D : MonoBehaviour
    {
        public string horizontalAxis = "Horizontal";
        public string verticalAxis = "Vertical";
        public bool useRaw = true;
        public float deadZone = 0.1f;

        GooBody2D goo;

        void Awake() { goo = GetComponent<GooBody2D>(); }

        void Update()
        {
            float x = useRaw ? Input.GetAxisRaw(horizontalAxis) : Input.GetAxis(horizontalAxis);
            float y = 0f;

            Vector2 inp = new Vector2(x, y);
            if (inp.sqrMagnitude < deadZone * deadZone) inp = Vector2.zero;

            goo.input = inp;
            goo.lookDir = new Vector2(x, 0f);
        }
    }
}