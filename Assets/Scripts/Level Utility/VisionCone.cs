using UnityEngine;

public class VisionCone : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float heightOffset = 0.5f;
    [SerializeField] private float distanceScale = 0.3f;
    [SerializeField] private bool testMode = false;  //  Set to false for normal mode

    [Header("Colors")]
    [SerializeField] private Color idleColor = new Color(1f, 0.5f, 0f, 0.2f);
    [SerializeField] private Color chasingColor = new Color(1f, 0f, 0f, 0.4f);
    [SerializeField] private Color blockedColor = new Color(0.5f, 0.5f, 0.5f, 0.2f); //  Grey when blocked

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private Guard guard;
    private SpriteRenderer spriteRenderer;
    private Transform coneVisual;

    private void Start()
    {
        guard = GetComponentInParent<Guard>();

        if (transform.childCount > 0)
        {
            coneVisual = transform.GetChild(0);
            spriteRenderer = coneVisual.GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null)
        {
            Debug.LogError("VisionCone: No SpriteRenderer found on child!");
            return;
        }

        spriteRenderer.color = idleColor;

        //  Disable test mode
        testMode = false;

        Debug.Log("VisionCone initialized");
    }

    private void Update()
    {
        if (spriteRenderer == null || guard == null) return;

        if (testMode)
        {
            // Test mode - just spin
            transform.Rotate(Vector3.up, 30 * Time.deltaTime);
            float pulse = Mathf.Sin(Time.time * 2f) * 0.2f + 1f;
            coneVisual.localScale = Vector3.one * (2f * pulse);
        }
        else
        {
            // Normal mode - follow guard
            transform.position = guard.transform.position + Vector3.up * heightOffset;
            transform.rotation = guard.transform.rotation;

            float range = guard.GetDetectionRange();
            coneVisual.localScale = Vector3.one * (range * distanceScale);

            //  Get brain reference
            AIBrain brain = guard.currentBrain;

            //  Determine if player can be seen
            bool canSeePlayer = false;
            string reason = "";

            if (brain != null)
            {
                Transform player = brain.Player.transform;
                if (player != null)
                {
                    float distance = Vector3.Distance(transform.position, player.position);
                    Vector3 directionToPlayer = (player.position - transform.position).normalized;
                    float angle = Vector3.Angle(transform.forward, directionToPlayer);
                    float halfFOV = guard.GetFieldOfView() / 2f;

                    // Check conditions
                    if (distance > range)
                    {
                        reason = $"Too far: {distance:F1} > {range}";
                    }
                    else if (angle > halfFOV)
                    {
                        reason = $"Out of FOV: {angle:F1}° > {halfFOV}°";
                    }
                    else
                    {
                        // Check line of sight
                        RaycastHit hit;
                        if (Physics.Raycast(transform.position, directionToPlayer, out hit, distance))
                        {
                            if (hit.transform == player)
                            {
                                canSeePlayer = true;
                                reason = "Can see!";
                            }
                            else if (hit.transform.CompareTag("Grass"))
                            {
                                reason = $"Blocked by grass: {hit.transform.name}";
                            }
                            else
                            {
                                reason = $"Blocked by: {hit.transform.name}";
                            }
                        }
                        else
                        {
                            reason = "No line of sight";
                        }
                    }

                    //  Update color based on whether player can be seen
                    if (canSeePlayer)
                    {
                        spriteRenderer.color = chasingColor;
                    }
                    else if (reason.Contains("Blocked"))
                    {
                        spriteRenderer.color = blockedColor;
                    }
                    else
                    {
                        spriteRenderer.color = idleColor;
                    }

                    //  Debug output every second
                    if (showDebug && Time.frameCount % 60 == 0)
                    {
                        Debug.Log($"=== VisionCone ===");
                        Debug.Log($"Distance: {distance:F1} / Range: {range}");
                        Debug.Log($"Angle: {angle:F1}° / Half FOV: {halfFOV}°");
                        Debug.Log($"Can see: {canSeePlayer} - {reason}");
                        Debug.Log($"Guard state: {GetGuardState()}");
                    }
                }
                else
                {
                    if (showDebug && Time.frameCount % 60 == 0)
                        Debug.Log("VisionCone: Player is NULL!");
                }
            }
        }
    }

    private string GetGuardState()
    {
        if (guard.currentBrain is BTBrain bt)
            return bt.GetCurrentAction();
        if (guard.currentBrain is FSM fsm)
            return fsm.GetCurrentState();
        if (guard.currentBrain is Zombie zombie)
            return zombie.IsChasing() ? "Chasing" : "Idle";
        return "Unknown";
    }
}