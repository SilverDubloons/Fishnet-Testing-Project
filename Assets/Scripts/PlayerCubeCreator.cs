using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCubeCreator : NetworkBehaviour
{
    public NetworkObject cubePrefab;

    public override void OnStartClient()
    {
        if (IsOwner)
            GetComponent<PlayerInput>().enabled = true;
    }

   /*  public void OnAttack(InputAction.CallbackContext context)
    {
		Debug.Log("OnAttack called");
        if (context.started)
            SpawnCube();
    } */

	public void OnAttack()
    {
        SpawnCube();
    }

    // We are using a ServerRpc here because the Server needs to do all network object spawning.
    [ServerRpc]
    private void SpawnCube()
    {
        NetworkObject obj = Instantiate(cubePrefab, transform.position, Quaternion.identity);
		obj.GetComponent<SyncMaterialColor>().color.Value = Random.ColorHSV();
        Spawn(obj); // NetworkBehaviour shortcut for ServerManager.Spawn(obj);
    }
}