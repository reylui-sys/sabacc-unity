using UnityEngine;
using TMPro;
using System.Collections;

public class LoadingAnimation : MonoBehaviour
{
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private string baseText = "Esperando a los demás jugadores";
    [SerializeField] private float interval = 0.5f; // tiempo entre cada punto

    private int dotCount = 0;
    private const int maxDots = 3;

    void Start()
    {
        if (loadingText == null)
            loadingText = GetComponent<TMP_Text>();

        StartCoroutine(AnimateDots());
    }

    IEnumerator AnimateDots()
    {
        while (true)
        {
            string dots = new string('.', dotCount);
            loadingText.text = baseText + dots;
            dotCount = (dotCount + 1) % (maxDots + 1); // 0, 1, 2, 3 → luego vuelve a 0
            yield return new WaitForSeconds(interval);
        }
    }
}