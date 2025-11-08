using FishNet.Object;
using System.Collections;
using UnityEngine;

public class DespawnAfterTime : NetworkBehaviour
{
    public float secondsBeforeDespawn = 3f;

    public override void OnStartServer()
    {
        StartCoroutine(DespawnAfterSeconds());
    }

    private IEnumerator DespawnAfterSeconds()
    {
        yield return new WaitForSeconds(secondsBeforeDespawn);

        Despawn(); // NetworkBehaviour shortcut for ServerManager.Despawn(gameObject);
    }
}