using System;

/// <summary>
/// Log del núcleo sin depender de UnityEngine.
/// El núcleo (Sabacc.Core) no puede usar Debug.Log porque su asmdef tiene
/// "No Engine References". La capa de Unity conecta estas salidas a Debug.Log
/// al arrancar (ver UnityCoreLogBridge). En los tests quedan en silencio
/// salvo que un test las sustituya para comprobar qué se ha registrado.
/// </summary>
public static class CoreLog
{
    public static Action<string> Info = _ => { };
    public static Action<string> Warning = _ => { };
    public static Action<string> Error = _ => { };
}
