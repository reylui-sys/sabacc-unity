using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class MenuItemHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private UnityEngine.UI.Image image;
    [SerializeField] private MenuButtonManager manager;

    public void OnPointerEnter(PointerEventData eventData)
    {
        manager.OnButtonHover(tmpText, image);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // El último hover permanece activo hasta que otro lo cambie.
    }
}