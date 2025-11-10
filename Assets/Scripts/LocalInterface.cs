using UnityEngine;

public class LocalInterface : MonoBehaviour
{
	public Vector2 referenceResolution;
	
    public static LocalInterface instance;
	
	void Awake()
	{
		instance = this;
	}
}
