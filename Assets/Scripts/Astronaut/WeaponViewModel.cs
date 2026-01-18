using UnityEngine;

/// <summary>
/// Manages the first-person weapon view model.
/// Only visible to the local player, hidden from others in multiplayer.
/// </summary>
public class WeaponViewModel : MonoBehaviour
{
    [Header("Weapon Model")]
    [Tooltip("The weapon 3D model (child of camera)")]
    public GameObject weaponModel;

    [Header("Position Settings")]
    public Vector3 weaponPosition = new Vector3(0.3f, -0.2f, 0.5f);
    public Vector3 weaponRotation = new Vector3(0f, 0f, 0f);
    public float weaponScale = 1f;

    [Header("Sway Settings")]
    public bool enableSway = true;
    public float swayAmount = 0.02f;
    public float swaySpeed = 5f;

    [Header("Bob Settings (while walking)")]
    public bool enableBob = true;
    public float bobAmount = 0.01f;
    public float bobSpeed = 10f;

    private Vector3 initialPosition;
    private float bobTimer = 0f;
    private PlayerMovement playerMovement;

    void Start()
    {
        playerMovement = GetComponentInParent<PlayerMovement>();

        // Auto-find weapon model if not assigned
        if (weaponModel == null)
        {
            weaponModel = transform.childCount > 0 ? transform.GetChild(0).gameObject : null;
        }

        if (weaponModel != null)
        {
            // Apply initial transform
            weaponModel.transform.localPosition = weaponPosition;
            weaponModel.transform.localRotation = Quaternion.Euler(weaponRotation);
            weaponModel.transform.localScale = Vector3.one * weaponScale;
            initialPosition = weaponPosition;
        }
    }

    void Update()
    {
        if (weaponModel == null) return;

        Vector3 targetPos = initialPosition;

        // Weapon sway based on mouse movement
        if (enableSway)
        {
            float mouseX = Input.GetAxis("Mouse X") * swayAmount;
            float mouseY = Input.GetAxis("Mouse Y") * swayAmount;

            targetPos.x -= mouseX;
            targetPos.y -= mouseY;
        }

        // Weapon bob while moving
        if (enableBob && playerMovement != null)
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            bool isMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;

            if (isMoving)
            {
                bobTimer += Time.deltaTime * bobSpeed;
                float bobOffset = Mathf.Sin(bobTimer) * bobAmount;
                targetPos.y += bobOffset;
            }
            else
            {
                bobTimer = 0f;
            }
        }

        // Smooth lerp to target position
        weaponModel.transform.localPosition = Vector3.Lerp(
            weaponModel.transform.localPosition,
            targetPos,
            Time.deltaTime * swaySpeed
        );
    }

    /// <summary>
    /// Show or hide the weapon model
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (weaponModel != null)
        {
            weaponModel.SetActive(visible);
        }
    }
}
