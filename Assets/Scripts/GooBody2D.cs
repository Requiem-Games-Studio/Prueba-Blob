using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class GooBody2D : MonoBehaviour
{
    [Header("References")]
    public Transform bodySprite;
    public Transform eyeL;
    public Transform eyeR;
    public Transform armL, armR, legL, legR;

    [Header("Anchors")]
    public bool useLimbAnchors = true;
    public Transform anchorArmL, anchorArmR, anchorLegL, anchorLegR;
    public Transform eyeAnchorL, eyeAnchorR;

    [Header("Walk Cycle (Rayman)")]
    public float walkFrequency     = 6f;
    public float stepLength        = 0.6f;
    public float stepHeight        = 0.2f;
    public float armSwingX         = 0.4f;
    public float armSwingY         = 0.2f;
    public float bodyBobAmplitude  = 0.08f;
    public float startWalkSpeed    = 0.25f;

    [Header("Motion")]
    public float moveForce    = 20f;
    public float maxSpeed     = 5f;
    public float squashAmount = 0.15f;   // squash por velocidad
    public float squashSmooth = 0.1f;

    [Header("Jump")]
    public LayerMask groundMask;
    public float groundCheckOffsetY = -0.5f; // punto de chequeo (bajo el cuerpo)
    public float groundCheckRadius  = 0.2f;  // radio del chequeo
    public float jumpForce          = 8.5f;  // fuerza del salto (impulso)
    public int   maxAirJumps        = 0;     // 0 = solo salto en tierra, 1 = doble salto, etc.
    public float coyoteTime         = 0.12f; // margen tras caer aún puedes saltar
    public float jumpBufferTime     = 0.12f; // margen si presionas antes de tocar suelo
    public float lowJumpGravityMult = 2.2f;  // si sueltas salto rápido, cae más
    public float fallGravityMult    = 2.8f;  // caída más pesada

    [Header("Limb Follow")]
    public float limbFollow = 12f;

    [Header("Eyes")]
    [Range(0f, 0.5f)]
    public float eyeRange = 0.15f;

    [Header("Idle")]
    public float idleBobAmplitude = 0.03f;
    public float idleBobFrequency = 1.5f;

    [Header("Blink")]
    public float minBlinkInterval = 2f;
    public float maxBlinkInterval = 5f;
    public float blinkDuration    = 0.12f;

    [Header("Collision Squash")]
    public float collisionSquashAmount   = 0.35f;
    public float collisionSquashDuration = 0.20f;

    [Header("Squeeze (espacios angostos)")]
    public bool  useSqueezeZones   = true;
    public float squeezeLerpSpeed  = 10f;
    [Range(0.1f, 1f)]
    public float minSqueezeFactor  = 0.25f;

    // --- Inputs públicos (los rellena tu controller) ---
    [HideInInspector] public Vector2 input;
    [HideInInspector] public Vector2 lookDir;

    // --- Internos ---
    Rigidbody2D rb;
    CircleCollider2D bodyCollider;

    // Offsets del collider (tuyos)
    public Vector2 normalOffset = new Vector2(0f, -0.9f);
    public Vector2 squeezedOffset = new Vector2(0f, 0f);

    Vector3 scaleVel;

    Vector3 armLBaseLocal, armRBaseLocal;
    Vector3 legLBaseLocal, legRBaseLocal;
    Vector3 bodyBaseLocal;
    Vector3 bodyBaseScale = Vector3.one;

    Vector2 baseBodySize;

    float walkPhase;
    float idlePhase;

    // Blink
    float blinkTimer;
    float nextBlinkTime;
    float blinkT;
    bool  isBlinking;
    float blinkScaleY = 1f;
    Vector3 eyeLBaseScale, eyeRBaseScale;

    // Squash por colisión
    float   collisionSquashTimer;
    Vector2 collisionNormal;

    // Collider base
    float baseColliderRadius;

    // Squeeze
    Vector3 squeezeScaleTarget  = Vector3.one;
    Vector3 squeezeScaleCurrent = Vector3.one;
    Collider2D currentSqueezeZone;

    // Jump state
    bool grounded;
    float coyoteCounter;
    float jumpBufferCounter;
    int   airJumpsUsed;
    bool  jumpHeld;
    bool  jumpQueued; // pedido de salto (desde input)

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<CircleCollider2D>();

        if (anchorArmL) armLBaseLocal = anchorArmL.localPosition;
        if (anchorArmR) armRBaseLocal = anchorArmR.localPosition;
        if (anchorLegL) legLBaseLocal = anchorLegL.localPosition;
        if (anchorLegR) legRBaseLocal = anchorLegR.localPosition;

        if (eyeL) eyeLBaseScale = eyeL.localScale;
        if (eyeR) eyeRBaseScale = eyeR.localScale;

        blinkTimer    = 0f;
        nextBlinkTime = Random.Range(minBlinkInterval, maxBlinkInterval);
        blinkT        = 0f;
        isBlinking    = false;
        blinkScaleY   = 1f;

        if (bodySprite)
        {
            bodyBaseLocal = bodySprite.localPosition;
            bodyBaseScale = bodySprite.localScale;

            var sr = bodySprite.GetComponent<SpriteRenderer>();
            if (sr)
                baseBodySize = sr.bounds.size;
        }

        if (bodyCollider)
        {
            baseColliderRadius = bodyCollider.radius;
        }

        squeezeScaleTarget  = Vector3.one;
        squeezeScaleCurrent = Vector3.one;

        // recomendado para no girar con golpes
        rb.freezeRotation = true;
    }

    void Start()
    {
        if (useLimbAnchors)
        {
            if (armL && anchorArmL) armL.position = anchorArmL.position;
            if (armR && anchorArmR) armR.position = anchorArmR.position;
            if (legL && anchorLegL) legL.position = anchorLegL.position;
            if (legR && anchorLegR) legR.position = anchorLegR.position;
        }
    }

    void Update()
    {
        // --- Movimiento horizontal con física (como lo tenías) ---
        if (input.sqrMagnitude > 0.0001f)
            rb.AddForce(new Vector2(input.x, 0f) * moveForce, ForceMode2D.Force);

        Vector2 vel = rb.velocity;
        float speedX = Mathf.Abs(vel.x);

        if (speedX > maxSpeed)
            rb.velocity = new Vector2(Mathf.Sign(vel.x) * maxSpeed, vel.y);

        // --- SALTO: lectura rápida con input.y (opcional) ---
       // if (input.y > 0.5f) PressJump();
        //else ReleaseJump();

    

        // --- Ground check, coyote y jump buffer ---
        UpdateGrounded();
        UpdateJumpTimers();

        // --- Ejecutar salto si corresponde ---
        TryConsumeJump();

        // --- Gravedad variable para mejor feel ---
        ApplyBetterJumpGravity();

        // --- Squash (velocidad + colisión + squeeze) ---
        ActualizarSquashVisual(speedX);

        // --- Estado caminar vs idle ---
        bool isWalking = speedX >= startWalkSpeed;

        if (useLimbAnchors)
        {
            if (isWalking)
                UpdateRaymanWalk();
            else
                UpdateIdlePose();

            FollowLimbToAnchor(legL,  anchorLegL);
            FollowLimbToAnchor(legR,  anchorLegR);
            FollowLimbToAnchor(armL,  anchorArmL);
            FollowLimbToAnchor(armR,  anchorArmR);
        }

        // --- Blink + ojos ---
        UpdateBlinkLogic();
        UpdateEyes();
    }

    // =============== INPUT API PARA TU CONTROLLER ===============
    public void PressJump()
    {
        if (!jumpHeld) // flanco de subida
            jumpQueued = true;
        jumpHeld = true;
    }

    public void ReleaseJump()
    {
        jumpHeld = false;
    }

    // =============== GROUND CHECK + TIMERS ===============
    void UpdateGrounded()
    {
        Vector2 origin = (Vector2)transform.position + new Vector2(0f, groundCheckOffsetY);
        grounded = Physics2D.OverlapCircle(origin, groundCheckRadius, groundMask);

        if (grounded)
        {
            coyoteCounter = coyoteTime;
            airJumpsUsed = 0; // reset al tocar suelo
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }
    }

    void UpdateJumpTimers()
    {
        if (jumpQueued)
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;

        // limpiar flag de frame
        jumpQueued = false;
    }

    void TryConsumeJump()
    {
        // ¿hay un salto en buffer y se puede saltar por coyote o suelo?
        bool canGroundJump = (grounded || coyoteCounter > 0f);
        bool canAirJump    = (!canGroundJump && airJumpsUsed < maxAirJumps);

        if (jumpBufferCounter > 0f && (canGroundJump || canAirJump))
        {
            // aplicar salto
            float vy = rb.velocity.y;
            if (vy < 0f) vy = 0f; // evita salto "pesado" si caías
            rb.velocity = new Vector2(rb.velocity.x, vy);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

            // contabilidad
            if (canAirJump && !canGroundJump)
                airJumpsUsed++;

            // consumir buffer y coyote
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }
    }

    void ApplyBetterJumpGravity()
    {
        // Caída más pesada
        if (rb.velocity.y < -0.01f)
        {
            rb.velocity += Vector2.up * Physics2D.gravity.y * (fallGravityMult - 1f) * Time.deltaTime;
        }
        // Si vas subiendo pero soltaste salto, corta la subida
        else if (rb.velocity.y > 0.01f && !jumpHeld)
        {
            rb.velocity += Vector2.up * Physics2D.gravity.y * (lowJumpGravityMult - 1f) * Time.deltaTime;
        }
    }

    // ================= SQUASH VISUAL GLOBAL =================
    void ActualizarSquashVisual(float speedX)
    {
        if (!bodySprite) return;

        // 1) Squash por velocidad
        float t = Mathf.Clamp01(speedX / maxSpeed);
        Vector3 moveScale = new Vector3(
            1f + t * squashAmount,
            1f - t * squashAmount,
            1f
        );

        // 2) Squash por colisión
        Vector3 collisionScale = Vector3.one;
        if (collisionSquashTimer > 0f)
        {
            collisionSquashTimer -= Time.deltaTime;
            float k = Mathf.Clamp01(collisionSquashTimer / collisionSquashDuration);
            float strength = Mathf.Sin(k * Mathf.PI);

            if (Mathf.Abs(collisionNormal.x) > Mathf.Abs(collisionNormal.y))
            {
                float sx = 1f - collisionSquashAmount * strength;
                float sy = 1f + collisionSquashAmount * strength;
                collisionScale = new Vector3(sx, sy, 1f);
            }
            else
            {
                float sy = 1f - collisionSquashAmount * strength;
                float sx = 1f + collisionSquashAmount * strength;
                collisionScale = new Vector3(sx, sy, 1f);
            }
        }

        // 3) Squeeze (espacios angostos) — SOLO visual aquí
        squeezeScaleCurrent = Vector3.Lerp(
            squeezeScaleCurrent,
            squeezeScaleTarget,
            Time.deltaTime * squeezeLerpSpeed
        );

        // 4) Factor visual total = movimiento * colisión * squeeze
        Vector3 visualFactor = Vector3.Scale(moveScale, collisionScale);
        visualFactor = Vector3.Scale(visualFactor, squeezeScaleCurrent);

        // Aplicar al sprite
        Vector3 targetScale = new Vector3(
            bodyBaseScale.x * visualFactor.x,
            bodyBaseScale.y * visualFactor.y,
            bodyBaseScale.z * visualFactor.z
        );

        bodySprite.localScale = Vector3.SmoothDamp(
            bodySprite.localScale,
            targetScale,
            ref scaleVel,
            squashSmooth
        );

        // 5) Collider: SOLO responde a squeeze, NO a velocidad ni colisión
        if (bodyCollider != null)
        {
            float targetRadius = baseColliderRadius;
            if (useSqueezeZones && currentSqueezeZone != null)
            {
                float squeezeFactor = Mathf.Min(squeezeScaleCurrent.x, squeezeScaleCurrent.y);
                targetRadius = baseColliderRadius * squeezeFactor;
            }

            bodyCollider.radius = Mathf.Lerp(
                bodyCollider.radius,
                targetRadius,
                Time.deltaTime * squeezeLerpSpeed
            );
        }
    }

    // ================== IDLE ==================
    void UpdateIdlePose()
    {
        if (anchorLegL)
            anchorLegL.localPosition = Vector3.Lerp(anchorLegL.localPosition, legLBaseLocal, Time.deltaTime * 6f);
        if (anchorLegR)
            anchorLegR.localPosition = Vector3.Lerp(anchorLegR.localPosition, legRBaseLocal, Time.deltaTime * 6f);
        if (anchorArmL)
            anchorArmL.localPosition = Vector3.Lerp(anchorArmL.localPosition, armLBaseLocal, Time.deltaTime * 6f);
        if (anchorArmR)
            anchorArmR.localPosition = Vector3.Lerp(anchorArmR.localPosition, armRBaseLocal, Time.deltaTime * 6f);

        if (bodySprite)
        {
            idlePhase += idleBobFrequency * Time.deltaTime;
            float bob = Mathf.Sin(idlePhase) * idleBobAmplitude;
            bodySprite.localPosition = bodyBaseLocal + Vector3.up * bob;
        }
    }

    // ================== WALK CYCLE ==================
    void UpdateRaymanWalk()
    {
        float speedX = Mathf.Abs(rb.velocity.x);
        float moveAmount = Mathf.InverseLerp(startWalkSpeed, maxSpeed, speedX);
        if (moveAmount < 0.001f)
            return;

        walkPhase += walkFrequency * speedX * Time.deltaTime;

        float dir = Mathf.Sign(rb.velocity.x);
        if (dir == 0f) dir = 1f;

        Vector3 right = Vector3.right * dir;
        Vector3 up    = Vector3.up;

        float phaseLegL = -walkPhase;
        float phaseLegR = -walkPhase + Mathf.PI;

        float stride = stepLength;
        float lift   = stepHeight;

        if (anchorLegL)
        {
            float c = Mathf.Cos(phaseLegL);
            float s = Mathf.Sin(phaseLegL);
            float offX = c * stride;
            float offY = s > 0f ? s * lift : 0f;

            Vector3 offset = right * offX + up * offY;
            Vector3 target = legLBaseLocal + offset * moveAmount;

            anchorLegL.localPosition = Vector3.Lerp(
                anchorLegL.localPosition,
                target,
                Time.deltaTime * 10f
            );
        }

        if (anchorLegR)
        {
            float c = Mathf.Cos(phaseLegR);
            float s = Mathf.Sin(phaseLegR);
            float offX = c * stride;
            float offY = s > 0f ? s * lift : 0f;

            Vector3 offset = right * offX + up * offY;
            Vector3 target = legRBaseLocal + offset * moveAmount;

            anchorLegR.localPosition = Vector3.Lerp(
                anchorLegR.localPosition,
                target,
                Time.deltaTime * 10f
            );
        }

        float armRadiusX = armSwingX;
        float armRadiusY = armSwingY;

        if (anchorArmL)
        {
            float c = Mathf.Cos(phaseLegR);
            float s = Mathf.Sin(phaseLegR);
            float offX = c * armRadiusX;
            float offY = s * armRadiusY;

            Vector3 offset = right * offX + up * offY;
            Vector3 target = armLBaseLocal + offset * moveAmount;

            anchorArmL.localPosition = Vector3.Lerp(
                anchorArmL.localPosition,
                target,
                Time.deltaTime * 10f
            );
        }

        if (anchorArmR)
        {
            float c = Mathf.Cos(phaseLegL);
            float s = Mathf.Sin(phaseLegL);
            float offX = c * armRadiusX;
            float offY = s * armRadiusY;

            Vector3 offset = right * offX + up * offY;
            Vector3 target = armRBaseLocal + offset * moveAmount;

            anchorArmR.localPosition = Vector3.Lerp(
                anchorArmR.localPosition,
                target,
                Time.deltaTime * 10f
            );
        }

        if (bodySprite)
        {
            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * bodyBobAmplitude * moveAmount;
            Vector3 targetBody = bodyBaseLocal + up * bob;

            bodySprite.localPosition = Vector3.Lerp(
                bodySprite.localPosition,
                targetBody,
                Time.deltaTime * 8f
            );
        }
    }

    // ================== BLINK ==================
    void UpdateBlinkLogic()
    {
        if (!eyeL && !eyeR) return;

        blinkTimer += Time.deltaTime;

        if (!isBlinking && blinkTimer >= nextBlinkTime)
        {
            isBlinking = true;
            blinkT     = 0f;
        }

        if (isBlinking)
        {
            blinkT += Time.deltaTime;
            float half = blinkDuration * 0.5f;

            if (blinkT <= blinkDuration)
            {
                if (blinkT <= half)
                {
                    float tt = blinkT / half;
                    blinkScaleY = Mathf.Lerp(1f, 0.15f, tt);
                }
                else
                {
                    float tt = (blinkT - half) / half;
                    blinkScaleY = Mathf.Lerp(0.15f, 1f, tt);
                }
            }
            else
            {
                isBlinking    = false;
                blinkTimer    = 0f;
                blinkScaleY   = 1f;
                nextBlinkTime = Random.Range(minBlinkInterval, maxBlinkInterval);
            }
        }
        else
        {
            blinkScaleY = Mathf.MoveTowards(blinkScaleY, 1f, Time.deltaTime * 10f);
        }
    }

    // ================== OJOS ==================
    void UpdateEyes()
    {
        if (!eyeL && !eyeR) return;

        Vector2 vel = rb.velocity;

        Vector2 dirEyes =
            (lookDir.sqrMagnitude > 0.001f) ? lookDir.normalized :
            (vel.sqrMagnitude > 0.001f)     ? vel.normalized :
            Vector2.zero;

        float bodyRadius = 0.4f;
        if (bodySprite)
        {
            var r = bodySprite.GetComponent<SpriteRenderer>();
            if (r) bodyRadius = Mathf.Max(r.bounds.extents.x, r.bounds.extents.y);
        }

        Vector3 lookOffset = (Vector3)(dirEyes * eyeRange * bodyRadius);

        if (eyeL && eyeAnchorL)
        {
            eyeL.position = Vector3.Lerp(
                eyeL.position,
                eyeAnchorL.position + lookOffset,
                10f * Time.deltaTime
            );

            Vector3 targetScaleL = eyeLBaseScale;
            targetScaleL.y *= blinkScaleY;

            eyeL.localScale = Vector3.Lerp(
                eyeL.localScale,
                targetScaleL,
                25f * Time.deltaTime
            );
        }

        if (eyeR && eyeAnchorR)
        {
            eyeR.position = Vector3.Lerp(
                eyeR.position,
                eyeAnchorR.position + lookOffset,
                10f * Time.deltaTime
            );

            Vector3 targetScaleR = eyeRBaseScale;
            targetScaleR.y *= blinkScaleY;

            eyeR.localScale = Vector3.Lerp(
                eyeR.localScale,
                targetScaleR,
                25f * Time.deltaTime
            );
        }
    }

    // ================== LIMBS FOLLOW ==================
    void FollowLimbToAnchor(Transform limb, Transform anchor)
    {
        if (!limb || !anchor) return;

        limb.position = Vector3.Lerp(
            limb.position,
            anchor.position,
            limbFollow * Time.deltaTime
        );
    }

    // ================== COLISIONES (impact squash) ==================
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!bodySprite) return;
        if (collision.contactCount <= 0) return;

        ContactPoint2D c = collision.GetContact(0);
        collisionNormal = c.normal;
        collisionSquashTimer = collisionSquashDuration;
    }

    // ================== SQUEEZE ZONES ==================
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!useSqueezeZones) return;
        if (!other.CompareTag("SqueezeZone")) return;

        bodyCollider.offset = squeezedOffset;
        currentSqueezeZone = other;
        ActualizarSqueezeDesdeZona(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!useSqueezeZones) return;
        if (other != currentSqueezeZone) return;

        bodyCollider.offset = normalOffset;
        currentSqueezeZone = null;
        squeezeScaleTarget = Vector3.one; // volver a tamaño normal
    }

    void ActualizarSqueezeDesdeZona(Collider2D zona)
    {
        if (!bodySprite) return;
        if (baseBodySize.x <= 0f || baseBodySize.y <= 0f) return;

        Bounds zoneBounds = zona.bounds;

        float widthFactor  = zoneBounds.size.x / baseBodySize.x;
        float heightFactor = zoneBounds.size.y / baseBodySize.y;

        widthFactor  = Mathf.Clamp(widthFactor,  minSqueezeFactor, 1f);
        heightFactor = Mathf.Clamp(heightFactor, minSqueezeFactor, 1f);

        squeezeScaleTarget = new Vector3(widthFactor, heightFactor, 1f);
    }

    // ======== Gizmos útiles para ver el ground check ========
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 origin = transform.position + new Vector3(0f, groundCheckOffsetY, 0f);
        Gizmos.DrawWireSphere(origin, groundCheckRadius);
    }
}
