using UnityEngine;
using UnityEngine.UI;

public class Grappling : MonoBehaviour
{
    // O grapple coordena o movimento especial, a linha visual e o estado do swinging.
    [Header("References")]
    private PlayerMovement pm;
    private Swinging swinging;
    public Transform cam;
    public Transform gunTip;
    public LayerMask whatIsGrappleable;
    public LineRenderer lr;

    [Header("Grappling")]
    public float maxGrappleDistance;
    public float gappleDelayTime;
    public float grappleStopDelay = 1f;
    public float overshootYAxis = 3f;

    // O ponto permanece fixo durante o disparo, enquanto a origem acompanha o GunTip.
    Vector3 grapplePoint;

    // O cooldown impede disparos consecutivos antes de a mecânica terminar de se reorganizar.
    [Header("Cooldown")]
    public float grappleCD = 0.25f;
    private float grappleCDTimer;

    [Header("Input")]
    public KeyCode grappleKey = KeyCode.Mouse1;

    [Header("Prediction")]
    public float predictionSphereRadius = 2f;

    // Outros sistemas podem consultar o estado, mas somente este script pode alterá-lo.
    private bool grappling;
    public bool IsGrappling => grappling;

    // A cor da mira comunica se existe alvo, se o grapple está disponível ou em cooldown.
    [Header("Crosshair")]
    public Image crosshair;
    public Color normalColor = new Color(1f, 1f, 1f, 0.65f);
    public Color grappleColor = Color.cyan;
    public Color cooldownColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);

    void Start()
    {
        // As três mecânicas ficam no mesmo Player para compartilhar seus estados diretamente.
        pm = GetComponent<PlayerMovement>();
        swinging = GetComponent<Swinging>();
    }

    void Update()
    {
        if (Input.GetKeyDown(grappleKey)) StartGrapple();

        // O timer é reduzido em tempo real e deixa de bloquear o disparo quando chega a zero.
        if (grappleCDTimer > 0) grappleCDTimer -= Time.deltaTime;

        UpdateCrosshair();
    }

    private void LateUpdate()
    {
        if (grappling)
        {
            // A extremidade presa fica fixa; somente a origem acompanha o movimento da arma.
            lr.SetPosition(0, gunTip.position);
        }
    }

    private void UpdateCrosshair()
    {
        if (crosshair == null) return;

        if (grappleCDTimer > 0)
        {
            // Durante o cooldown, a mira não deve sugerir que o disparo está disponível.
            crosshair.color = cooldownColor;
            return;
        }

        // A mira usa a mesma previsão do disparo para não indicar um alvo diferente do escolhido.
        bool canGrapple = TryGetGrapplePoint(out _);
        
        if (canGrapple)
        {
            crosshair.color = grappleColor;
        }
        else
        {
            crosshair.color = normalColor;
        }
    }

    private void StartGrapple()
    {
        if (grappleCDTimer > 0) return;

        // Somente uma mecânica de corda pode controlar o Player por vez.
        if (swinging != null)
        {
            swinging.StopSwinging();
        }

        grappling = true;

        // O movimento comum fica suspenso enquanto a linha viaja até o ponto escolhido.
        pm.freeze = true;

        if (TryGetGrapplePoint(out RaycastHit hit))
        {
            grapplePoint = hit.point;

            Invoke(nameof(ExecuteGrapple), gappleDelayTime);
        }
        else
        {
            // Um disparo sem alvo ainda desenha a tentativa, mas não aplica impulso.
            grapplePoint = cam.position + cam.forward * maxGrappleDistance;

            Invoke(nameof(StopGrapple), gappleDelayTime);
        }

        lr.enabled = true;
        lr.SetPosition(1, grapplePoint);
    }

    private bool TryGetGrapplePoint(out RaycastHit hit)
    {
        // O Raycast central tem prioridade para manter a mira precisa quando há um alvo direto.
        if (Physics.Raycast(
            cam.position,
            cam.forward,
            out hit,
            maxGrappleDistance,
            whatIsGrappleable
        ))
        {
            return true;
        }

        // O SphereCast funciona como assistência para superfícies próximas do centro da mira.
        return Physics.SphereCast(
            cam.position,
            predictionSphereRadius,
            cam.forward,
            out hit,
            maxGrappleDistance,
            whatIsGrappleable
        );
    }

    private void ExecuteGrapple()
    {
        // O PlayerMovement reassume o CharacterController para executar a trajetória calculada.
        pm.freeze = false;

        // Usar a base do personagem como referência produz um arco mais consistente visualmente.
        Vector3 lowestPoint = new Vector3(transform.position.x, transform.position.y - 1f, transform.position.z);

        float grapplePointRelativeYPos = grapplePoint.y - lowestPoint.y;
        float highestPointOnArc = grapplePointRelativeYPos + overshootYAxis;

        // Para alvos abaixo do Player, a altura extra ainda cria um lançamento perceptível.
        if (grapplePointRelativeYPos < 0) highestPointOnArc = overshootYAxis;

        pm.JumpToPosition(grapplePoint, highestPointOnArc);

        Invoke(nameof(StopGrapple), grappleStopDelay);
    }

    public void StopGrapple()
    {
        // Cancelar chamadas pendentes evita que um disparo encerrado volte a executar depois.
        CancelInvoke(nameof(ExecuteGrapple));
        CancelInvoke(nameof(StopGrapple));

        if (!grappling) return;

        // Todos os estados compartilhados são liberados antes de iniciar o cooldown.
        pm.freeze = false;
        pm.ResetRestrictions();
        grappling = false;

        grappleCDTimer = grappleCD;

        lr.enabled = false;
    }

}
