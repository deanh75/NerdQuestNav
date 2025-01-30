using UnityEngine;

public class ToggleVisibility : MonoBehaviour
{
    // Boolean to control visibility
    public bool isVisible;

    // Reference to the GameObject to hide or show (can be the same object this script is attached to)
    public GameObject targetObject;

    // Update is called once per frame
    void Update()
    {
        if (targetObject != null)
        {
            targetObject.SetActive(isVisible);
        }
        else
        {
            Debug.LogWarning("Target object is not assigned.");
        }
    }
}