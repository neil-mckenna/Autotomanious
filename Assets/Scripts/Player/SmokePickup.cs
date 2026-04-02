using UnityEngine;

public class SmokePickup : MonoBehaviour
{
    [SerializeField] private int amountToAdd = 1;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private GameObject pickupEffect;

    void Start()
    {
        Debug.Log($"<color=grey>[SmokePickup] {gameObject.name} ready at {transform.position}</color>");
    }

    void OnTriggerEnter(Collider other)
    {
        //Debug.Log($"<color=blue>[SmokePickup] {other.gameObject.name} entered trigger</color>");

        PlayerWeapon playerWeapon = other.GetComponent<PlayerWeapon>();

        if (playerWeapon == null)
        {
            //Debug.LogWarning($"<color=red>[SmokePickup] {other.gameObject.name} has NO PlayerWeapon component!</color>");
            return;
        }

        //Debug.Log($"<color=green>[SmokePickup] PlayerWeapon found! Adding {amountToAdd} smoke bomb(s)</color>");

        int beforeCount = playerWeapon.GetSmokeBombCount();
        playerWeapon.AddSmokeBombs(amountToAdd);
        int afterCount = playerWeapon.GetSmokeBombCount();

        //Debug.Log($"<color=grey>[SmokePickup] Smoke count: {beforeCount} - {afterCount}</color>");

        // Effects
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            Debug.Log("[SmokePickup] Sound played");
        }

        if (pickupEffect != null)
        {
            Instantiate(pickupEffect, transform.position, Quaternion.identity);
            Debug.Log("[SmokePickup] Effect instantiated");
        }

        Debug.Log($"<color=red>[SmokePickup] Destroying {gameObject.name}</color>");
        Destroy(gameObject);
    }
}