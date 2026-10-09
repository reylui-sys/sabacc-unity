using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class MenuButtonManager : MonoBehaviour
{
    [SerializeField] private TMP_Text defaultActiveText; // El texto que empieza en blanco ("Jugar")

    [SerializeField] private Color colorInactive = new Color(1f, 1f, 1f, 132f / 255f); // blanco semitransparente
    [SerializeField] private Color colorActive = Color.white;    // Blanco al hover o activo

    private TMP_Text currentActiveText; // El texto actualmente en blanco

    [SerializeField] private Color colorInactiveImage = new Color(1f, 1f, 1f, 132f / 255f); // blanco semitransparente
    [SerializeField] private Color colorActiveImage = Color.white;

    private UnityEngine.UI.Image currentActiveImage; // Imagen actualmente activa

    void Start()
    {
        // Inicializamos el botón por defecto en blanco
        SetActiveText(defaultActiveText);
    }

    public void OnButtonHover(TMP_Text tmpText, UnityEngine.UI.Image image = null)
    {
        // Si ya está activo, no hacemos nada
        if (currentActiveText == tmpText) return;

        // Desactivamos el anterior
        if (currentActiveText != null)
            currentActiveText.color = colorInactive;

        if (currentActiveImage != null)
        currentActiveImage.color = colorInactiveImage;

        // Activamos el nuevo
        tmpText.color = colorActive;
        currentActiveText = tmpText;

        if (image != null)
        {
            image.color = colorActiveImage;
            currentActiveImage = image;
        }
    }

    public void SetActiveText(TMP_Text tmpText)
    {
        // Desactivamos el anterior
        if (currentActiveText != null)
            currentActiveText.color = colorInactive;

        // Activamos el nuevo
        tmpText.color = colorActive;
        currentActiveText = tmpText;
    }
}