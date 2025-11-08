using UnityEngine;

public class LocalInterface : MonoBehaviour
{
	public Vector2 referenceResolution;
	
    public static LocalInterface instance;
	
	void Awake()
	{
		instance = this;
	}
	
	public void DisplayError(string errorMessage)
	{
		Debug.LogError($"<color=#ffa500>[Silver Dubloons]</color> <color=#ff0000>[Error]</color> {errorMessage}");
	}
	
	public void DisplayWarning(string logMessage)
	{
		Debug.LogWarning($"<color=#ffa500>[Silver Dubloons]</color> <color=##ffff00>[Warning]</color> {logMessage}");
	}
	
	public void DisplayLog(string logMessage)
	{
		Debug.Log($"<color=#ffa500>[Silver Dubloons]</color> <color=#0000ff>[Log]</color> {logMessage}");
	}
}
