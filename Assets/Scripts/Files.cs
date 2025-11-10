using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;

public class Files : MonoBehaviour
{
	private string localFilesDirectory;
	[DllImport("__Internal")]
    private static extern void JS_FileSystem_Sync();
	
	public static Files instance;
	
	void Awake()
	{
		instance = this;
		#if UNITY_WEBGL && !UNITY_EDITOR
			if(!Directory.Exists($"/idbfs/{gameName}"))
			{
				Directory.CreateDirectory($"/idbfs/{gameName}");
			}
			localFilesDirectory = "/idbfs/{gameName}/";
		#else
			localFilesDirectory = $"{Application.persistentDataPath}/";
		#endif
	}
	
	public void FileUpdated()
	{
		#if UNITY_WEBGL && !UNITY_EDITOR
			JS_FileSystem_Sync();
		#endif
	}
	
    public string GetFileText(string localFilePath)
	{
		string filePath = $"{localFilesDirectory}{localFilePath}.txt";
		if(File.Exists(filePath))
		{
			using(StreamReader reader = new StreamReader(filePath))
			{
				string fileData = reader.ReadToEnd();
				return fileData;
			}
		}
		return null;
	}
	
	public void SetFileText(string localFilePath, string content, bool trim = true)
	{
		string filePath = $"{localFilesDirectory}{localFilePath}.txt";
		if(trim)
		{
			File.WriteAllText(filePath, content.Trim());
		}
		else
		{
			File.WriteAllText(filePath, content);
		}
		FileUpdated();
	}
	
	public void AppendFileText(string localFilePath, string content)
	{
		string filePath = $"{localFilesDirectory}{localFilePath}.txt";
		if(!File.Exists(filePath))
		{
			using(StreamWriter sw = File.CreateText(filePath))
			{
				sw.WriteLine(content);
			}
		}
		else
		{
			using(StreamWriter sw = File.AppendText(filePath))
			{
				sw.WriteLine(content);
			}
		}
	}
}
