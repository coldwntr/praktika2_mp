using System;
using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class ServerBootstrap : MonoBehaviour
{
    private const ushort DefaultPort = 7770;

    private void Start()
    {
        if (!Application.isBatchMode)
            return;

        if (InstanceFinder.NetworkManager == null)
        {
            Debug.LogWarning("[ServerBootstrap] FishNet NetworkManager was not found.");
            return;
        }

        DisableSceneUi();

        ushort port = ResolvePort();

        Transport transport = InstanceFinder.TransportManager.Transport;
        if (transport is Tugboat tugboat)
            tugboat.SetPort(port);
        else if (transport != null)
            transport.SetPort(port);

        if (InstanceFinder.IsServerStarted)
        {
            Debug.Log($"[ServerBootstrap] Server is already started on port {port}.");
            return;
        }

        InstanceFinder.ServerManager.StartConnection();
        Debug.Log($"[ServerBootstrap] Starting dedicated server on port {port}");
    }

    private static ushort ResolvePort()
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "-port", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ushort.TryParse(args[i + 1], out ushort parsedPort))
                return parsedPort;

            Debug.LogWarning($"[ServerBootstrap] Invalid -port value '{args[i + 1]}'. Using default port {DefaultPort}.");
            break;
        }

        return DefaultPort;
    }

    private static void DisableSceneUi()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null)
                canvas.gameObject.SetActive(false);
        }
    }
}
