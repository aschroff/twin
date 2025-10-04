using System.Collections;
using UnityEngine;
using Code;
using Code.AI;
using Code.Proc.Meshcapade;
using Lean.Gui;


public class AvatarProcess : Process
{
    private MeshcapadeAvatarClient client;

    private void Awake()
    {
        // Ensure a client exists on this GameObject
        client = gameObject.AddComponent<MeshcapadeAvatarClient>();
    }

    public override ProcessResult Execute(string variant = "")
    {
        Debug.Log("AvatarProcess: Starting avatar creation...");

        // Kick off async avatar creation
        client.CreateAvatarFromImage(
            GetMyImageCoroutine,
            OnAvatarComplete,
            OnAvatarError
        );

        // Return immediately, process will finish async
        return new ProcessResult { code = 0 };
    }

    // Example image provider
    private IEnumerator GetMyImageCoroutine()
    {
        // TODO: Replace with your own image acquisition (screenshot, file, webcam, etc.)
        string path = Application.dataPath + "/test.jpg";
        byte[] bytes = System.IO.File.ReadAllBytes(path);
        yield return bytes;
    }

    private void OnAvatarComplete(MeshcapadeAvatarClient.AvatarResult result)
    {
        Debug.Log($"✅ Avatar ready! ID={result.avatarId}, Asset={result.assetUrl}");

        // Example: notify UI or save result
        getNotification()?.Pulse(); // Using LeanPulse

    }

    private void OnAvatarError(string error)
    {
        Debug.LogError($"❌ Avatar creation failed: {error}");

        // Example: show user a notification
        getNotification()?.Pulse();
    }
}