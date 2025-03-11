using UnityEngine;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Collections.Concurrent;
//this is for sending out the image from the dron to the python script its called CameraAgents
//this code is in folder Codes in the Assets
public class AgentVisionController : MonoBehaviour
{
    [SerializeField] private int captureWidth = 640;
    [SerializeField] private int captureHeight = 480;
    [SerializeField] private int streamPort = 5123; // Cambiado para coincidir con Python
    [SerializeField] private int quality = 75;
    [SerializeField] private float captureInterval = 0.033f;

    private Camera agentCamera;
    private RenderTexture renderTexture;
    private Texture2D screenShot;
    private int agentId;
    private bool isCapturing = true;
    private RobotAgent robotAgent;

    private UdpClient udpClient;
    private Thread streamThread;
    private bool isStreaming = true;
    private ConcurrentQueue<byte[]> frameQueue = new ConcurrentQueue<byte[]>();

    void Start()
    {
        InitializeCamera();
        InitializeStreaming();
        StartCoroutine(CaptureFrames());
    }

    void InitializeCamera()
    {
        robotAgent = GetComponent<RobotAgent>();
        if (robotAgent == null)
        {
            Debug.LogError("RobotAgent component not found!");
            return;
        }
        agentId = robotAgent.id;
        Debug.Log($"Initializing camera for Agent {agentId}"); // Debug log

        GameObject cameraObj = new GameObject($"AgentCamera_{agentId}");
        cameraObj.transform.SetParent(transform);
        cameraObj.transform.localPosition = new Vector3(0, 0f, 0.0737f);
        cameraObj.transform.localRotation = Quaternion.Euler(10, 0, 0);

        agentCamera = cameraObj.AddComponent<Camera>();
        agentCamera.fieldOfView = 60;
        agentCamera.nearClipPlane = 0.1f;
        agentCamera.farClipPlane = 80f;

        renderTexture = new RenderTexture(captureWidth, captureHeight, 24);
        screenShot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
        agentCamera.targetTexture = renderTexture;
        
        Debug.Log($"Camera initialized for Drone {agentId}"); // Debug log
    }

    void InitializeStreaming()
    {
        try
        {
            udpClient = new UdpClient();
            streamThread = new Thread(StreamFrames);
            streamThread.Start();
            Debug.Log($"Drone {agentId}: Started UDP streaming on port {streamPort + agentId}"); // Debug log
        }
        catch (Exception e)
        {
            Debug.LogError($"Drone {agentId}: Failed to initialize streaming: {e.Message}");
        }
    }

    IEnumerator CaptureFrames()
    {
        WaitForSeconds waitInterval = new WaitForSeconds(captureInterval);
        int frameCount = 0;

        while (isStreaming)
        {
            if (isCapturing)
            {
                RenderTexture.active = renderTexture;
                agentCamera.Render();
                screenShot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                screenShot.Apply();
                RenderTexture.active = null;

                byte[] frameData = screenShot.EncodeToJPG(quality);
                byte[] header = BitConverter.GetBytes(agentId);
                byte[] packetData = new byte[header.Length + frameData.Length];
                header.CopyTo(packetData, 0);
                frameData.CopyTo(packetData, header.Length);

                frameQueue.Enqueue(packetData);
                
                frameCount++;
                if (frameCount % 10000 == 0) // Log cada 10000 frames
                {
                    Debug.Log($"Drone {agentId}: Captured frame {frameCount}");
                }
            }

            yield return waitInterval;
        }
    }

    void StreamFrames()
    {
        int sentCount = 0;
        while (isStreaming)
        {
            try
            {
                if (frameQueue.TryDequeue(out byte[] frameData))
                {
                    udpClient.Send(frameData, frameData.Length, "127.0.0.1", streamPort + agentId);
                    sentCount++;
                    if (sentCount % 10000 == 0) // Log cada 10000 frames enviados
                    {
                        Debug.Log($"Drone {agentId}: Sent frame {sentCount} to port {streamPort + agentId}");
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Drone {agentId} streaming error: {e.Message}");
                Thread.Sleep(1000);
            }
        }
    }

    void OnDestroy()
    {
        isStreaming = false;
        if (streamThread != null && streamThread.IsAlive)
        {
            streamThread.Join(1000);
        }
        if (udpClient != null)
        {
            udpClient.Close();
        }
        if (renderTexture != null)
        {
            renderTexture.Release();
        }
        if (screenShot != null)
        {
            Destroy(screenShot);
        }
    }
}