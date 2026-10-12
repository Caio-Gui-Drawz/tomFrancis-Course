using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Adicionado automaticamente a todo Button da cena pelo AudioEvents — não precisa colocar à mão.
[RequireComponent(typeof(Button))]
public class ButtonSounds : MonoBehaviour, IPointerEnterHandler
{
    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(AudioEvents.PlayButtonClick);
    }

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(AudioEvents.PlayButtonClick);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && button.interactable) AudioEvents.PlayButtonHover();
    }
}
