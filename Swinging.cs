using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Swinging : MonoBehaviour
{
    // O botão principal mantém a corda ativa; os outros controlam seu comprimento.
    [Header("Input")]
    public KeyCode swingKey = KeyCode.Mouse0;
    public KeyCode shortenCableKey = KeyCode.Space;
    public KeyCode extendCableKey = KeyCode.LeftControl;

    [Header("References")]
    public LineRenderer lr;
    public Transform gunTip;
    public Transform cam;
    public Transform player;
    public LayerMask whatIsGrappleable;

    [Header("Swinging")]
    public float maxSwingDistance = 25f;
    public float swingGravity = 35f;
    public float maxSwingSpeed = 28f;
    public float ropeShortenSpeed = 8f;
    public float exitSpeedMultiplier = 1f;

    // O swinging move diretamente o mesmo CharacterController usado pelo movimento normal.
    private CharacterController controller;
    private PlayerMovement pm;
    private Grappling grappling;

    // O ponto é fixo; a posição visual e a velocidade evoluem durante o balanço.
    private Vector3 swingPoint;
    private Vector3 currentGrapplePosition;
    private Vector3 swingVelocity;

    // O comprimento funciona como limite máximo, permitindo que a corda fique frouxa.
    private float ropeLength;
    private bool swinging;

    // Esses impulsos dão controle ao jogador sem substituir a dinâmica da corda.
    [Header("Odm Gear")]
    public Transform orientation;
    public float horizontalThrustForce = 25f;
    public float forwardThrustForce = 35f;
    public float pullToPointForce = 25f;
    public float extendCableSpeed = 20f;

    // A previsão combina precisão central com uma margem de assistência ao redor da mira.
    [Header("Prediction")]
    public RaycastHit predictionHit;
    public float predictionSphereRadius;
    public Transform predictionPoint;
    private bool hasPredictionHit;

    void Start()
    {
        // Os componentes permanecem no Player para compartilhar movimento e estados especiais.
        controller = GetComponent<CharacterController>();
        pm = GetComponent<PlayerMovement>();
        grappling = GetComponent<Grappling>();

        // O próprio objeto é uma referência segura quando o campo não foi preenchido no Inspector.
        if (player == null)
        {
            player = transform;
        }
    }

    void Update()
    {
        // A previsão é atualizada antes do input para o clique usar o alvo do frame atual.
        CheckForSwingPoints();

        if (Input.GetKeyDown(swingKey)) StartSwinging();
        if (Input.GetKeyUp(swingKey)) StopSwinging();

        if (swinging)
        {
            // Primeiro acumulamos o controle do jogador; depois aplicamos a restrição da corda.
            OdmGearMovement();
            MoveSwinging();
        }
    }

    void LateUpdate()
    {
        // A linha é desenhada depois do movimento para acompanhar as posições finais do frame.
        DrawRope();
    }

    private void StartSwinging()
    {
        if (!hasPredictionHit) return;

        // O swing assume o controle e encerra qualquer grapple de impulso ainda ativo.
        if (grappling != null)
        {
            grappling.StopGrapple();
        }

        if (pm != null)
        {
            pm.ResetRestrictions();
        }

        // A distância inicial vira o comprimento da corda para evitar reposicionar o Player.
        swingPoint = predictionHit.point;
        ropeLength = Vector3.Distance(player.position, swingPoint);
        currentGrapplePosition = gunTip.position;

        // Preservar a velocidade de entrada faz corrida, pulo e grapple alimentarem o balanço.
        swingVelocity = controller.velocity;
        swinging = true;

        // Enquanto o swing está ativo, apenas este script deve mover o CharacterController.
        if (pm != null)
        {
            pm.freeze = true;
        }

        if (lr != null)
        {
            lr.enabled = true;
            lr.positionCount = 2;
        }

        if (predictionPoint != null)
        {
            predictionPoint.gameObject.SetActive(false);
        }
    }

    public void StopSwinging()
    {
        if (!swinging) return;

        swinging = false;

        // A velocidade acumulada volta ao movimento normal para conservar o momentum na saída.
        if (pm != null)
        {
            pm.SetVelocity(swingVelocity * exitSpeedMultiplier);
        }

        if (lr != null)
        {
            lr.positionCount = 0;
            lr.enabled = false;
        }
    }

    private void MoveSwinging()
    {
        // A gravidade gera a queda que transforma a restrição da corda em movimento pendular.
        swingVelocity += Vector3.down * swingGravity * Time.deltaTime;

        Vector3 anchorToPlayer = player.position - swingPoint;
        float distanceFromAnchor = anchorToPlayer.magnitude;

        // A correção só acontece quando o Player tenta ultrapassar o comprimento da corda.
        if (distanceFromAnchor > ropeLength)
        {
            Vector3 ropeDirection = anchorToPlayer.normalized;

            // O produto escalar isola a parte da velocidade que aponta para fora da âncora.
            float outwardSpeed = Vector3.Dot(swingVelocity, ropeDirection);

            if (outwardSpeed > 0f)
            {
                // Removemos somente a velocidade radial; a componente tangencial mantém o balanço.
                swingVelocity -= ropeDirection * outwardSpeed;
            }

            // Recoloca o Player exatamente no limite permitido sem alterar a posição da âncora.
            Vector3 targetPosition = swingPoint + ropeDirection * ropeLength;
            Vector3 correction = targetPosition - player.position;
            controller.Move(correction);
        }

        // O limite protege o controle contra aceleração contínua dos impulsos e da gravidade.
        swingVelocity = Vector3.ClampMagnitude(swingVelocity, maxSwingSpeed);
        CollisionFlags flags = controller.Move(swingVelocity * Time.deltaTime);

        // Ao bater no teto, cancelar a subida evita pressionar o controller contra a geometria.
        if ((flags & CollisionFlags.Above) != 0 && swingVelocity.y > 0f)
        {
            swingVelocity.y = 0f;
        }
    }

    void DrawRope()
    {
        if (!swinging || lr == null) return;

        // O Lerp simula a linha viajando do GunTip até o ponto de conexão.
        currentGrapplePosition = Vector3.Lerp(currentGrapplePosition, swingPoint, Time.deltaTime * 8f);

        lr.SetPosition(0, gunTip.position);
        lr.SetPosition(1, currentGrapplePosition);
    }

    private void OdmGearMovement()
    {
        // O corpo define frente e lados horizontais; a câmera é apenas um fallback.
        Transform movementOrientation = orientation != null ? orientation : cam;

        // Projetar no plano remove a inclinação da câmera e impede impulsos verticais acidentais.
        Vector3 rightDirection = Vector3.ProjectOnPlane(movementOrientation.right, Vector3.up).normalized;
        Vector3 forwardDirection = Vector3.ProjectOnPlane(movementOrientation.forward, Vector3.up).normalized;

        if (Input.GetKey(KeyCode.D))
        {
            swingVelocity += rightDirection * horizontalThrustForce * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.A))
        {
            swingVelocity -= rightDirection * horizontalThrustForce * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.W))
        {
            swingVelocity += forwardDirection * forwardThrustForce * Time.deltaTime;
        }
        
        if (Input.GetKey(shortenCableKey))
        {
            // Encurtar combina uma força em direção à âncora com a redução física do limite.
            Vector3 directionToPoint = swingPoint - player.position;
            swingVelocity += directionToPoint.normalized * pullToPointForce * Time.deltaTime;

            ropeLength -= ropeShortenSpeed * Time.deltaTime;

            // Um comprimento mínimo evita colapsar o Player exatamente sobre a âncora.
            ropeLength = Mathf.Max(ropeLength, 2f);
        }

        if (Input.GetKey(extendCableKey))
        {
            ropeLength += extendCableSpeed * Time.deltaTime;

            // A corda nunca ultrapassa o mesmo alcance usado para encontrar o ponto.
            ropeLength = Mathf.Min(ropeLength, maxSwingDistance);
        }
    }

    private void CheckForSwingPoints()
    {
        if (swinging)
        {
            // Durante o balanço, o ponto atual já está definido e uma nova previsão é desnecessária.
            hasPredictionHit = false;

            if (predictionPoint != null)
            {
                predictionPoint.gameObject.SetActive(false);
            }

            return;
        }

        bool grappleIsActive = grappling != null && grappling.IsGrappling;

        // O Raycast representa o centro exato da mira e sempre recebe prioridade.
        bool hasRaycastHit = Physics.Raycast(
            cam.position,
            cam.forward,
            out RaycastHit raycastHit,
            maxSwingDistance,
            whatIsGrappleable
        );

        // O SphereCast oferece uma margem de seleção para alvos próximos do centro.
        bool hasSphereCastHit = Physics.SphereCast(
            cam.position,
            predictionSphereRadius,
            cam.forward,
            out RaycastHit sphereCastHit,
            maxSwingDistance,
            whatIsGrappleable
        );

        if (hasRaycastHit)
        {
            predictionHit = raycastHit;
            hasPredictionHit = true;
        }
        else if (hasSphereCastHit)
        {
            // A assistência só é usada quando o centro da mira não encontrou uma superfície.
            predictionHit = sphereCastHit;
            hasPredictionHit = true;
        }
        else
        {
            predictionHit = default;
            hasPredictionHit = false;
        }

        if (predictionPoint != null)
        {
            // A busca continua durante o grapple para permitir troca imediata, mas o indicador fica oculto.
            predictionPoint.gameObject.SetActive(hasPredictionHit && !grappleIsActive);

            if (hasPredictionHit)
            {
                predictionPoint.position = predictionHit.point;
            }
        }
    }
}
