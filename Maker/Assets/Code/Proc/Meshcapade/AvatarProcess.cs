using System.Collections;
using UnityEngine;
using Code;
using Code.AI;
using Code.Proc.Meshcapade;
using Lean.Gui;
using UnityEngine.UI;


public class AvatarProcess : Process
{
    private MeshcapadeAvatarClient client;
    [SerializeField] string imagePath;

    public void SetImagePath(string path)
    {
        imagePath=path;
    }

    private void Awake()
    {
        // Ensure a client exists on this GameObject
        client = gameObject.AddComponent<MeshcapadeAvatarClient>();
    }

    public override ProcessResult Execute(string variant = "")
    {
        Debug.Log("AvatarProcess: Starting avatar creation...");
        client.username = getSettingsManager().getInput("Meshcapade User");
        client.password = getSettingsManager().getInput("Meshcapade Password");
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
        string path = imagePath;
        byte[] bytes = System.IO.File.ReadAllBytes(path);
        yield return bytes;
    }

    private void OnAvatarComplete(MeshcapadeAvatarClient.AvatarResult result)
    {
        Debug.Log($"✅ Avatar ready! ID={result.avatarId}, Asset={result.assetUrl}");

        LeanPulse notification = getNotification();
        foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
        {
            text.text = $"Body from image succeeded ID={result.avatarId}.";
        }

        notification.Pulse();

    }

    private void OnAvatarError(string error)
    {
        Debug.LogError($"❌ Avatar creation failed: {error}");

        LeanPulse notification = getNotification();
        foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
        {
            text.text = "Body from image failed.";
        }

        notification.Pulse();
    }
}