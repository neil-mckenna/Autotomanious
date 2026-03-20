using UnityEngine;

public class Player : MonoBehaviour
{
    private GameManager gameManager;
    private bool isDead = false;

    // Reference to Drive script for movement info
    private Drive drive;

    private void OnEnable()
    {
        isDead = false;
        FindGameManager();
        //Debug.Log("Player enabled - isDead reset to false");
    }

    private void Start()
    {
        gameManager = Object.FindAnyObjectByType<GameManager>();
        drive = GetComponent<Drive>();

        if (drive == null)
        {
            Debug.LogWarning("Drive script not found on player!");
        }
    }

    private void FindGameManager()
    {
        gameManager = Object.FindAnyObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogWarning("GameManager not found, will try again later");
        }
    }

    // Called by Drive script when player makes noise
    public void MakeNoise(Vector3 position, float radius, string noiseType)
    {
        if (isDead) return;

        Debug.Log($" Player.MakeNoise called at {position} with radius {radius}");

        // Find all nearby guards
        Collider[] nearbyColliders = Physics.OverlapSphere(position, radius);

        foreach (Collider col in nearbyColliders)
        {
            Guard guard = col.GetComponent<Guard>();
            if (guard != null && !guard.HasLineOfSightToPlayer())
            {
                // Guard hears the noise!
                if (guard.currentBrain is BTBrain btBrain)
                {
                    btBrain.SetSuspicious(position, 2f);
                }
                else if (guard.currentBrain is FSM fsmBrain)
                {
                    fsmBrain.SetSuspicious(position, 2f);
                }

                Debug.Log($"Guard alerted by player {noiseType} noise");
            }
        }
    }

    // player die when guard hit them
    private void OnTriggerEnter(Collider other)
    {
        //Debug.LogWarning($"Collided with {other.gameObject}");

        if (isDead) return;

        if (other.gameObject.CompareTag("Guard"))
        {
            Die();
        }
    }

    public void Die()
    {
        if (isDead) { return; }
        isDead = true;

        Debug.Log("Player dead!");

        if (gameManager == null)
        {
            gameManager = Object.FindAnyObjectByType<GameManager>();
        }

        if (gameManager != null)
        {
            gameManager.PlayerDied("Touched By Guard");
        }
        else
        {
            Debug.LogError("CRITICAL: Cannot find GameManager when player dies!");
        }

        DisablePlayer();
    }

    private void DisablePlayer()
    {
        // Disable movement scripts (Drive)
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != this) // Don't disable this Player script
                script.enabled = false;
        }

        // Disable CharacterController
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        // Hide player visually
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = false;
        }

        // Disable collider
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;
    }
}