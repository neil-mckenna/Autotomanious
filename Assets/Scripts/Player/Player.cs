using UnityEngine;
using UnityEngine.InputSystem.XR;

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

        

        ResetPlayer();
        
    }

    private void Start()
    {
        gameManager = Object.FindAnyObjectByType<GameManager>();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(this);
        }
        else
        {
            Debug.LogError("GameManager instance not found! Player registration failed.");
            // Fallback: try to find GameManager
            GameManager gm = FindAnyObjectByType<GameManager>();
            if (gm != null)
            {
                gm.RegisterPlayer(this);
            }
        }


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
            if (guard != null && !guard.currentBrain.HasLineOfSightToPlayer())
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
            Die(other.gameObject.name);
        }
    }

    public void Die(string killerName)
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
            gameManager.PlayerDied($"Touched By Guard {killerName}");
        }
        else
        {
            Debug.LogError("CRITICAL: Cannot find GameManager when player dies!");
        }

        DisablePlayer();
    }

    private void DisablePlayer()
    {
        Drive drive = GetComponent<Drive>();
        if (drive != null)
            drive.enabled = false;
    }

    public void ResetPlayer()
    {
        isDead = false;

        Drive drive = GetComponent<Drive>();
        if (drive != null)
        {
            drive.enabled = true;
            //Debug.Log("Drive script re-enabled");
        }

        // Re-enable all scripts
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            script.enabled = true;
        }

        // Re-enable all renderers
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = true;
        }

        // Re-enable collider
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = true;

        //Debug.Log("Player reset and reactivated");
    }
}