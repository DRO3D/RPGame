// Assets/Code/Core/SpawnPoint.cs
using UnityEngine;
public class SpawnPoint : MonoBehaviour
{
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.2f);
        Gizmos.DrawRay(transform.position, transform.forward * 1f);
    }
}
