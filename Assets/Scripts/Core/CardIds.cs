/// <summary>
/// Única fuente de verdad para convertir el nombre de una carta en su ID
/// (el mismo nombre que su prefab). Antes la limpieza estaba duplicada en
/// SabaccCard.GetCardId y en SabaccCardDefinitions.GetCardById, y una de las
/// dos copias tenía los caracteres corruptos ("Ã±" en vez de "ñ").
/// </summary>
public static class CardIds
{
    public static string CleanName(string name)
    {
        return name
            .Replace(" ", "_")
            .Replace("ñ", "n").Replace("Ñ", "N")
            .Replace("á", "a").Replace("é", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ú", "u");
    }

    public static string For(string suit, string name)
    {
        string cleanName = CleanName(name);
        return suit == null ? cleanName : suit + "_" + cleanName;
    }
}
