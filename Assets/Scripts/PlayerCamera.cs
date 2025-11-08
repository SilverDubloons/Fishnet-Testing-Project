using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

// This script will be a NetworkBehaviour so that we can use the 
// OnOwnershipClient override.
public class PlayerCamera : NetworkBehaviour
{
    [SerializeField] private Transform cameraHolder;

    // This method is called on the client after gaining or losing ownership of the object.
    // We could have used OnStartClient instead, as we did in the previous example, but using OnOwnershipClient
    // means this will work for a player object we don't initially own but are given ownership to later.
    public override void OnOwnershipClient(NetworkConnection prevOwner)
    {
        if (Camera.main == null)
            return;

        // If we are the new owner of this object, then take control of the camera by parenting it
        // and moving it to our camera holder.
        if (IsOwner)
        {
            Camera.main.transform.SetPositionAndRotation(cameraHolder.position, cameraHolder.rotation);
            Camera.main.transform.SetParent(cameraHolder);
        }
    }
}