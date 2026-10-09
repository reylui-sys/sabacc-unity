using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

public class prueba
{
    [MenuItem("Build/Build Peña para Windows")]
    public static void BuildForWindows()
    {
        string[] escenas = {
            "Assets/Scenes/Inicio.unity",
            "Assets/Scenes/Configuracion.unity",
            "Assets/Scenes/Creditos.unity",
            "Assets/Scenes/Tutorial.unity",
            "Assets/Scenes/ConnectToServer.unity",
            "Assets/Scenes/Lobby.unity",
            "Assets/Scenes/Peña_Main.unity"
        };

        // Preguntar número de jugadores (1 a 4) con diálogo simple
        int numJugadores = -1;
        if (EditorUtility.DisplayDialog("¿1 jugador?", "¿Generar solo 1 build?", "Sí", "No"))
            numJugadores = 1;
        else if (EditorUtility.DisplayDialog("¿2 jugadores?", "¿Generar 2 builds?", "Sí", "No"))
            numJugadores = 2;
        else if (EditorUtility.DisplayDialog("¿3 jugadores?", "¿Generar 3 builds?", "Sí", "No"))
            numJugadores = 3;
        else if (EditorUtility.DisplayDialog("¿4 jugadores?", "¿Generar 4 builds?", "Sí", "No"))
            numJugadores = 4;
        else
        {
            Debug.LogWarning("❌ Build cancelada: no se seleccionó número de jugadores.");
            return;
        }

        string folder = EditorUtility.SaveFolderPanel("Guardar builds", "", "Builds");
        if (string.IsNullOrEmpty(folder))
        {
            Debug.LogWarning("❌ Build cancelada: no se seleccionó carpeta.");
            return;
        }

        // Configuración
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        string originalName = PlayerSettings.productName;
        BuildOptions opciones = BuildOptions.Development | BuildOptions.AllowDebugging;

        // Generar builds
        for (int i = 1; i <= numJugadores; i++)
        {
            PlayerSettings.productName = $"Jugador {i}";
            string outputPath = $"{folder}/Jugador {i}/Jugador {i}.exe";

            var report = BuildPipeline.BuildPlayer(escenas, outputPath, BuildTarget.StandaloneWindows64, opciones);

            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log($"✔ Build creada: {outputPath}");
            else
                Debug.LogError($"❌ Falló la build para Jugador {i}. Revisa la consola para detalles.");
        }

        PlayerSettings.productName = originalName;
        Debug.Log($"🎉 {numJugadores} build(s) generada(s) en:\n{folder}");
    }
}