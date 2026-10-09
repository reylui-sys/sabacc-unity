using UnityEngine;

/// <summary>
/// Conecta el log del núcleo (CoreLog, sin dependencias de Unity) con la
/// consola de Unity. Se ejecuta solo al arrancar el juego, antes de cargar
/// la primera escena; no hace falta ponerlo en ningún GameObject.
/// </summary>
internal static class UnityCoreLogBridge
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        CoreLog.Info = message => Debug.Log(message);
        CoreLog.Warning = message => Debug.LogWarning(message);
        CoreLog.Error = message => Debug.LogError(message);
    }
}
