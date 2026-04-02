using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerWeapon : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private GameObject smokeBombPrefab;
    [SerializeField] private GameObject flashBangPrefab;
    [SerializeField] private int maxSmokeBombs = 6;
    [SerializeField] private int maxFlashBangs = 6;
    [SerializeField] private float throwForce = 8f;
    [SerializeField] private float throwCooldown = 1f;

    [Header("Throwing Improvements")]
    [SerializeField] private Transform rightShoulder; // Assign in inspector - right shoulder transform
    [SerializeField] private Transform leftShoulder;  // Assign in inspector - left shoulder transform
    [SerializeField] private bool useRightHand = true; // Toggle between right and left shoulder
    [SerializeField] private float upwardForce = 1.5f;
    [SerializeField] private LayerMask throwCollisionMask = -1;
    [SerializeField] private float collisionCheckRadius = 0.2f;
    [SerializeField] private float gravityMultiplier = 2.5f;

    [Header("UI - Text Names")]
    [SerializeField] private string smokeUITextName = "SmokeCountText";
    [SerializeField] private string flashUITextName = "FlashCountText";

    [Header("Audio")]
    [SerializeField] private AudioClip throwSound;
    [SerializeField] private float throwVolume = 0.7f;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActionAsset;

    private int currentSmokeBombs;
    private int currentFlashBangs;
    private float lastThrowTime;
    private Camera playerCamera;
    private TextMeshProUGUI smokeCountText;
    private TextMeshProUGUI flashCountText;
    private AudioSource audioSource;
    private Collider playerCollider;
    private Animator playerAnimator; // Optional: for throw animation

    private InputActionMap playerActionMap;
    private InputAction smokeBombAction;
    private InputAction flashBangAction;

    void Start()
    {
        currentSmokeBombs = 0;
        currentFlashBangs = 0;

        playerCamera = Camera.main;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        playerCollider = GetComponent<Collider>();
        if (playerCollider == null)
            playerCollider = GetComponentInChildren<Collider>();

        playerAnimator = GetComponent<Animator>();

        // Auto-find shoulder transforms if not assigned
        if (rightShoulder == null || leftShoulder == null)
        {
            FindShoulderTransforms();
        }

        SetupInput();
        FindUIElements();
        UpdateUI();
    }

    void FindShoulderTransforms()
    {
        // Try to find shoulder bones by common naming conventions
        Transform[] allChildren = GetComponentsInChildren<Transform>();

        foreach (Transform child in allChildren)
        {
            if (child.name.ToLower().Contains("shoulder") || child.name.ToLower().Contains("clavicle"))
            {
                if (child.name.ToLower().Contains("right") || child.name.ToLower().Contains("r_"))
                {
                    rightShoulder = child;
                }
                else if (child.name.ToLower().Contains("left") || child.name.ToLower().Contains("l_"))
                {
                    leftShoulder = child;
                }
            }
        }

        // If still not found, create empty transforms as fallback
        if (rightShoulder == null)
        {
            GameObject rightShoulderObj = new GameObject("RightShoulder");
            rightShoulderObj.transform.parent = transform;
            rightShoulderObj.transform.localPosition = new Vector3(0.3f, 1.2f, 0.2f);
            rightShoulder = rightShoulderObj.transform;
        }

        if (leftShoulder == null)
        {
            GameObject leftShoulderObj = new GameObject("LeftShoulder");
            leftShoulderObj.transform.parent = transform;
            leftShoulderObj.transform.localPosition = new Vector3(-0.3f, 1.2f, 0.2f);
            leftShoulder = leftShoulderObj.transform;
        }
    }

    Transform GetCurrentThrowPoint()
    {
        return useRightHand ? rightShoulder : leftShoulder;
    }

    void SetupInput()
    {
        if (inputActionAsset == null)
        {
            Debug.LogError("Input Action Asset not assigned!");
            return;
        }

        playerActionMap = inputActionAsset.FindActionMap("Player");

        if (playerActionMap == null)
        {
            Debug.LogError("Could not find 'Player' action map in Input Action Asset!");
            return;
        }

        smokeBombAction = playerActionMap.FindAction("SmokeBomb");
        flashBangAction = playerActionMap.FindAction("FlashBang");

        if (smokeBombAction == null)
            Debug.LogError("Could not find 'SmokeBomb' action!");

        if (flashBangAction == null)
            Debug.LogError("Could not find 'FlashBang' action!");

        playerActionMap.Enable();

        if (smokeBombAction != null)
            smokeBombAction.performed += OnSmokeBomb;

        if (flashBangAction != null)
            flashBangAction.performed += OnFlashBang;
    }

    void OnDestroy()
    {
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
        if (!TryGetThrowPosition(out Vector3 throwPosition, out Vector3 throwDirection))
            return;

        currentSmokeBombs--;
        lastThrowTime = Time.time;

        // Play throw animation if available
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Throw");
        }

        GameObject smoke = Instantiate(smokeBombPrefab, throwPosition, Quaternion.identity);

        Rigidbody rb = smoke.GetComponent<Rigidbody>();
        Collider grenadeCollider = smoke.GetComponent<Collider>();

        if (rb != null && grenadeCollider != null)
        {
            rb.useGravity = true;
            rb.mass = 1.5f;

            Physics.IgnoreCollision(grenadeCollider, playerCollider, true);
            rb.AddForce(throwDirection * throwForce, ForceMode.Impulse);

            StartCoroutine(ApplyGravityMultiplier(rb));
            StartCoroutine(EnablePlayerCollisionAfterDelay(grenadeCollider, 0.2f));
        }

        PlayThrowSound();
        UpdateUI();
        Debug.Log($"Smoke bomb thrown from {(useRightHand ? "right" : "left")} shoulder! Remaining: {currentSmokeBombs}");
    }

    void ThrowFlashBang()
    {
        if (!TryGetThrowPosition(out Vector3 throwPosition, out Vector3 throwDirection))
            return;

        currentFlashBangs--;
        lastThrowTime = Time.time;

        // Play throw animation if available
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("Throw");
        }

        GameObject flash = Instantiate(flashBangPrefab, throwPosition, Quaternion.identity);

        Rigidbody rb = flash.GetComponent<Rigidbody>();
        Collider grenadeCollider = flash.GetComponent<Collider>();

        if (rb != null && grenadeCollider != null)
        {
            rb.useGravity = true;
            rb.mass = 1.5f;

            Physics.IgnoreCollision(grenadeCollider, playerCollider, true);
            rb.AddForce(throwDirection * throwForce, ForceMode.Impulse);

            StartCoroutine(ApplyGravityMultiplier(rb));
            StartCoroutine(EnablePlayerCollisionAfterDelay(grenadeCollider, 0.2f));
        }

        PlayThrowSound();
        UpdateUI();
        Debug.Log($"Flash bang thrown from {(useRightHand ? "right" : "left")} shoulder! Remaining: {currentFlashBangs}");
    }

    IEnumerator ApplyGravityMultiplier(Rigidbody rb)
    {
        if (rb == null) yield break;

        float elapsedTime = 0;
        while (rb != null && elapsedTime < 5f)
        {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    bool TryGetThrowPosition(out Vector3 throwPosition, out Vector3 throwDirection)
    {
        throwPosition = Vector3.zero;
        throwDirection = Vector3.zero;

        Transform throwPoint = GetCurrentThrowPoint();

        if (throwPoint == null)
        {
            Debug.LogError("No throw point assigned!");
            return false;
        }

        // Get throw position from shoulder
        throwPosition = throwPoint.position;

        // Calculate throw direction based on camera forward + slight upward arc
        if (playerCamera != null)
        {
            Vector3 baseDirection = playerCamera.transform.forward;
            throwDirection = (baseDirection + Vector3.up * upwardForce).normalized;
        }
        else
        {
            throwDirection = (transform.forward + Vector3.up * upwardForce).normalized;
        }

        // Check if spawn position is inside any wall/object
        if (Physics.CheckSphere(throwPosition, collisionCheckRadius, throwCollisionMask))
        {
            // Try to adjust position slightly forward
            Vector3 adjustedPos = throwPosition + throwDirection * 0.3f;
            if (!Physics.CheckSphere(adjustedPos, collisionCheckRadius, throwCollisionMask))
            {
                throwPosition = adjustedPos;
            }
            else
            {
                Debug.LogWarning("Spawn position at shoulder is obstructed");
            }
        }

        return true;
    }

    IEnumerator EnablePlayerCollisionAfterDelay(Collider grenadeCollider, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (grenadeCollider != null && playerCollider != null)
        {
            Physics.IgnoreCollision(grenadeCollider, playerCollider, false);
        }
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

    // Optional: Method to switch throwing hand
    public void SwitchThrowingHand()
    {
        useRightHand = !useRightHand;
        Debug.Log($"Switched to {(useRightHand ? "right" : "left")} shoulder");
    }

    void OnDrawGizmosSelected()
    {
        if (rightShoulder != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(rightShoulder.position, collisionCheckRadius);
        }

        if (leftShoulder != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(leftShoulder.position, collisionCheckRadius);
        }

        if (playerCamera != null && GetCurrentThrowPoint() != null)
        {
            Gizmos.color = Color.green;
            Vector3 direction = (playerCamera.transform.forward + Vector3.up * upwardForce).normalized;
            Gizmos.DrawRay(GetCurrentThrowPoint().position, direction * 3f);
        }
    }
}