using UnityEngine;
using TMPro;
using System.Text.RegularExpressions;
using System.Collections;

public class Label : MonoBehaviour
{
	public enum FontSize {small, large}
    
    [SerializeField] private FontSize fontSize = FontSize.small;
	
	public RectTransform rt;
    public TMP_Text labelShadow;
	public TMP_Text label;
	public RectTransform labelShadowRT;
	public RectTransform labelRT;
	
	private bool expandRetracting;
	private IEnumerator expandRetractCoroutine;
	
	private void OnValidate()
	{
		#if UNITY_EDITOR
		// Delay the font size application to avoid OnValidate conflicts
		UnityEditor.EditorApplication.delayCall += () => 
		{
			if (this != null) // Make sure the object still exists
			{
				ApplyFontSize();
			}
		};
		#else
		ApplyFontSize();
		#endif
	}
	
	public void ApplyFontSize()
	{
		switch(fontSize)
		{
			case FontSize.small:
				ChangeFontSize(8);
			break;
			case FontSize.large:
				ChangeFontSize(16);
			break;
		}
	}
	
	public void ChangeText(string newText, bool filterRichText = false)
	{
		if(filterRichText)
		{
			labelShadow.text = RemoveRichTextTagsExceptAlign(newText);
		}
		else
		{
			labelShadow.text = newText;
		}
		label.text = newText;
	}
	
	public void ChangeText(string newText1, string newText2, bool filterRichText = false)
	{
		if(filterRichText)
		{
			labelShadow.text = RemoveRichTextTagsExceptAlign(newText2);
		}
		else
		{
			labelShadow.text = newText2;
		}
		label.text = newText1;
	}
	
	public string RemoveRichTextTags(string input)
	{
		string pattern = @"<.*?>";
		return Regex.Replace(input, pattern, string.Empty);
	}
	
	public string RemoveRichTextTagsExceptAlign(string input)
	{
		string pattern = @"<(?!align\s*=\s*(""|').*?(""|')|\/align)[^>]+>";
		return Regex.Replace(input, pattern, string.Empty);
	}
	
	public void ClearText()
	{
		labelShadow.text = string.Empty;
		label.text = string.Empty;
	}
	
	public void ForceMeshUpdate()
	{
		labelShadow.ForceMeshUpdate(true, true);
		label.ForceMeshUpdate(true, true);
	}
	
	public void ChangeColor(Color color, Color shadowColor)
	{
		ChangeColor(color);
		labelShadow.color = shadowColor;
	}
	
	public void ChangeColor(Color color)
	{
		label.color = color;
	}
	
	public void StartExpandRetract(float duration = 0.2f, float expandFactor = 1.6f)
	{
		if(expandRetracting)
		{
			StopCoroutine(expandRetractCoroutine);
		}
		expandRetractCoroutine = ExpandRetract(duration, expandFactor);
		StartCoroutine(expandRetractCoroutine);
	}
	
	private IEnumerator ExpandRetract(float duration, float expandFactor)
	{
		expandRetracting = true;
		float t = 0;
		Vector2 expansionScale = new Vector2(expandFactor, expandFactor);
		while(t < duration / 2)
		{
			t += Time.deltaTime;
			labelShadowRT.localScale = Vector2.Lerp(Vector2.one, expansionScale, t / (duration / 2));
			labelRT.localScale = Vector2.Lerp(Vector2.one, expansionScale, t / (duration / 2));
			yield return null;
		}
		t = 0;
		while(t < duration / 2)
		{
			t += Time.deltaTime;
			labelShadowRT.localScale = Vector2.Lerp(expansionScale, Vector2.one, t / (duration / 2));
			labelRT.localScale = Vector2.Lerp(expansionScale, Vector2.one, t / (duration / 2));
			yield return null;
		}
		labelShadowRT.localScale = Vector2.one;
		labelRT.localScale = Vector2.one;
		expandRetracting = false;
	}
	
	public float GetPreferredHeight()
	{
		ForceMeshUpdate();
		return label.preferredHeight;
	}
	
	public float GetPreferredWidth()
	{
		ForceMeshUpdate();
		return label.preferredWidth;
	}
	
	public string GetText()
	{
		return label.text;
	}
	
	public void ChangeAlpha(float newAlpha)
	{
		label.color = new Color(label.color.r, label.color.g, label.color.b, newAlpha);
		labelShadow.color = new Color(labelShadow.color.r, labelShadow.color.g, labelShadow.color.b, newAlpha);
	}
	
	public void ChangeFontSize(int newFontSize)
	{
		if(labelShadow == null || label == null)
		{
			return;
		}
		if(Mathf.Abs(label.fontSize - newFontSize) < 0.1f)
		{
			return;
		}
		labelShadow.fontSize = newFontSize;
		label.fontSize = newFontSize;
		if(newFontSize == 8)
		{
			labelShadowRT.offsetMax = new Vector2(1f, -1f);
			labelShadowRT.offsetMin = new Vector2(1f, -1f);
		}
		else if(newFontSize == 16)
		{
			labelShadowRT.offsetMax = new Vector2(2f, -2f);
			labelShadowRT.offsetMin = new Vector2(2f, -2f);
		}
	}
	
	public Vector2 GetPreferredValues()
	{
		return label.GetPreferredValues();
	}
	
	public Vector2 GetPreferredValuesString(float width)
	{
		return label.GetPreferredValues(label.text, width, 9001f);
	}
	
	public void ChangeSpriteAsset(TMP_SpriteAsset newSpriteAsset)
	{
		labelShadow.spriteAsset = newSpriteAsset;
		label.spriteAsset = newSpriteAsset;
	}
}
