using UnityEngine;
using TMPro;
using System;

public class Log : MonoBehaviour
{
	[SerializeField] private Label logOutputLabel;
	[SerializeField] private RectTransform logOutputContentRT;
	
    public static Log instance;
	
	void Awake()
	{
		instance = this;
	}
	
	public void DisplayError(string errorMessage)
	{
		string debugTag = "<color=#ff0000>[Error]</color>";
		OutputMessage(errorMessage, debugTag);
	}
	
	public void DisplayWarning(string warningMessage)
	{
		string debugTag = "<color=##ffff00>[Warning]</color>";
		OutputMessage(warningMessage, debugTag);
	}
	
	public void DisplayLog(string logMessage)
	{
		string debugTag = "<color=#0000ff>[Log]</color>";
		OutputMessage(logMessage, debugTag);
	}
	
	public void OutputMessage(string displayMessage, string debugTag)
	{
		Debug.Log($"<color=#ffa500>[Silver Dubloons]</color> {debugTag} {displayMessage}");
		if(logOutputLabel != null && logOutputContentRT != null)
		{
			logOutputLabel.ChangeText(logOutputLabel.GetText() +"\n" + $"{debugTag} {displayMessage}");
			// float height = logOutputLabel.GetPreferredHeight();
			float height = logOutputLabel.GetPreferredValuesString(135f).y;
			logOutputContentRT.sizeDelta = new Vector2(logOutputContentRT.sizeDelta.x, height + 10f);
		}
		Files.instance.AppendFileText("Log", $"[{DateTime.Now}]: {displayMessage}");
	}
}
