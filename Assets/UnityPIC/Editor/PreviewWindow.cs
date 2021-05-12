using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


public class PreviewWindow : EditorWindow
{
    [MenuItem ("Window/Point Cloud Renderer")]
    
    public static void ShowWindow() {
        EditorWindow.GetWindow(typeof(PreviewWindow));
    }

    private void OnGUI() {
        Debug.Log(Camera.current?.pixelHeight);
        Debug.Log(Camera.current?.pixelWidth);
        Debug.Log(Camera.main.pixelHeight);
        Debug.Log(Camera.main.pixelWidth);
    }
}
