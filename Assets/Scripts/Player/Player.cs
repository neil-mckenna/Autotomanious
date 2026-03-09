using Unity.VisualScripting;
using UnityEngine;

public class Player : MonoBehaviour
{
    GameManager gameManager;
    private bool isDead = false;

    // safety to renable 
    private void OnEnable()
    {
        isDead = false;

        FindGameManager();

        Debug.Log("Player enabled - isDead reset to false");
    }

    private void Start()
    {
        gameManager = Object.FindAnyObjectByType<GameManager>();

    }

    private void FindGameManager()
    {
        gameManager = Object.FindAnyObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogWarning("GameManager not found, will try again later");
        }
    }

    // player die when guard hit them
    private void OnTriggerEnter(Collider other)
    {
        Debug.LogWarning($"Collided with {other.gameObject}");

        if(isDead) return;

        if(other.gameObject.CompareTag("Guard"))
        {
            Die();
        }

    }

    public void Die()
    {
        if(isDead) { return; }
        isDead = true;

        Debug.LogError("Player dead!");


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

        // disable instead of destroy as it causes issues with scene reload
        DisablePlayer();
    }

    private void DisablePlayer()
    {
        // Disable movement scripts, Drive
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != this) // Don't disable this Player script
                script.enabled = false;
        }

        // Hide player visually
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = false;
        }

        // Disable collider to prevent further interactions
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;
    }


}
