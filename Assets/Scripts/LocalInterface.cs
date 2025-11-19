using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class LocalInterface : MonoBehaviour
{
	public Vector2 referenceResolution;
	[SerializeField] private Label versionLabel;
    public BuildVersionData versionData;

    public static LocalInterface instance;
	
	void Awake()
	{
		instance = this;
	}

	void Start()
	{
        UpdateVersionLabel();

    }

    private void UpdateVersionLabel()
    {
        string versionString = versionData.version;

        string[] versionStringSplit = versionString.Split('.');
        string versionStringFormatted = string.Empty;

        if(versionStringSplit.Length == 4)
        {
            versionStringFormatted =
                $"Version\n{versionStringSplit[0]}.{versionStringSplit[1]}\n{versionStringSplit[2]}.{versionStringSplit[3]}";
        }

        versionLabel.ChangeText(versionStringFormatted);
    }

    public Vector2 GetMousePosition()
    { 
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Logger.instance.Log($"Mouse Position: {mousePosition}");
        return mousePosition;
    }
}
