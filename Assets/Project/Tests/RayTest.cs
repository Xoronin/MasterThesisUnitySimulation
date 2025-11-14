using UnityEngine;

public class RaycastDebug : MonoBehaviour
{
    public Camera cam;
    public LayerMask mask; // set in Inspector: same as raytracer (Buildings | Terrain)

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 10000f, mask))
        {
            Debug.Log($"Hit: {hit.collider.name} on layer {hit.collider.gameObject.layer}");
        }
        else
        {
            Debug.Log("No hit with this mask");
        }
    }
}
