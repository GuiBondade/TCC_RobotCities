#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[InitializeOnLoad]
public static class MultiSceneBootstrapper
{
    // Lembre de ajustar os nomes aqui para os nomes reais das suas cenas persistentes!
    private static readonly HashSet<string> cenasPersistentes = new HashSet<string>
    {
        "UIScene",            
        "Teste",            
        "MapScene"      
    };

    private static List<string> mapasExtrasParaRestaurar = new List<string>();

    static MultiSceneBootstrapper()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            mapasExtrasParaRestaurar.Clear();

            for (int i = EditorSceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = EditorSceneManager.GetSceneAt(i);
                
                if (!cenasPersistentes.Contains(scene.name))
                {
                    if (scene.isLoaded)
                    {
                        mapasExtrasParaRestaurar.Add(scene.path);
                        // O 'true' aqui garante o Unload completo e limpo para não dar o erro de travar o LoadSceneAsync
                        EditorSceneManager.CloseScene(scene, true); 
                    }
                }
            }
        }

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            foreach (string scenePath in mapasExtrasParaRestaurar)
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }
        }
    }
}
#endif
