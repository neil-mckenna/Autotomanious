using UnityEngine;

public class FlashPickup : MonoBehaviour
{
    [SerializeField] private int amountToAdd = 1;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private GameObject pickupEffect;

    void Start()
    {
        Debug.Log($"<color=yellow>[FlashPickup] {gameObject.name} ready at {transform.position}</color>");
    }

    void OnTriggerEnter(Collider other)
    {
        //Debug.Log($"<color=blue>[FlashPickup] {other.gameObject.name} entered trigger</color>");

        PlayerWeapon playerWeapon = other.GetComponent<PlayerWeapon>();

        if (playerWeapon == null)
        {
            Debug.LogWarning($"<color=red>[FlashPickup] {other.gameObject.name} has NO PlayerWeapon component!</color>");
            return;
        }

        //Debug.Log($"<color=green>[FlashPickup] PlayerWeapon found! Adding {amountToAdd} flash bang(s)</color>");

        int beforeCount = playerWeapon.GetFlashBangCount();
        playerWeapon.AddFlashBangs(amountToAdd);
        int afterCount = playerWeapon.GetFlashBangCount();

        //Debug.Log($"<color=yellow>[FlashPickup] Flash count: {beforeCount} - {afterCount}</color>");

        // Effects
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            Debug.Log("[FlashPickup] Sound played");
        }

        if (pickupEffect != null)
        {
            Instantiate(pickupEffect, transform.position, Quaternion.identity);
            Debug.Log("[FlashPickup] Effect instantiated");
        }

        //Debug.Log($"<color=red>[FlashPickup] Destroying {gameObject.name}</color>");
        Destroy(gameObject);
    }
}