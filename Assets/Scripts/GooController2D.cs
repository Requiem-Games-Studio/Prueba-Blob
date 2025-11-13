using UnityEngine;

[RequireComponent(typeof(GooBody2D))]
public class GooController2D : MonoBehaviour
{
    GooBody2D goo;

    void Awake()
    {
        goo = GetComponent<GooBody2D>();
    }

    void Update()
    {
        // Movimiento horizontal (A/D o flechas)
        float x = Input.GetAxisRaw("Horizontal");

        // Salto (por defecto la tecla Space en el eje "Jump")
        bool jumpDown = Input.GetButtonDown("Jump");

        // Mandamos el input al cuerpo
        goo.input = new Vector2(x, 0f);

       // if (jumpDown)
        {
       //     goo.QueueJump();
        }
    }
}

