using UnityEngine;
using UnityEditor;

public class BuildWindows
{
    [MenuItem("Build/Build 2-4 Jugadores Windows")]
    public static void BuildForWindows()
    {
        string[] escenas = {
            "Assets/Scenes/Inicio.unity",
            "Assets/Scenes/Configuracion.unity",
            "Assets/Scenes/Creditos.unity",
            "Assets/Scenes/Tutorial.unity",
            "Assets/Scenes/ConnectToServer.unity",
            "Assets/Scenes/Lobby.unity",
            "Assets/Scenes/MainScene.unity"
        };

        // --- Selección de carpeta ---
        string folder = EditorUtility.SaveFolderPanel(
            "Elige la carpeta donde guardar las builds",
            "",
            "Builds"
        );

        if (string.IsNullOrEmpty(folder))
        {
            Debug.LogWarning("❌ Build cancelada: no se seleccionó carpeta.");
            return;
        }

        // --- Configuración de ventana ---
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        //PlayerSettings.fullScreenMode = FullScreenMode.ExclusiveFullScreen; // Pantalla completa
        //PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; // Pantalla completa sin bordes

        // Guardar nombre original del proyecto
        string originalName = PlayerSettings.productName;

        // Opciones de build
        BuildOptions opciones = BuildOptions.Development | BuildOptions.AllowDebugging;
        // Si NO las quieres "development", usa:
        // BuildOptions.None;

        // --- Crear builds para Jugador 1 a 4 ---
        for (int i = 1; i <= 4; i++)
        {
            string nombreJugador = "Jugador " + i;

            // Cambiar el nombre de la ventana
            PlayerSettings.productName = nombreJugador;

            // Ruta final elegida por el usuario
            string outputPath = $"{folder}/Jugador {i}/{nombreJugador}.exe";

            // Hacer build
            BuildPipeline.BuildPlayer(
                escenas,
                outputPath,
                BuildTarget.StandaloneWindows64,
                opciones
            );

            Debug.Log($"✔ Build creada: {outputPath}");
        }

        // Restaurar nombre original
        PlayerSettings.productName = originalName;

        Debug.Log("🎉 Todas las builds para Windows han sido creadas en:");
        Debug.Log(folder);
    }
}
