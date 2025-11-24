using System;
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

    public static string GetCommandLineArgument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name && args.Length > i + 1)
            {
                return args[i + 1];
            }
        }
        return null;
    }

    public Vector2 GetMousePosition()
    { 
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Logger.instance.Log($"Mouse Position: {mousePosition}");
        return mousePosition;
    }
}
