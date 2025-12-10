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

        // Mandamos el input al cuerpo
        goo.input = new Vector2(x, 0f);

           // Salto (por defecto la tecla Space en el eje "Jump")
        if (Input.GetButtonDown("Jump"))
        {
            goo.PressJump();
        }

        if (Input.GetButtonUp("Jump"))
        {
            goo.ReleaseJump();
        }

       // if (jumpDown)
       //     goo.QueueJump();
        
    }
}

