using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;
// using static ThemeManager;

#if UNITY_EDITOR

using UnityEditor;

#endif

public class ButtonPlus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
	public enum ButtonType {Standard, Alternate, Back, Warning, Buy, VariantSimple, DrawPile}
	public ButtonType buttonType;
	
    public RectTransform rt;
	public bool isButton;
	public bool buttonEnabled = true;
	public bool playClickingSound = true;
	public AudioClip clickSound;
	public AudioSource soundSource;
	public float volumeFactor;
	public bool holdingDown = false;
	public bool mouseOverButton = false;
	public bool specialState = false;
	[SerializeField]
    public UnityEvent onClickEvent;
	[SerializeField]
    private UnityEvent onDoubleClickEvent;
	private float timeOfLastClick;
	private int clicksInARow = 0;
	
	public bool moveImageWhenClicked = true;
	private Vector2 buttonImageOrigin;
	public RectTransform buttonImageRT;
	public Vector2 buttonImageDestinationAdditive = new Vector2(0, -2f);
	public float moveImageDuration = 0.05f;
	private IEnumerator moveImageCoroutine;
	private bool movingImage = false;
	public RectTransform labelRT;
	// public ControllerSelectableObject controllerSelectableObject;
	
	public bool expandEnabled;
	public float expansionFactor = 1.05f;
	public float expansionDuration = 0.1f;
	private IEnumerator scaleChangeCoroutine;
	private bool changingScale = false;
	
	public bool changeColorEnabled;
	public bool colorChangeIsMultiplicative = true;
	public Color baseColor = Color.blue;
	public Color specialStateColor = Color.green;
	public Color mouseOverColor = new Color(0.86f, 0.86f, 0.86f, 1f);
	public Color disabledColor = new Color(0.2f, 0.2f, 0.2f, 1f);
	public Image buttonImage;
	public Image shadowImage;
	public float changeColorDuration = 0.1f;
	private IEnumerator colorChangeCoroutine;
	private bool changingColor = false;
	
	private IEnumerator checkForGlobalMouseUpCoroutine;
	private bool checkingForGlobalMouseUp = false;
//	private bool quitting = false;
	
	public bool tickOnMouseOver = false;
	
	public Label buttonLabel;
	
	void Start()
	{
		/* ThemeManager.instance.OnThemeChanged += ApplyTheme;
        ApplyTheme(); */
		if(isButton)
		{
			buttonImageOrigin = buttonImageRT.anchoredPosition;
		}
	}
	
	void OnDestroy() 
	{
        /* if(ThemeManager.instance != null)
		{
            ThemeManager.instance.OnThemeChanged -= ApplyTheme;
		} */
    }
	
	public void UpdateColor()
	{
		if(changeColorEnabled && buttonImage != null)
		{
			if(!buttonEnabled)
			{
				buttonImage.color = disabledColor;
			}
			else
			{
				if(specialState)
				{
					buttonImage.color = specialStateColor;
				}
				else
				{
					buttonImage.color = baseColor;
				}
				if(mouseOverButton)
				{
					buttonImage.color = buttonImage.color * mouseOverColor;
				}
			}
		}
	}
	
	/* public void ApplyTheme()
	{
		switch(buttonType)
		{
			case ButtonType.Standard:
				baseColor = ThemeManager.instance.GetColorFromCurrentTheme(UIElementType.standardButtonActive);
			break;
			case ButtonType.Alternate:
				baseColor = ThemeManager.instance.GetColorFromCurrentTheme(UIElementType.altButtonActive);
			break;
			case ButtonType.Back:
				baseColor = ThemeManager.instance.GetColorFromCurrentTheme(UIElementType.backButtonActive);
			break;
			case ButtonType.Warning:
				baseColor = ThemeManager.instance.GetColorFromCurrentTheme(UIElementType.warningButtonActive);
			break;
			case ButtonType.Buy:
				baseColor = ThemeManager.instance.GetColorFromCurrentTheme(UIElementType.shopBuyButton);
			break;
			case ButtonType.VariantSimple:
				baseColor = ThemeManager.instance.GetColorFromCurrentTheme(UIElementType.variantSimpleInterior);
			break;
			case ButtonType.DrawPile:
				baseColor = ThemeManager.instance.GetColorFromCurrentTheme(UIElementType.backdrop);
			break;
		}
		if(buttonType == ButtonType.VariantSimple)
		{
			specialStateColor = ThemeManager.instance.GetColorFromCurrentTheme(UIElementType.VariantSimpleSelected);
		}
		else
		{
			specialStateColor = ThemeManager.instance.GetColorFromCurrentTheme(UIElementType.buttonSpecialState);
		}
		mouseOverColor = ThemeManager.instance.GetColorFromCurrentTheme(UIElementType.buttonMouseOver);
	    disabledColor = ThemeManager.instance.GetColorFromCurrentTheme(UIElementType.buttonDisabled);
		UpdateColor();
	} */
	
	public void ChangeButtonEvent(UnityAction newAction)
	{
		onClickEvent.RemoveAllListeners();
		onClickEvent.AddListener(newAction);
	}
	
	public void ChangeButtonText(string newText)
	{
		buttonLabel.ChangeText(newText);
	}
	
	public void ChangeButtonBaseColor(Color newColor)
	{
		buttonImage.color = newColor;
		baseColor = newColor;
	}
	
	public bool GetButtonEnabled()
	{
		return buttonEnabled;
	}
	
	void OnApplicationQuit()
	{
		// quitting = true;
	}
	
	void OnDisable()
	{
/* 		if(quitting || TransitionStinger.instance.switchingScenes)
		{
			return;
		} */
		// Debug.Log($"Disabling {this.gameObject.name} with parent {rt.parent.gameObject.name} and 2 parents up {rt.parent.parent.gameObject.name}");
		mouseOverButton = false;
		ResetButton();
	}
	
	public void ResetButton()
	{
		if(changingScale)
		{
			StopCoroutine(scaleChangeCoroutine);
			changingScale = false;
		}
		if(changingColor)
		{
			StopCoroutine(colorChangeCoroutine);
			changingColor = false;
		}
		if(movingImage)
		{
			StopCoroutine(moveImageCoroutine);
			movingImage = false;
		}
		rt.localScale = Vector3.one;
		if(isButton)
		{
			buttonImageRT.anchoredPosition = buttonImageOrigin;
		}
		if(buttonEnabled && buttonImage != null)
		{
			// Vector2 mousePos = new Vector2((Input.mousePosition.x / Screen.width) * LocalInterface.instance.referenceResolution.x - LocalInterface.instance.referenceResolution.x / 2, (Input.mousePosition.y / Screen.height) * LocalInterface.instance.referenceResolution.y - LocalInterface.instance.referenceResolution.y / 2);
			Vector2 mousePos = new Vector2((Mouse.current.position.ReadValue().x / Screen.width) * LocalInterface.instance.referenceResolution.x - LocalInterface.instance.referenceResolution.x / 2, (Mouse.current.position.ReadValue().y / Screen.height) * LocalInterface.instance.referenceResolution.y - LocalInterface.instance.referenceResolution.y / 2);
			List<RaycastResult> results = new List<RaycastResult>();
			PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
			pointerEventData.position = Mouse.current.position.ReadValue();
			if(EventSystem.current == null || pointerEventData == null || Mouse.current == null)
			{
				return;
			}
			EventSystem.current.RaycastAll(pointerEventData, results);
			foreach(RaycastResult result in results)
			{
				if(result.gameObject == buttonImage.gameObject)
				{
					OnPointerEnter(pointerEventData);
					break;
				}
				if(shadowImage != null)
				{
					if(result.gameObject == shadowImage.gameObject)
					{
						OnPointerEnter(pointerEventData);
						break;
					}
				}
			}
		}
		UpdateColor();
	}
	
	public void ChangeButtonEnabled(bool newEnabledState)
	{
		if(buttonEnabled == newEnabledState)
		{
			return;
		}
		buttonEnabled = newEnabledState;
		ResetButton();
		// UpdateHotkey();
	}
	
	public void ChangeSpecialState(bool isSpecial)
	{
		// Debug.Log($"{this.name} change specialState called with intent to set special state to {isSpecial}");
		specialState = isSpecial;
		if(buttonEnabled)
		{
			if(changingColor)
			{
				StopCoroutine(colorChangeCoroutine);
			}
		}
		UpdateColor();
		// Debug.Log($"{this.name} finished change specialState to {specialState}");
	}
	
	public void OnPointerEnter(PointerEventData pointerEventData)
    {
		/* if(tickOnMouseOver)
		{
			SoundManager.instance.PlayTickSound();
		} */
		if(isButton && !buttonEnabled)
		{
			return;
		}
		if(expandEnabled)
		{
			if(changingScale)
			{
				StopCoroutine(scaleChangeCoroutine);
			}
			scaleChangeCoroutine = ChangeScale(new Vector3(expansionFactor, expansionFactor, 1f), expansionDuration);
			StartCoroutine(scaleChangeCoroutine);
		}
		if(changeColorEnabled)
		{
			if(changingColor)
			{
				StopCoroutine(colorChangeCoroutine);
			}
			if(colorChangeIsMultiplicative)
			{
				if(specialState)
				{
					colorChangeCoroutine = ChangeColor(specialStateColor * mouseOverColor, changeColorDuration);
				}
				else
				{
					colorChangeCoroutine = ChangeColor(baseColor * mouseOverColor, changeColorDuration);
				}
			}
			else
			{
				colorChangeCoroutine = ChangeColor(mouseOverColor, changeColorDuration);
			}
			StartCoroutine(colorChangeCoroutine);
		}
		if(isButton)
		{
			mouseOverButton = true;
			if(holdingDown)
			{
				if(movingImage)
				{
					StopCoroutine(moveImageCoroutine);
				}
				moveImageCoroutine = MoveImage(buttonImageOrigin + buttonImageDestinationAdditive, moveImageDuration);
				StartCoroutine(moveImageCoroutine);
				if(checkingForGlobalMouseUp)
				{
					StopCoroutine(checkForGlobalMouseUpCoroutine);
					checkingForGlobalMouseUp = false;
				}
			}
		}
	}
	
	public void OnPointerExit(PointerEventData pointerEventData)
    {
		MouseExit();
	}
	
	public void MouseExit()
	{
		if(isButton)
		{
			mouseOverButton = false;
		}
		if(isButton && !buttonEnabled)
		{
			return;
		}
		if(expandEnabled)
		{
			if(changingScale)
			{
				StopCoroutine(scaleChangeCoroutine);
			}
			scaleChangeCoroutine = ChangeScale(Vector3.one, expansionDuration);
			StartCoroutine(scaleChangeCoroutine);
		}
		if(changeColorEnabled)
		{
			if(changingColor)
			{
				StopCoroutine(colorChangeCoroutine);
			}
			if(specialState)
			{
				colorChangeCoroutine = ChangeColor(specialStateColor, changeColorDuration);
			}
			else
			{
				colorChangeCoroutine = ChangeColor(baseColor, changeColorDuration);
			}
			StartCoroutine(colorChangeCoroutine);
		}
		if(isButton)
		{
			mouseOverButton = false;
			if(holdingDown)
			{
				if(movingImage)
				{
					StopCoroutine(moveImageCoroutine);
				}
				moveImageCoroutine = MoveImage(buttonImageOrigin, moveImageDuration);
				StartCoroutine(moveImageCoroutine);
				checkForGlobalMouseUpCoroutine = CheckForGlobalMouseUp();
				StartCoroutine(checkForGlobalMouseUpCoroutine);
			}
		}
	}
	
	public void OnPointerDown(PointerEventData pointerEventData)
	{
		StartClickingButton();
	}
	
	public void StartClickingButton()
	{
		if(!isButton || !buttonEnabled)
		{
			return;
		}
		holdingDown = true;
		if(moveImageWhenClicked)
		{
			if(movingImage)
			{
				StopCoroutine(moveImageCoroutine);
			}
			moveImageCoroutine = MoveImage(buttonImageOrigin + buttonImageDestinationAdditive, moveImageDuration);
			StartCoroutine(moveImageCoroutine);
		}
	}
	
	public void ExectuteButtonPress()
	{
		/* if(playClickingSound && mouseOverButton && holdingDown)
		{
			if(clickSound == SoundManager.instance.clickSound)
			{
				SoundManager.instance.PlayClickSound();
			}
			else if(Preferences.instance.soundOn && (Application.isFocused || (!Application.isFocused && !Preferences.instance.muteOnFocusLost)))
			{
				soundSource.PlayOneShot(clickSound, Preferences.instance.soundVolume * volumeFactor);
			}
		} */
		if(clickSound != null && soundSource != null)
		{
			soundSource.PlayOneShot(clickSound, volumeFactor);
		}
		if(moveImageWhenClicked)
		{
			if(movingImage)
			{
				StopCoroutine(moveImageCoroutine);
			}
			moveImageCoroutine = MoveImage(buttonImageOrigin, moveImageDuration);
			StartCoroutine(moveImageCoroutine);
		}
		if(!isButton || !buttonEnabled)
		{
			return;
		}
		if(onDoubleClickEvent.GetPersistentEventCount() > 0)
		{
			if(Time.time - timeOfLastClick > 0.4f)
			{
				clicksInARow = 0;
			}
			timeOfLastClick = Time.time;
			clicksInARow++;
			if(mouseOverButton && holdingDown)
			{
				holdingDown = false;
				if(clicksInARow >= 2)
				{
					onDoubleClickEvent.Invoke();
				}
				else
				{
					onClickEvent.Invoke();
				}
			}
		}
		else
		{
			if(mouseOverButton && holdingDown)
			{
				holdingDown = false;
				onClickEvent.Invoke();
			}
		}
/* 		if(ControllerSelection.instance.usingController)
		{
			mouseOverButton = false;
			// ResetButton();
		} */
	}
	
/* 	public void SelectWithController()
	{
		mouseOverButton = true;
		holdingDown = true;
		ExectuteButtonPress();
	} */
	
	public void OnPointerUp(PointerEventData pointerEventData)
	{
		ExectuteButtonPress();
	}
	
	private IEnumerator ChangeScale(Vector3 destinationScale, float duration)
	{
		changingScale = true;
		Vector3 startingScale = rt.localScale;
		float t = 0;
		while(t < duration)
		{
			t += Time.deltaTime;
			rt.localScale = new Vector3(Mathf.Lerp(startingScale.x, destinationScale.x, t / duration), Mathf.Lerp(startingScale.y, destinationScale.y, t / duration), 1f);
			yield return null;
		}
		rt.localScale = destinationScale;
		changingScale = false;
	}
	
	private IEnumerator ChangeColor(Color destinationColor, float duration)
	{
		changingColor = true;
		Color originColor = buttonImage.color;
		float t = 0;
		while(t < duration)
		{
			t += Time.deltaTime;
			buttonImage.color = Color.Lerp(originColor, destinationColor, t / duration);
			yield return null;
		}
		buttonImage.color = destinationColor;
		changingColor = false;
	}
	
	private IEnumerator MoveImage(Vector2 destination, float duration)
	{
		movingImage = true;
		Vector2 origin = buttonImageRT.anchoredPosition;
		float t = 0;
		while(t < duration)
		{
			t += Time.deltaTime;
			buttonImageRT.anchoredPosition = Vector2.Lerp(origin, destination, t / duration);
			yield return null;
		}
		buttonImageRT.anchoredPosition = destination;
		movingImage = false;
	}
	
	private IEnumerator CheckForGlobalMouseUp()
	{
		checkingForGlobalMouseUp = true;
		while(Input.GetMouseButton(0))
		{
			yield return null;
		}
		holdingDown = false;
		checkingForGlobalMouseUp = false;
	}
	
	/* public void UpdateHotkey()
	{
		if(controllerSelectableObject == null)
		{
			return;
		}
		if(!ControllerSelection.instance.usingController)
		{
			DisableHotkey();
			return;
		}
		if(!controllerSelectableObject.hasHotkey)
		{
			return;
		}
		hotkeyImage.gameObject.SetActive(true);
		if(controllerSelectableObject.hotkeyBottom)
		{
			if(controllerSelectableObject.buttonUsesImage)
			{
				controllerSelectableObject.buttonImageRT.anchoredPosition = controllerSelectableObject.buttonImagePositionWithHotkey;
			}
			else
			{
				labelRT.offsetMin = new Vector2(2f, 20f);
			}
			hotkeyRT.anchorMin = new Vector2(0.5f, 0);
			hotkeyRT.anchorMax = new Vector2(0.5f, 0);
			hotkeyRT.pivot = new Vector2(0.5f, 0);
			hotkeyRT.anchoredPosition = new Vector2(0, 2f);
		}
		else
		{
			if(controllerSelectableObject.buttonUsesImage)
			{
				controllerSelectableObject.buttonImageRT.anchoredPosition = controllerSelectableObject.buttonImagePositionWithHotkey;
			}
			else
			{
				labelRT.offsetMax = new Vector2(-20f, -2f);
			}
			hotkeyRT.anchorMin = new Vector2(1f, 0.5f);
			hotkeyRT.anchorMax = new Vector2(1f, 0.5f);
			hotkeyRT.pivot = new Vector2(1f, 0.5f);
			hotkeyRT.anchoredPosition = new Vector2(-2f, 0);
		}
		hotkeyImage.sprite = LocalInterface.instance.GetHotkeySpriteForAction(controllerSelectableObject.hotkeyActionName, buttonEnabled);
		if(controllerSelectableObject.changeFontSizeWithHotkey)
		{
			buttonLabel.ChangeFontSize(controllerSelectableObject.fontSizeWithHotkey);
		}
	} */
	
	/* public void DisableHotkey()
	{
		if(controllerSelectableObject == null)
		{
			return;
		}
		if(hotkeyImage != null && hotkeyImage.gameObject.activeSelf)
		{
			if(controllerSelectableObject.buttonUsesImage)
			{
				controllerSelectableObject.buttonImageRT.anchoredPosition = controllerSelectableObject.buttonImagePositionWithoutHotkey;
			}
			else
			{
				labelRT.offsetMin = new Vector2(2f, 2f);
				labelRT.offsetMax = new Vector2(-2f, -2f);
			}
			hotkeyImage.gameObject.SetActive(false);
		}
		if(controllerSelectableObject.changeFontSizeWithHotkey)
		{
			buttonLabel.ChangeFontSize(controllerSelectableObject.fontSizeWithoutHotkey);
		}
	} */
}

#if UNITY_EDITOR

public class ShowIfAttribute : PropertyAttribute
{
    public string[] ConditionFieldNames { get; private set; }
    public bool RequiredValue { get; private set; } = true;
    
    public ShowIfAttribute(params string[] conditionFieldNames)
    {
        ConditionFieldNames = conditionFieldNames;
    }

    public ShowIfAttribute(bool requiredValue, params string[] conditionFieldNames)
    {
        ConditionFieldNames = conditionFieldNames;
        RequiredValue = requiredValue;
    }
}

[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public class ShowIfPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ShowIfAttribute showIf = (ShowIfAttribute)attribute;
        bool shouldShow = true;

        foreach (string conditionFieldName in showIf.ConditionFieldNames)
        {
            SerializedProperty conditionProperty = property.serializedObject.FindProperty(conditionFieldName);
            
            if (conditionProperty == null) continue;
            
            bool conditionMet = conditionProperty.propertyType switch
            {
                SerializedPropertyType.Boolean => conditionProperty.boolValue == showIf.RequiredValue,
                _ => true // Default to showing if we don't know how to evaluate
            };

            shouldShow &= conditionMet;
        }

        if (shouldShow)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ShowIfAttribute showIf = (ShowIfAttribute)attribute;
        bool shouldShow = true;

        foreach (string conditionFieldName in showIf.ConditionFieldNames)
        {
            SerializedProperty conditionProperty = property.serializedObject.FindProperty(conditionFieldName);
            
            if (conditionProperty == null) continue;
            
            bool conditionMet = conditionProperty.propertyType switch
            {
                SerializedPropertyType.Boolean => conditionProperty.boolValue == showIf.RequiredValue,
                _ => true
            };

            shouldShow &= conditionMet;
        }

        return shouldShow ? EditorGUI.GetPropertyHeight(property, label) : -EditorGUIUtility.standardVerticalSpacing;
    }
}

#endif