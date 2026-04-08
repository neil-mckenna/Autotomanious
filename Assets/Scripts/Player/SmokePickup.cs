using UnityEngine;

// ============================================================================
// SMOKE PICKUP - COLLECTIBLE ITEM FOR SMOKE BOMB GRENADES
// ============================================================================
// 
// This script handles smoke bomb ammunition pickups in the game world.
// When the player touches the pickup, they receive smoke bombs.
//
// FEATURES:
// - Configurable amount of smoke bombs to add (1-3 typically)
// - Audio feedback when picked up
// - Visual effect when picked up (particle system or similar)
// - Automatic destruction after pickup
// - Debug logging for troubleshooting
//
// INTEGRATION:
// - Works with PlayerWeapon component
// - PlayerWeapon must have AddSmokeBombs() method
// - Can be placed in scene or spawned dynamically
//
// DIFFERENCES FROM FLASH PICKUP:
// - Adds smoke bombs instead of flashbangs
// - Grey color coding for debug logs (distinct from flash pickup)
//
// ============================================================================

public class SmokePickup : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - CONFIGURE IN UNITY INSPECTOR
    // ========================================================================

    [Header("=== PICKUP SETTINGS ===")]
    [Tooltip("Number of smoke bombs to add when picked up (usually 1-3)")]
    [SerializeField] private int amountToAdd = 1;

    [Header("=== AUDIO ===")]
    [Tooltip("Sound effect played when picked up")]
    [SerializeField] private AudioClip pickupSound;

    [Header("=== VISUAL EFFECTS ===")]
    [Tooltip("Particle effect or visual prefab to spawn on pickup")]
    [SerializeField] private GameObject pickupEffect;

    // ========================================================================
    // UNITY LIFECYCLE METHODS
    // ========================================================================

    void Start()
    {
        // Log pickup spawn location for debugging
        // Useful for verifying pickups are placed correctly in the scene
        //Debug.Log($"<color=grey>[SmokePickup] {gameObject.name} ready at {transform.position}</color>");
    }

    // ========================================================================
    // COLLISION HANDLING
    // ========================================================================

    /// <summary>
    /// Handles trigger collision with the player.
    /// When player touches the pickup, adds smoke bombs and destroys the item.
    /// </summary>
    /// <param name="other">The collider that entered the trigger</param>
    void OnTriggerEnter(Collider other)
    {
        // Debug: Log trigger entry (commented out to reduce console spam)
        // Uncomment for troubleshooting pickup detection issues
        // Debug.Log($"<color=blue>[SmokePickup] {other.gameObject.name} entered trigger</color>");

        // Try to get PlayerWeapon component from the entering object
        PlayerWeapon playerWeapon = other.GetComponent<PlayerWeapon>();

        // If no PlayerWeapon found, exit (not a player or missing component)
        if (playerWeapon == null)
        {
            // Debug warning for troubleshooting (commented out)
            // Uncomment if pickups aren't working
            // Debug.LogWarning($"<color=red>[SmokePickup] {other.gameObject.name} has NO PlayerWeapon component!</color>");
            return;
        }

        // Debug: Log successful component find (commented out)
        // Debug.Log($"<color=green>[SmokePickup] PlayerWeapon found! Adding {amountToAdd} smoke bomb(s)</color>");

        // Log before/after counts for verification (commented out)
        // Uncomment to verify ammo is being added correctly
        // int beforeCount = playerWeapon.GetSmokeBombCount();
        // playerWeapon.AddSmokeBombs(amountToAdd);
        // int afterCount = playerWeapon.GetSmokeBombCount();
        // Debug.Log($"<color=grey>[SmokePickup] Smoke count: {beforeCount} -> {afterCount}</color>");

        // Add smoke bombs to player's inventory
        playerWeapon.AddSmokeBombs(amountToAdd);

        // Play pickup sound effect if assigned
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            //Debug.Log("[SmokePickup] Sound played");
        }

        // Spawn pickup visual effect if assigned
        if (pickupEffect != null)
        {
            Instantiate(pickupEffect, transform.position, Quaternion.identity);
            Debug.Log("[SmokePickup] Effect instantiated");
        }

        // Destroy the pickup object
        Debug.Log($"<color=red>[SmokePickup] Destroying {gameObject.name}</color>");
        Destroy(gameObject);
    }
}