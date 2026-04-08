using UnityEngine;

// ============================================================================
// FLASH PICKUP - COLLECTIBLE ITEM FOR FLASHBANG GRENADES
// ============================================================================
// 
// This script handles flashbang ammunition pickups in the game world.
// When the player touches the pickup, they receive flashbangs.
//
// FEATURES:
// - Configurable amount of flashbangs to add (1-3 typically)
// - Audio feedback when picked up
// - Visual effect when picked up (particle system or similar)
// - Automatic destruction after pickup
//
// INTEGRATION:
// - Works with PlayerWeapon component
// - PlayerWeapon must have AddFlashBangs() method
// - Can be placed in scene or spawned dynamically
//
// ============================================================================

public class FlashPickup : MonoBehaviour
{
    // ========================================================================
    // SERIALIZED FIELDS - CONFIGURE IN UNITY INSPECTOR
    // ========================================================================

    [Header("=== PICKUP SETTINGS ===")]
    [Tooltip("Number of flashbangs to add when picked up (usually 1-3)")]
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
        // Initialization logging (commented out by default)
        // Uncomment for debugging pickup placement
        // Debug.Log($"<color=yellow>[FlashPickup] {gameObject.name} ready at {transform.position}</color>");
    }

    // ========================================================================
    // COLLISION HANDLING
    // ========================================================================

    /// <summary>
    /// Handles trigger collision with the player.
    /// When player touches the pickup, adds flashbangs and destroys the item.
    /// </summary>
    /// <param name="other">The collider that entered the trigger</param>
    void OnTriggerEnter(Collider other)
    {
        // Debug: Log trigger entry (commented out by default)
        // Debug.Log($"<color=blue>[FlashPickup] {other.gameObject.name} entered trigger</color>");

        // Try to get PlayerWeapon component from the entering object
        PlayerWeapon playerWeapon = other.GetComponent<PlayerWeapon>();

        // If no PlayerWeapon found, exit (not a player or missing component)
        if (playerWeapon == null)
        {
            // Debug warning for troubleshooting (commented out)
            // Debug.LogWarning($"<color=red>[FlashPickup] {other.gameObject.name} has NO PlayerWeapon component!</color>");
            return;
        }

        // Debug: Log successful pickup (commented out)
        // Debug.Log($"<color=green>[FlashPickup] PlayerWeapon found! Adding {amountToAdd} flash bang(s)</color>");
        // Debug: Log before/after counts (commented out)
        // int beforeCount = playerWeapon.GetFlashBangCount();
        // playerWeapon.AddFlashBangs(amountToAdd);
        // int afterCount = playerWeapon.GetFlashBangCount();
        // Debug.Log($"<color=yellow>[FlashPickup] Flash count: {beforeCount} -> {afterCount}</color>");

        // Add flashbangs to player's inventory
        playerWeapon.AddFlashBangs(amountToAdd);

        // Play pickup sound effect if assigned
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            // Debug: Log sound playback (commented out)
            // Debug.Log("[FlashPickup] Sound played");
        }

        // Spawn pickup visual effect if assigned
        if (pickupEffect != null)
        {
            Instantiate(pickupEffect, transform.position, Quaternion.identity);
            // Debug: Log effect spawn (commented out)
            // Debug.Log("[FlashPickup] Effect instantiated");
        }

        // Destroy the pickup object
        // Debug: Log destruction (commented out)
        // Debug.Log($"<color=red>[FlashPickup] Destroying {gameObject.name}</color>");
        Destroy(gameObject);
    }
}