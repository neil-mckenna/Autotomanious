using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class PlayerWeapon : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private GameObject smokeBombPrefab;
    [SerializeField] private GameObject flashBangPrefab;
    [SerializeField] private int maxSmokeBombs = 3;
    [SerializeField] private int maxFlashBangs = 2;
    [SerializeField] private float throwForce = 15f;
    [SerializeField] private float throwCooldown = 1f;

    [Header("UI - Text Names")]
    [SerializeField] private string smokeUITextName = "SmokeCountText";
    [SerializeField] private string flashUITextName = "FlashCountText";

    [Header("Audio")]
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private float throwVolume = 0.7f;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActionAsset;  // Drag your .inputsettings here

    private int currentSmokeBombs;
    private int currentFlashBangs;
    private float lastThrowTime;
    private Camera playerCamera;
    private TextMeshProUGUI smokeCountText;
    private TextMeshProUGUI flashCountText;
    private AudioSource audioSource;

    private InputActionMap playerActionMap;
    private InputAction smokeBombAction;
    private InputAction flashBangAction;

    void Start()
    {
        // Initialize ammo
        currentSmokeBombs = maxSmokeBombs;
        currentFlashBangs = maxFlashBangs;

        // Get references
        playerCamera = Camera.main;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Setup input from asset
        SetupInput();

        // Find UI elements
        FindUIElements();

        UpdateUI();

        //Debug.Log($" Player ready: {currentSmokeBombs} smoke bombs, {currentFlashBangs} flash bangs");
    }

    void SetupInput()
    {
        if (inputActionAsset == null)
        {
            Debug.LogError("Input Action Asset not assigned!");
            return;
        }

        // Find the "Player" action map
        playerActionMap = inputActionAsset.FindActionMap("Player");

        if (playerActionMap == null)
        {
            Debug.LogError("Could not find 'Player' action map in Input Action Asset!");
            return;
        }

        // Find specific actions
        smokeBombAction = playerActionMap.FindAction("SmokeBomb");
        flashBangAction = playerActionMap.FindAction("FlashBang");

        if (smokeBombAction == null)
            Debug.LogError("Could not find 'SmokeBomb' action!");

        if (flashBangAction == null)
            Debug.LogError("Could not find 'FlashBang' action!");

        // Enable and bind
        playerActionMap.Enable();

        if (smokeBombAction != null)
            smokeBombAction.performed += OnSmokeBomb;

        if (flashBangAction != null)
            flashBangAction.performed += OnFlashBang;
    }

    void OnDestroy()
    {
        // Clean up
        if (smokeBombAction != null)
            smokeBombAction.performed -= OnSmokeBomb;

        if (flashBangAction != null)
            flashBangAction.performed -= OnFlashBang;

        if (playerActionMap != null)
            playerActionMap.Disable();
    }

    void OnSmokeBomb(InputAction.CallbackContext context)
    {
        if (currentSmokeBombs > 0 && Time.time > lastThrowTime + throwCooldown)
        {
            ThrowSmokeBomb();
        }
    }

    void OnFlashBang(InputAction.CallbackContext context)
    {
        if (currentFlashBangs > 0 && Time.time > lastThrowTime + throwCooldown)
        {
            ThrowFlashBang();
        }
    }

    void ThrowSmokeBomb()
    {
        currentSmokeBombs--;
        lastThrowTime = Time.time;

        Vector3 throwPosition = transform.position + Vector3.up * 1f;

        Vector3 throwDirection = playerCamera.transform.forward + Vector3.up * 0.3f;
        throwDirection.Normalize();


        GameObject smoke = Instantiate(smokeBombPrefab, throwPosition, Quaternion.identity);

        Rigidbody rb = smoke.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(playerCamera.transform.forward * throwForce, ForceMode.Impulse);
        }

        PlayThrowSound();
        UpdateUI();
        Debug.Log($" Smoke bomb thrown! Remaining: {currentSmokeBombs}");
    }

    void ThrowFlashBang()
    {
        currentFlashBangs--;
        lastThrowTime = Time.time;

        Vector3 throwPosition = transform.position + Vector3.up * 1f;

        Vector3 throwDirection = playerCamera.transform.forward + Vector3.up * 0.3f;
        throwDirection.Normalize();

        GameObject flash = Instantiate(flashBangPrefab, throwPosition, Quaternion.identity);

        Rigidbody rb = flash.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(playerCamera.transform.forward * throwForce, ForceMode.Impulse);
        }

        PlayThrowSound();
        UpdateUI();
        Debug.Log($" Flash bang thrown! Remaining: {currentFlashBangs}");
    }

    void PlayThrowSound()
    {
        if (throwSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(throwSound, throwVolume);
        }
    }

    void FindUIElements()
    {
        GameObject smokeTextObj = GameObject.Find(smokeUITextName);
        if (smokeTextObj != null)
        {
            smokeCountText = smokeTextObj.GetComponent<TextMeshProUGUI>();
        }

        GameObject flashTextObj = GameObject.Find(flashUITextName);
        if (flashTextObj != null)
        {
            flashCountText = flashTextObj.GetComponent<TextMeshProUGUI>();
        }
    }

    void UpdateUI()
    {
        if (smokeCountText != null)
            smokeCountText.text = $"SMOKE: {currentSmokeBombs}";

        if (flashCountText != null)
            flashCountText.text = $"FLASH: {currentFlashBangs}";
    }

    public void AddSmokeBombs(int amount)
    {
        currentSmokeBombs += amount;
        UpdateUI();
    }

    public void AddFlashBangs(int amount)
    {
        currentFlashBangs += amount;
        UpdateUI();
    }

    public int GetSmokeBombCount() => currentSmokeBombs;
    public int GetFlashBangCount() => currentFlashBangs;
}