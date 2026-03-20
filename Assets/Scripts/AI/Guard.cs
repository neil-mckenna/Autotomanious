using System.Runtime.InteropServices.WindowsRuntime;
using UnityEditor.Timeline.Actions;
using UnityEngine;
using UnityEngine.AI;

// a guard class for the guard prefab
public class Guard : MonoBehaviour
{
    #region Properties

    [Header("Guard Settings")]
    [SerializeField] public AIBrain currentBrain;
    [SerializeField] protected float detectionRange = 10f;
    [SerializeField] protected float fieldOfView = 60f;

    [Header("Kill Settings")]
    [SerializeField] protected float killRange = 2f;

    [Header("Hearing Settings")] // NEW
    [SerializeField] protected float maxHearingDistance = 15f; // Guards can't hear beyond this
    [SerializeField] public float guardHearingSensitivity = 5f; // multiplayer for sound travel 

    [Header("References")]
    [SerializeField] protected Transform playerTransform;

    

    private bool hasKilledPlayer = false;

    public Transform PlayerTransform => currentBrain?.PlayerTransform;

    public float GetFieldOfView()
    {
        return fieldOfView;
    }

    #endregion

    #region Detection Changes
    public float GetDetectionRange() => detectionRange;



    public void SetDetectionRange(float newRange)
    {
        detectionRange = newRange;
    }

    #endregion

    #region Start

    private void Start()
    {
        //Debug.Log($"Guard.Start() - Agent exists: {GetComponent<NavMeshAgent>() != null}");
        //Debug.Log($"Guard.Start() - Brain exists: {currentBrain != null}");

        InitializeBrain();
    }

    private void InitializeBrain()
    {
        if (currentBrain != null)
        {
            currentBrain.Init(this);
        }
    }

    #endregion

    #region Update

    private void Update()
    {

        if (currentBrain != null)
        {
            currentBrain.Think();
        }

        // Debug current destination
        if (GetComponent<NavMeshAgent>() != null && GetComponent<NavMeshAgent>().hasPath)
        {
            Debug.DrawLine(transform.position, GetComponent<NavMeshAgent>().destination, Color.red);
        }

        DrawRaycastToPlayer();

        CheckForKill();
    }

    #endregion

    #region Player Targetting

    // convulted method but had a race condition with player re-spawning  
    public bool HasLineOfSightToPlayer()
    {
        if (!playerTransform) return false;

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;

        RaycastHit hit;
        bool raycastHit = Physics.Raycast(transform.position, directionToPlayer, out hit, detectionRange);

        if (raycastHit)
        {
            // Check if we hit grass FIRST - vision is blocked!
            if (hit.transform.CompareTag("Grass"))
            {
                //Debug.DrawLine(transform.position, hit.point, Color.yellow, 1.0f);
                //Debug.LogWarning($"Vision blocked by grass: {hit.transform.name}");
                return false; // Vision blocked by grass - CANNOT see player
            }

            // Otherwise check if we hit the player
            bool canSee = hit.transform == playerTransform;
            //Debug.DrawLine(transform.position, hit.point, canSee ? Color.green : Color.red, 1.0f);
            return canSee;
        }

        //Debug.DrawRay(transform.position, directionToPlayer * detectionRange, Color.magenta, 1.0f);
        return false;
    }

    // setter method to guard to target player
    public void SetPlayer(Transform player)
    {
        playerTransform = player;
    }

    // find player
    public void FindPlayer()
    {
        // Try to find player by name
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            //Debug.Log($"Guard found player: {player.name} at {player.transform.position}");
        }
        else
        {
            //Debug.LogWarning("Guard couldn't find player yet - will try again later");
        }
    }

    // this was to help me debug initially 
    private void DrawRaycastToPlayer()
    {
        if (playerTransform == null) return;

        // Calculate direction and distance
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // Set color based on what the raycast hits
        Color rayColor = Color.gray; // Default

        // Perform the raycast
        if (Physics.Raycast(transform.position, directionToPlayer, out RaycastHit hit, detectionRange))
        {
            if (hit.transform == playerTransform)
            {
                rayColor = Color.green; // Can see player!

                // Draw additional info
                //Debug.Log($"Can see player! Distance: {distanceToPlayer:F2}, Angle: {Vector3.Angle(transform.forward, directionToPlayer):F2}°");
            }
            else
            {
                rayColor = Color.yellow; // Hit something else

                // Draw a small sphere at the hit point
                //Debug.DrawRay(transform.position, directionToPlayer * hit.distance, rayColor, 0f);
                //Debug.DrawLine(hit.point, playerTransform.position, Color.magenta, 0f);

                // Log what's blocking
                //Debug.Log($" Raycast hit: {hit.transform.name} (not player)");
            }
        }
        else
        {
            rayColor = Color.red; // Hit nothing (or beyond range)

            // Draw the full ray
            Debug.DrawRay(transform.position, directionToPlayer * detectionRange, rayColor, 0f);

            if (distanceToPlayer > detectionRange)
            {
                //Debug.Log($" Player too far: {distanceToPlayer:F2} > {detectionRange}");
            }
            else
            {
                //Debug.Log($" Raycast hit nothing - check colliders/layers");
            }
        }

        // Draw the main ray
        Debug.DrawRay(transform.position, directionToPlayer * Mathf.Min(distanceToPlayer, detectionRange), rayColor, 0f);
    }

    #endregion

    #region Hear player
    public void HearNoise(Vector3 noisePosition, float noiseRadius)
    {
        // Calculate distance to noise
        float distanceToNoise = Vector3.Distance(transform.position, noisePosition);

        // Check if within max hearing range
        if (distanceToNoise > maxHearingDistance)
        {
            return;
        }

        // Calculate effective radius
        float effectiveRadius = Mathf.Min(noiseRadius, maxHearingDistance);

        // Check if within effective noise radius
        if (distanceToNoise <= effectiveRadius)
        {
            Debug.Log($" Guard {gameObject.name} HEARD noise at distance {distanceToNoise}");

            //// Check if already chasing
            //if (HasLineOfSightToPlayer())
            //{
            //    Debug.Log("Already sees player - chasing instead");
            //    return;
            //}

            // FIX: ALWAYS go suspicious regardless of grass!
            Debug.Log($" Setting SUSPICIOUS state (player in grass: {IsPlayerInGrass()})");

            if (currentBrain is BTBrain btBrain)
            {
                btBrain.SetSuspicious(noisePosition, Random.Range(1f, 5f));
            }
            else if (currentBrain is FSM fsmBrain)
            {
                fsmBrain.SetSuspicious(noisePosition, Random.Range(1f, 5f));
            }

            // Visual feedback - purple for ALL noise reactions
            Debug.DrawLine(transform.position, noisePosition, Color.magenta, 3f);
        }
    }

    // Helper method to check if player is in grass
    private bool IsPlayerInGrass()
    {
        if (playerTransform == null) return false;

        // Check if player is touching grass trigger
        Collider[] playerColliders = playerTransform.GetComponentsInChildren<Collider>();
        foreach (Collider col in playerColliders)
        {
            // Check if any collider is overlapping with grass
            Collider[] overlaps = Physics.OverlapSphere(col.bounds.center, 0.1f);
            foreach (Collider overlap in overlaps)
            {
                if (overlap.CompareTag("Grass"))
                {
                    return true;
                }
            }
        }

        // Alternative: Raycast down to check grass under player
        RaycastHit hit;
        if (Physics.Raycast(playerTransform.position, Vector3.down, out hit, 2f))
        {
            if (hit.collider.CompareTag("Grass"))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Kill Player

    private void CheckForKill()
    {
        if (hasKilledPlayer) return;
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer <= killRange)
        {
            KillPlayer();
        }
    }

    private void KillPlayer()
    {
        if (hasKilledPlayer) return;

        hasKilledPlayer = true;
        Debug.Log($"{gameObject.name} caught and killed the player!");

        if (playerTransform != null)
        {
            Player player = playerTransform.GetComponent<Player>();
            if (player != null)
            {
                player.Die();
            }
        }
    }

    #endregion


    #region Debug

    // For debugging
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw field of view
        Vector3 leftBoundary = Quaternion.Euler(0, -fieldOfView, 0) * transform.forward * detectionRange;
        Vector3 rightBoundary = Quaternion.Euler(0, fieldOfView, 0) * transform.forward * detectionRange;

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, leftBoundary);
        Gizmos.DrawRay(transform.position, rightBoundary);
    }

    // Enhanced Gizmos for Scene view
    private void OnDrawGizmos()
    {
        // NEW: Draw hearing range sphere
        Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.1f); // Semi-transparent purple
        Gizmos.DrawWireSphere(transform.position, maxHearingDistance);

        // kill zone
        Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, killRange);

        // Draw detection range sphere
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f); // Semi-transparent yellow
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw FOV cone
        float halfFOV = fieldOfView / 2f;
        Vector3 leftBoundary = Quaternion.Euler(0, -halfFOV, 0) * transform.forward * detectionRange;
        Vector3 rightBoundary = Quaternion.Euler(0, halfFOV, 0) * transform.forward * detectionRange;

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);

        // Draw filled FOV arc (only in Scene view, not Game view)
#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(0f, 0f, 1f, 0.1f);
        UnityEditor.Handles.DrawSolidArc(
            transform.position,
            Vector3.up,
            Quaternion.Euler(0, -halfFOV, 0) * transform.forward,
            fieldOfView,
            detectionRange
        );
#endif

        // Draw line to player with state info
        if (playerTransform != null)
        {
            Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToPlayer);
            float distance = Vector3.Distance(transform.position, playerTransform.position);

            // Color based on detection conditions
            Gizmos.color = Color.gray;
            if (distance <= detectionRange && angle <= fieldOfView)
            {
                // Within range and FOV - do raycast to check actual line of sight
                if (Physics.Raycast(transform.position, directionToPlayer, out RaycastHit hit, detectionRange))
                {
                    Gizmos.color = hit.transform == playerTransform ? Color.green : Color.yellow;
                }
                else
                {
                    Gizmos.color = Color.red;
                }
            }
            else if (distance > detectionRange)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f); // Orange for out of range
            }
            else if (angle > fieldOfView)
            {
                Gizmos.color = Color.cyan; // Out of FOV
            }

            Gizmos.DrawLine(transform.position, playerTransform.position);

            // Draw distance and angle text in Scene view
#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 3,
                $"Dist: {distance:F1}\nAngle: {angle:F1}°\nRange: {detectionRange}\nFOV: {fieldOfView}°"
            );
#endif
        }
    }

    #endregion
}
