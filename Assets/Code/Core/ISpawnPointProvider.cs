// Assets/Code/Core/ISpawnPointProvider.cs
using UnityEngine;
public interface ISpawnPointProvider
{
    Vector3 GetPlayerSpawn();
    Vector3 GetPlayerForward() => Vector3.forward;
}
