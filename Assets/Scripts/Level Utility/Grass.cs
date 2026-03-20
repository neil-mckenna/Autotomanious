using UnityEngine;

public class Grass : MonoBehaviour
{
    [Header("Grass Settings")]
    [SerializeField] private float detectionReductionFactor = 0.5f; // 50% reduction
    [SerializeField] private string playerTag = "Player";

    private bool playerInGrass = false;

    // unity triggers on collider
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            EnterGrass();
        }
    }

    // unity triggers on collider
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            ExitGrass();
        }
    }

    // trigger methid on entry
    private void EnterGrass()
    {
        if (playerInGrass) return;
        playerInGrass = true;

        Debug.LogWarning("Player entered grass - detection reduced!");
        ApplyDetectionModifier(true);
    }

    // trigger on exit
    private void ExitGrass()
    {
        if (!playerInGrass) return;
        playerInGrass = false;

        Debug.LogWarning("Player exited grass - detection normal");
        ApplyDetectionModifier(false);
    }

    // the modifer main method
    private void ApplyDetectionModifier(bool inGrass)
    {
        // grab all guard array ina greedy search 
        Guard[] guards = FindObjectsByType<Guard>(FindObjectsSortMode.None);

        // loop 
        foreach (Guard guard in guards)
        {
            // the modifer is on the prefab
            GrassModifier modifier = guard.GetComponent<GrassModifier>();

            if (inGrass)
            {
                // Store original if not already stored
                if (modifier != null && modifier.originalDetectionRange == 0)
                {
                    modifier.originalDetectionRange = guard.GetDetectionRange();
                }

                // Apply reduction, intially 0.5
                float newRange = guard.GetDetectionRange() * detectionReductionFactor;
                guard.SetDetectionRange(newRange);
            }
            else
            {
                // Restore original
                if (modifier != null && modifier.originalDetectionRange > 0)
                {
                    guard.SetDetectionRange(modifier.originalDetectionRange);
                }
            }
        }
    }

    // when scene die or reloads
    private void OnDestroy()
    {
        if (playerInGrass)
        {
            ApplyDetectionModifier(false);
        }
    }
}
