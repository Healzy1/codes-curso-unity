using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    CharacterController controller;

    Vector3 forward;
    Vector3 strafe;
    Vector3 direction = Vector3.zero;

    public float walkSpeed = 9f;

    Vector3 horizontalVelocity;
    Vector3 verticalVelocity;

    float gravityUp;
    float gravityDown;
    float jumpVelocity;

    public float sprintSpeed = 14f;
    public float groundAcceleration = 60f;
    public float groundDeceleration = 80f;
    public float landingDeceleration = 220f;
    public float stopSpeedThreshold = 0.15f;

    public float jumpHeight = 2.4f;
    public float timeToApex = 0.42f;
    public float fallMultiplier = 1.7f;
    public float groundedStickVelocity = -2f;

    public float coyoteTime = 0.12f;
    float coyoteTimeCounter;

    public float jumpBufferTime = 0.15f;
    float jumpBufferCounter;

    public float airAcceleration = 25f;

    [Range(0f, 1f)]
    public float airControl = 0.65f;

    public bool freeze;
    public bool activeGrapple;
    public float grappleControlDuration = 0.35f;
    public float grappleCollisionGraceTime = 0.1f;
    public float grappleHorizontalStrength = 1.15f;
    public float grappleVerticalStrength = 1.15f;
    float grappleControlTimer;
    float grappleCollisionTimer;

    [Header("Camera Effects")]
    public UnityEngine.Camera playerCamera;
    public float normalFOV = 60f;
    public float grappleFOV = 75f;
    public float fovChangeSpeed = 8f;
    float targetFOV;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        CalculateJumpValues();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<UnityEngine.Camera>();
        }

        if (playerCamera != null)
        {
            normalFOV = playerCamera.fieldOfView;
            targetFOV = normalFOV;
        }
    }

    void Update()
    {
        UpdateCameraFOV();

        if (freeze)
        {
            horizontalVelocity = Vector3.zero;
            verticalVelocity = Vector3.zero;
            return;
        }

        if (activeGrapple)
        {
            MoveDuringGrapple();
            return;
        }

        bool isGrounded = controller.isGrounded;

        float forwardInput = Input.GetAxisRaw("Vertical");
        float strafeInput = Input.GetAxisRaw("Horizontal");

        forward = forwardInput * transform.forward;
        strafe = strafeInput * transform.right;
        direction = (forward + strafe).normalized;

        float targetSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            targetSpeed = sprintSpeed;
        }
        else
        {
            targetSpeed = walkSpeed;
        }

        Vector3 targetHorizontalVelocity = direction * targetSpeed;

        bool hasMovementInput = direction.sqrMagnitude > 0f;
        float currentAcceleration;
        
        if (isGrounded)
        {
            if (hasMovementInput)
            {
                currentAcceleration = groundAcceleration;
            }
            else
            {
                currentAcceleration = groundDeceleration;
            }
        }
        else
        {
            currentAcceleration = airAcceleration;

            if (hasMovementInput)
            {
                Vector3 desiredAirVelocity = direction * targetSpeed;
                targetHorizontalVelocity = Vector3.Lerp(horizontalVelocity, desiredAirVelocity, airControl);
            }
            else
            {
                targetHorizontalVelocity = horizontalVelocity;
            }
        }

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetHorizontalVelocity,
            currentAcceleration * Time.deltaTime
        );

        if (isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = groundedStickVelocity;
        }

        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            verticalVelocity.y = jumpVelocity;
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
        }

        float currentGravity;
        if (verticalVelocity.y > 0f)
        {
            currentGravity = gravityUp;
        }
        else
        {
            currentGravity = gravityDown;
        }
        verticalVelocity.y += currentGravity * Time.deltaTime;

        Vector3 finalVelocity = horizontalVelocity + verticalVelocity;

        CollisionFlags collisionFlags = controller.Move(finalVelocity * Time.deltaTime);
        bool groundedAfterMove = (collisionFlags & CollisionFlags.Below) != 0;

        if (verticalVelocity.y > 0 && (collisionFlags & CollisionFlags.Above) != 0)
        {
            verticalVelocity = Vector3.zero;
        }

        bool wasGrounded = controller.isGrounded;

        bool landedThisFrame = !wasGrounded && groundedAfterMove;

        if (landedThisFrame && !hasMovementInput)
        {
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                Vector3.zero,
                landingDeceleration * Time.deltaTime
            );

            if (horizontalVelocity.sqrMagnitude < stopSpeedThreshold * stopSpeedThreshold)
            {
                horizontalVelocity = Vector3.zero;
            }
        }

        wasGrounded = groundedAfterMove;
    }

    void CalculateJumpValues()
    {
        float safeJumpHeight = Mathf.Max(jumpHeight, 0.1f);
        float safeTimeToApex = Mathf.Max(timeToApex, 0.1f);
        float safeFallMultiplier = Mathf.Max(fallMultiplier, 1f);

        gravityUp = (-2f * safeJumpHeight) / Mathf.Pow(safeTimeToApex, 2f);
        gravityDown = gravityUp * safeFallMultiplier;
        jumpVelocity = (2f * safeJumpHeight) / safeTimeToApex;
    }

    void OnValidate()
    {
        airControl = Mathf.Clamp01(airControl);
        CalculateJumpValues();
    }

    private bool enableMovementOnNextTouch;

    public void ResetRestrictions()
    {
        activeGrapple = false;
        enableMovementOnNextTouch = false;
        targetFOV = normalFOV;
    }

    public void JumpToPosition(Vector3 targetPosition, float trajectoryHeight)
    {
        activeGrapple = true;
        enableMovementOnNextTouch = true;
        grappleControlTimer = grappleControlDuration;
        grappleCollisionTimer = 0f;
        targetFOV = grappleFOV;

        Vector3 launchVelocity = CalculateJumpVelocity(transform.position, targetPosition, trajectoryHeight);

        horizontalVelocity = new Vector3(launchVelocity.x, 0f, launchVelocity.z) * grappleHorizontalStrength;
        verticalVelocity = new Vector3(0f, launchVelocity.y, 0f) * grappleVerticalStrength;

        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
    }

    public void SetVelocity(Vector3 velocity)
    {
        horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        verticalVelocity = new Vector3(0f, velocity.y, 0f);

        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
        activeGrapple = false;
        freeze = false;
    }

    void MoveDuringGrapple()
    {
        float currentGravity;
        if (verticalVelocity.y > 0f)
        {
            currentGravity = gravityUp;
        }
        else
        {
            currentGravity = gravityDown;
        }

        verticalVelocity.y += currentGravity * Time.deltaTime;

        Vector3 finalVelocity = horizontalVelocity + verticalVelocity;
        CollisionFlags collisionFlags = controller.Move(finalVelocity * Time.deltaTime);
        grappleCollisionTimer += Time.deltaTime;

        if (verticalVelocity.y > 0 && (collisionFlags & CollisionFlags.Above) != 0)
        {
            verticalVelocity = Vector3.zero;
        }

        if (enableMovementOnNextTouch && grappleCollisionTimer > grappleCollisionGraceTime && collisionFlags != CollisionFlags.None)
        {
            ResetRestrictions();

            Grappling grappling = GetComponent<Grappling>();
            if (grappling != null)
            {
                grappling.StopGrapple();
            }

            return;
        }

        grappleControlTimer -= Time.deltaTime;
        if (grappleControlTimer <= 0f)
        {
            activeGrapple = false;
        }
    }

    Vector3 CalculateJumpVelocity(Vector3 startPoint, Vector3 endPoint, float trajectoryHeight)
    {
        float displacementY = endPoint.y - startPoint.y;
        float safeTrajectoryHeight = Mathf.Max(trajectoryHeight, displacementY + 0.1f, 0.1f);
        float gravity = gravityUp;
        Vector3 displacementXZ = new Vector3(endPoint.x - startPoint.x, 0f, endPoint.z - startPoint.z);

        Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2f * gravity * safeTrajectoryHeight);
        Vector3 velocityXZ = displacementXZ / (
            Mathf.Sqrt(-2f * safeTrajectoryHeight / gravity)
            + Mathf.Sqrt(2f * (displacementY - safeTrajectoryHeight) / gravity)
        );

        return velocityXZ + velocityY;
    }

    void UpdateCameraFOV()
    {
        if (playerCamera == null) return;

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            fovChangeSpeed * Time.deltaTime
        );
    }
}
