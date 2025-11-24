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
	
	public void Log(string logMessage, bool log = true)
	{
		if(!log)
		{
			return;
        }
        string debugTag = "<color=#0000ff>[Log]</color>";
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
