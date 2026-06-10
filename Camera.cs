using UnityEngine;

public class Camera : MonoBehaviour
{
    
    public Transform characterBody;

    public float sensitivityX = 0.5f;
    public float sensitivityY = 0.5f;

    public float rotationX = 0;
    public float rotationY = 0;

    public float angleYMin = -85f;
    public float angleYMax = 85f;
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    
    void Update()
    {
        float verticalDelta = Input.GetAxisRaw("Mouse Y") * sensitivityY;
        float horizontalDelta = Input.GetAxisRaw("Mouse X") * sensitivityX;

        rotationX += horizontalDelta;
        rotationY += verticalDelta;

        rotationY = Mathf.Clamp(rotationY, angleYMin, angleYMax);

        characterBody.localEulerAngles = new Vector3(0, rotationX, 0);
        transform.localEulerAngles = new Vector3(-rotationY, 0, 0);
    }
}
