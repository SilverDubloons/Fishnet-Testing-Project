using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

public class Logger : MonoBehaviour
{
	[SerializeField] private Label logOutputLabel;
	[SerializeField] private RectTransform logOutputContentRT;
	[SerializeField] private Scrollbar logVerticalScrollbar;
	
    public static Logger instance;

	public int minimumLogLevel = 0; // displays all logs by default, lower means less important

    void Awake()
	{
		instance = this;
	}
	
	public void Error(string errorMessage)
	{
		string debugTag = "<color=#ff0000>[Error]</color>";
		OutputMessage(errorMessage, debugTag);
	}
	
	public void Warning(string warningMessage)
	{
		string debugTag = "<color=#ffff00>[Warning]</color>";
		OutputMessage(warningMessage, debugTag);
	}
	
	public void Log(string logMessage, int logLevel = int.MaxValue)
	{
		if(logLevel < minimumLogLevel)
		{
			return;
        }
		string logLevelString = logLevel == int.MaxValue ? "MAX" : logLevel.ToString();
        string debugTag = $"<color=#0000ff>[Log{logLevelString}]</color>";
		OutputMessage(logMessage, debugTag);
	}
	
	public void OutputMessage(string displayMessage, string debugTag)
	{
		Debug.Log($"<color=#ffa500>[Silver Dubloons]</color> {debugTag} {displayMessage}");
		if(logOutputLabel != null && logOutputContentRT != null)
		{
			logOutputLabel.ChangeText(logOutputLabel.GetText() +"\n" + $"{debugTag} {displayMessage}");
			float height = logOutputLabel.GetPreferredHeight();
			// float height = logOutputLabel.GetPreferredValuesString(135f).y;
			logOutputContentRT.sizeDelta = new Vector2(logOutputContentRT.sizeDelta.x, height + 10f);
			logVerticalScrollbar.value = 0f;
			StartCoroutine(LowerScrollbar());
		}
		Files.instance.AppendFileText("Log", $"[{DateTime.Now}]: {displayMessage}");
	}
	
	public IEnumerator LowerScrollbar()
	{
		yield return null;
		logVerticalScrollbar.value = 0f;
		yield return null;
		logVerticalScrollbar.value = 0f;
	}
}
