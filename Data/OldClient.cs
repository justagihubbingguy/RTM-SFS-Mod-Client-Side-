using LiteNetLib;
using SFS.World;
using LiteNetLib.Utils;
using UnityEngine;

public class SfsClientOld : MonoBehaviour
{
    public static SfsClientOld Instance;
    private NetManager _client;
    private EventBasedNetListener _listener;
    private NetDataWriter writer = new NetDataWriter();
    private NetPeer _serverPeer;

    private const string ConnectionKey = "SFS_Multiplayer";
    public bool isServer = false;
    /*
        mult1player sending..😊😊, sending...😊😁

        t0rture😝😝😊😝
    */
    public void Awake()
    {
        Instance = this;
    }
    public void OnDestroy() 
    {
        if (_client != null)
        {
        _client.Stop(); 
        Debug.Log("Networking threads stopped, process ready to exit.");
        }
    }
    //t-the f-ffunction t-to start the n-net...-.
    public void Start()
{
    _listener = new EventBasedNetListener();
    _client = new NetManager(_listener);

    _listener.ConnectionRequestEvent += request =>
    {
        request.AcceptIfKey(ConnectionKey);
    };
    
    _listener.NetworkReceiveEvent += (peer, reader, channel, deliveryMethod) => {
        if (reader.AvailableBytes >= 24)
        {

            double x = reader.GetDouble();
            double y = reader.GetDouble();
            double rot = reader.GetDouble();

            HandleIncomingRocketData(x, y, rot);
        } 
        else
        {
            string msg = reader.GetString();
            Debug.Log($"Received text from {peer.Address}: {msg}");
            
        }
        
        reader.Recycle();
    };

    _listener.PeerConnectedEvent += peer => {
        Debug.Log("Peer connected: " + peer.Address);
    };

    if (isServer)
    {
        _client.Start(9050);
        Debug.Log("Server started on port 9050");
    }
    else
    {
        _client.Start();
        _serverPeer = _client.Connect("127.0.0.1", 9050, "SFS_Multiplayer");
        Debug.Log("Connecting to server...");
    }
}
    public void SendMessageToServer(string msg)
    {
        NetDataWriter writer = new NetDataWriter();
        writer.Put(msg);
        _serverPeer.Send(writer, DeliveryMethod.ReliableOrdered);
    }
    public void HandleIncomingRocketData(double x, double y, double rot)
    {
        Debug.Log($"Rocket Pos at: {x}, {y}");
    }

    public void SendRocketData()
    {
        /*
            Sending the rocket data to the server (x,y,rot)
            ,im gonna add the rest later.
        */
        var playerController = PlayerController.main;
        if(playerController != null && playerController.player.Value != null)
        {
            var currentRocket = playerController.player.Value;
            var position = currentRocket.location.position.Value;

            writer.Reset();

            writer.Put(position.x);
            writer.Put(position.y);
            writer.Put(position.AngleRadians);
            if (!isServer && _serverPeer != null)
            {
                _serverPeer.Send(writer, DeliveryMethod.Unreliable);
            }
            else if (isServer)
            {
                _client.SendToAll(writer, DeliveryMethod.Unreliable);
            }
        }
    }
    private float timer;
    public void Update()
    {
        _client.PollEvents();
        timer += Time.deltaTime;

        if (timer >= 0.05f)
        {
            timer = 0f;

            if (!isServer)
            SendRocketData();
        }
    }
}