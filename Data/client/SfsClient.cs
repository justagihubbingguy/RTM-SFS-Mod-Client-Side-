/*
    MIT License

    Copyright (c) 2026 justagihubbingguy

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/
/*
    SfsClient.cs  (ntsan)
*/
/*
    This is the underlying source code for the Real Time Multiplayer Mod.

    pub fn RTM_SFS_Mod_Team() -> Player {

        Ntsan (justagihubbingguy): Built the underlying rocket packet system, synchronization, peer to peer and client.

        KAMO (The-GuyMC): Built the connection system, lobby system, room system, ownership and server.

        Pl3trix (parodoscki): Built the hooking system for iOS, UI for iOS, and full support for iOS (thank you for suffering with raw pointers).

        Arctic (Ice) : Built the system for Android, UI for Android, and full support for Android (thank you for suffering with raw pointers).
    }
*/
using LiteNetLib;
using SFS.World;
using LiteNetLib.Utils;
using UnityEngine;
using SFS.Parsers.Json;
using SFS.Builds;
using SFS.Parts.Modules;
using System.Linq;
using SFS.Parts;
public enum PacketType
{
    PositionUpdate = 0,
    BlueprintSpawn = 1,
    DespawnRocket = 2,
    LobbyInfo = 3
}
public class SfsClient : MonoBehaviour
{

    public static SfsClient Instance;
    private NetManager _client;
    private EventBasedNetListener _listener;
    private System.Collections.Concurrent.ConcurrentQueue<Blueprint> _blueprintQueue = new System.Collections.Concurrent.ConcurrentQueue<Blueprint>();
    private NetDataWriter writer = new NetDataWriter();
    private NetPeer _serverPeer;
    private RTMSFS.RocketHelper _rocketHelper = new RTMSFS.RocketHelper();
    private bool hasReceivedFirstPacket = false;
    private bool wasRocketActive = false;
    private Double2 targetPlanetPos;
    private Double2 targetPlanetVel;
    
    // collision sync (in maintenance)
    private bool isFirstPhysicsFrame = true;
    private Collider2D[] cachedRemoteColliders;
    private Collider2D[] cachedLocalColliders;
    private Rigidbody2D[] cachedRemoteRigidbodies;
    private Rocket cachedLocalRocketRef;

    // engine sync (finished)
    private float targetThrottle;
    private bool[] targetEngineStates = new bool[0];
    private EngineModule[] cachedRemoteEngines;

    // wheel sync (finished)
    private WheelModule[] cachedRemoteWheels;
    private bool[] targetWheelStates;
    private float[] targetWheelVelocities;

    // rcs sync (finished)
    private RcsModule[] cachedRemoteRcs;
    private bool targetRcsState;
    private float targetTurnAxis;
    private Vector2 targetDirectionalAxis;

    // rocket sync (finished)
    private Rocket cachedRemoteRocket;
    private float targetRotDegrees;
    private const string ConnectionKey = "SFS_Multiplayer";
    public bool isServer = false;

    public void Awake()
    {
        Instance = this;
    }
    public void OnDestroy() 
    {
        if (_client != null)
        {
            _client.Stop(); 
            Debug.Log("Ready to exit.");
        }
    }
    //starting.
    public void Start()
    {

        _listener = new EventBasedNetListener();
        _client = new NetManager(_listener);

        _listener.ConnectionRequestEvent += request =>
        {
            request.AcceptIfKey(ConnectionKey);
        };
        
        // Recieving Event
        _listener.NetworkReceiveEvent += (peer, reader, channel, deliveryMethod) => {
            PacketType packetType = (PacketType)reader.GetByte();

            switch (packetType)
            {

                case PacketType.PositionUpdate:

                    double x = reader.GetDouble();
                    double y = reader.GetDouble();

                    double velx = reader.GetDouble();
                    double vely = reader.GetDouble();

                    double rot = reader.GetDouble();

                    float throttle = reader.GetFloat();

                    byte engineCount = reader.GetByte();
                    bool[] engineStates = new bool[engineCount];

                    for(int i = 0; i < engineCount; i++)
                    {
                        engineStates[i] = reader.GetBool();
                    }

                    byte wheelCount = reader.GetByte();
                    bool[] wheelStates = new bool[wheelCount];
                    float[] wheelVelocities = new float[wheelCount];
                    for (int i = 0; i < wheelCount; i++)
                    {
                        wheelStates[i] = reader.GetBool();
                        wheelVelocities[i] = reader.GetFloat();
                    } 
                    bool isRcsOn = reader.GetBool();
                    float turnAxis = reader.GetFloat();
                    float dirX = reader.GetFloat();
                    float dirY = reader.GetFloat();


                    HandleIncomingRocketData(
                        x, y, velx, vely, rot,
                        throttle, engineStates, wheelStates,
                        wheelVelocities, isRcsOn, turnAxis, 
                        new Vector2(dirX, dirY)
                    );


                    if (isServer)
                    {

                        NetDataWriter mirror = new NetDataWriter();
                        
                        mirror.Put((byte)PacketType.PositionUpdate);
                        mirror.Put(x);
                        mirror.Put(y);
                        mirror.Put(velx);
                        mirror.Put(vely);
                        mirror.Put(rot);

                        mirror.Put(throttle);
                        mirror.Put(engineCount);

                        for (int i = 0; i < engineCount; i++)
                        {
                            mirror.Put(engineStates[i]);
                        }

                        mirror.Put(wheelCount);

                        for (int i = 0; i < wheelCount; i++)
                        {
                            mirror.Put(wheelStates[i]);
                            mirror.Put(wheelVelocities[i]);
                        }

                        mirror.Put(isRcsOn);
                        mirror.Put(turnAxis);

                        mirror.Put(dirX);
                        mirror.Put(dirY);

                        // use .( if broken 9ol.(

                        _client.SendToAll(mirror, DeliveryMethod.Unreliable, peer);
                    }
                    break;

                case PacketType.BlueprintSpawn:

                    string jsonBlueprint = reader.GetString();
                    HandleBlueprintData(jsonBlueprint);

                    break;
                case PacketType.DespawnRocket:

                    HandleRemoteDespawnRocketry();

                    if (isServer)
                    {

                        NetDataWriter mirror = new NetDataWriter();
                        mirror.Put((byte)PacketType.DespawnRocket);
                        _client.SendToAll(mirror, DeliveryMethod.ReliableOrdered, peer);
                    }

                    break;
                case PacketType.LobbyInfo:
                    string roomCode = reader.GetString();
                    Debug.Log($"Connected to room code: {roomCode}");
                    break;

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
    public void RedoColliderCache()
    {

        var currentLocalRocket = PlayerController.main?.player?.Value as Rocket;

        if (currentLocalRocket != null)
        {

            cachedLocalRocketRef = currentLocalRocket;
            cachedLocalColliders = currentLocalRocket.GetComponentsInChildren<Collider2D>(true);
            currentLocalRocket.rb2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (cachedRemoteRocket != null)
        {

            cachedRemoteRigidbodies = cachedRemoteRocket.GetComponentsInChildren<Rigidbody2D>(true);
            cachedRemoteColliders = cachedRemoteRocket.GetComponentsInChildren<Collider2D>(true);

            foreach (var rb in cachedRemoteRigidbodies)
            {

                if (rb == null) continue;
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.simulated = true;
                rb.useFullKinematicContacts = true;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;


                rb.freezeRotation = true;
            }

            int targetPartLayer = (cachedLocalColliders != null && cachedLocalColliders.Length > 0)
                ? cachedLocalColliders[0].gameObject.layer
                : LayerMask.NameToLayer("Default");

            foreach (var col in cachedRemoteColliders)
            {
                if (col == null) continue;
                col.gameObject.layer = targetPartLayer;
                col.enabled = true;
                col.isTrigger = false;
            }

            if (cachedLocalColliders != null)
            {
                float distance = Vector2.Distance(cachedLocalRocketRef.rb2d.position, cachedRemoteRocket.rb2d.position);
                bool isOverlappedOnSpawn = distance < 8f;
                for (int i = 0; i < cachedLocalColliders.Length; i++)
                {
                    var localCollider = cachedLocalColliders[i];
                    if (localCollider == null) continue;

                    for (int j = 0; j < cachedRemoteColliders.Length; j++)
                    {
                        var remoteCollider = cachedRemoteColliders[j];
                        if (remoteCollider != null)
                        {
                            Physics2D.IgnoreCollision(localCollider, remoteCollider, isOverlappedOnSpawn);
                        }
                    }
                }
            }
        }
    }
    public void HandleRemoteDespawnRocketry()
    {   
        // the holy nulls
        cachedRemoteRocket = null;
        cachedRemoteEngines = null;
        cachedRemoteWheels = null;
        cachedRemoteRcs = null;
        cachedRemoteColliders = null;
        cachedLocalColliders = null;
        cachedRemoteRigidbodies = null;
        cachedLocalRocketRef = null;

        var remoteObjects = 
        FindObjectsByType<Rocket>(FindObjectsSortMode.None)
        .Where(r => r.rocketName == "RemotePeer");
        
        foreach(var remote in remoteObjects)
        {
            if (remote != null && remote.gameObject != null)
            {
                Destroy(remote.gameObject);
            }
        }
    }
    public void SendDespawn()
    {

        NetDataWriter despawnWriter = new NetDataWriter();

        despawnWriter.Put((byte)PacketType.DespawnRocket);

        if (!isServer && _serverPeer != null)
        {

            _serverPeer.Send(despawnWriter,DeliveryMethod.ReliableOrdered);

        }
        else if (isServer)
        {

            _client.SendToAll(despawnWriter,DeliveryMethod.ReliableOrdered);

        }

    }

    public void HandleIncomingRocketData(
        double x, double y, double velx, double vely, double rot, float throttle,
         bool[] engineStates, bool[] wheelStates, float[] wheelVelocities, bool rcsOn, float turnAxis, Vector2 dirAxis
        )
    {
        targetPlanetPos = new Double2(x, y);
        targetPlanetVel = new Double2(velx, vely);
        targetRotDegrees = (float)(rot * Mathf.Rad2Deg);
        targetThrottle = throttle;
        targetEngineStates = engineStates;
        targetWheelStates = wheelStates;
        targetWheelVelocities = wheelVelocities;
        targetRcsState = rcsOn;
        targetTurnAxis = turnAxis;
        targetDirectionalAxis = dirAxis;
        
        if (!hasReceivedFirstPacket)
        {
            hasReceivedFirstPacket = true;
            Rocket remoteRocket = FindObjectsByType<Rocket>(FindObjectsSortMode.None).FirstOrDefault(r => r.rocketName == "RemotePeer");
            if (remoteRocket != null && remoteRocket.physics?.location != null)
            {
                remoteRocket.physics.location.position.Value = targetPlanetPos;
            }
        }
    }
    public void SendBlueprintToServer(Blueprint blueprint)
    {
        if (blueprint == null) return;

        string jsonPayload = JsonWrapper.ToJson(blueprint, false);

        NetDataWriter bpWriter = new NetDataWriter();

        bpWriter.Put((byte)PacketType.BlueprintSpawn);
        bpWriter.Put(jsonPayload);

        if(!isServer && _serverPeer != null)
        {

            _serverPeer.Send(bpWriter, DeliveryMethod.ReliableOrdered);

        }
        else if (isServer)
        {

            _client.SendToAll(bpWriter, DeliveryMethod.ReliableOrdered);

        }
    }
    public void HandleBlueprintData(string jsonPayload)
    {
        try
        {

            Blueprint recievedBlueprint = JsonWrapper.FromJson<Blueprint>(jsonPayload);

            if(recievedBlueprint != null)
            {

                _blueprintQueue.Enqueue(recievedBlueprint);

            }
        }
        catch (System.Exception ex)
        {

            Debug.LogError("Failed to deserialize network blueprint payload data: " + ex.Message);
        
        }

    }
    public void UpdateEngineCache()
    {
        
    }
    public void SendRocketData()
    {
        /*
            Sending the rocket data to the server (x,y,rot)
            ,im gonna add the rest later.
        */

        /*
            My format for the packets :
            --X-------------
            --Y-------------
            --ROT-----------
            --THROTTLE------
            --WHEEL-COUNT---
            --ENGINE-STATES-
            --WHEEL-COUNT---
            --WHEEL-STATES--
        */

        var playerController = PlayerController.main;

        if(playerController != null && playerController.player.Value != null)
        {
            wasRocketActive = true;
            
            if (playerController.player.Value is Rocket currentRocket) 
            {
                currentRocket.rocketName = "Local";

                Double2 position = currentRocket.physics.location.Value.position;
                Double2 velocity = currentRocket.physics.location.Value.velocity;
            
                float rotationRadians = 0f;
                if (currentRocket.rb2d != null)
                {
                    rotationRadians = currentRocket.rb2d.rotation * Mathf.Deg2Rad;
                }
                else
                {
                    rotationRadians = currentRocket.transform.eulerAngles.z * Mathf.Deg2Rad;
                }
                float throttle = 0;
                if (currentRocket.throttle != null && currentRocket.throttle.throttleOn.Value)
                {
                    throttle = currentRocket.throttle.throttlePercent.Value;
                }

                var localEngines = currentRocket.GetComponentsInChildren<EngineModule>(true);
                byte engineCount = (byte) (localEngines != null ? localEngines.Length : 0);
                var localWheels = currentRocket.GetComponentsInChildren<WheelModule>(true);

                byte wheelCount = (byte) (localWheels != null ? localWheels.Length : 0);

                var localRcs = currentRocket.GetComponentsInChildren<RcsModule>(true);
                bool isRcsOn = currentRocket.arrowkeys != null && currentRocket.arrowkeys.rcs.Value;
                float turnAxis = currentRocket.arrowkeys != null ? currentRocket.arrowkeys.turnAxis.Value : 0f;
                Vector2 directAxis = Vector2.zero;

                if (localRcs != null && localRcs.Length > 0 && localRcs[0] != null)
                {
                    directAxis = localRcs[0].DirectionalAxis;
                }

                // let number = 30 as &mut i32  (pl3trix was here)
                // let x: *mut c_void = number as *mut c_void;

                writer.Reset();
                writer.Put((byte)PacketType.PositionUpdate);

                writer.Put(position.x);
                writer.Put(position.y);
                writer.Put(velocity.x);
                writer.Put(velocity.y);

                writer.Put((double)rotationRadians);

                writer.Put(throttle);
                writer.Put(engineCount);

                if (localEngines != null)
                {
                    foreach(var engine in localEngines)
                    {
                        writer.Put(engine != null && engine.engineOn.Value); 
                    }
                }

                writer.Put(wheelCount);

                if (localWheels != null)
                {
                    foreach(var wheel in localWheels)
                    {
                        writer.Put(wheel != null && wheel.on.Value);
                        writer.Put(wheel != null ? wheel.angularVelocity : 0f);
                    }
                }

                writer.Put(isRcsOn);
                writer.Put(turnAxis);

                writer.Put(directAxis.x);
                writer.Put(directAxis.y);

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
        else if (wasRocketActive)
        {
            wasRocketActive = false;
            SendDespawn();
        }
    }

    private float timer;

    public void Update()
    {

        if(_client == null) return;

        _client.PollEvents();

        while(_blueprintQueue.TryDequeue(out Blueprint bp))
        {

            if(_rocketHelper.CanSpawnRocket())
            {
                HandleRemoteDespawnRocketry();
                hasReceivedFirstPacket = false;
                isFirstPhysicsFrame = true;
                _rocketHelper.SpawnRemoteRocket(bp);
            }
            else
            {

                _blueprintQueue.Enqueue(bp);
                break;

            }

        }

        timer += Time.unscaledDeltaTime;

        if (timer >= 0.05f)
        {

            timer = 0f;

            if (isServer)
            {

                SendRocketData();
            }

            if (!isServer && _serverPeer != null && _serverPeer.ConnectionState == ConnectionState.Connected)
            {

                SendRocketData();
            }

        }

    }
    // Rocket Sync Physical
    // MOST IMPORTANT!!!!!!
    // this was the most brutal part in history
    public void FixedUpdate()
    {
        if (!hasReceivedFirstPacket || cachedRemoteRocket == null || cachedRemoteRocket.rb2d == null) return;

        var currentLocalRocket = PlayerController.main?.player?.Value as Rocket;

        if (cachedRemoteColliders == null || cachedRemoteRigidbodies == null || cachedLocalColliders == null || currentLocalRocket != cachedLocalRocketRef)
        {
            RedoColliderCache();
        }

        try {
            targetPlanetPos += targetPlanetVel * Time.fixedDeltaTime;
            Double2 currentGlobalPos = cachedRemoteRocket.physics.location.position.Value;

            double dx = targetPlanetPos.x - currentGlobalPos.x;
            double dy = targetPlanetPos.y - currentGlobalPos.y;
            double distanceSq = (dx * dx) + (dy * dy);

            Double2 smoothGlobalPos;
            if (distanceSq > 2500.0)
            {
                smoothGlobalPos = targetPlanetPos;
            }
            else
            {
                smoothGlobalPos = Double2.Lerp(currentGlobalPos, targetPlanetPos, Time.fixedDeltaTime * 14f);
            }
            float currentRot = cachedRemoteRocket.rb2d.rotation;
            float angleDelta = Mathf.DeltaAngle(currentRot, targetRotDegrees);

            float smoothRot;
            if (Mathf.Abs(angleDelta) > 90f)
            {
                smoothRot = targetRotDegrees;
            }
            else
            {
                smoothRot = Mathf.LerpAngle(currentRot, targetRotDegrees, Time.fixedDeltaTime * 25f);
            }

            cachedRemoteRocket.physics.location.position.Value = smoothGlobalPos;
            cachedRemoteRocket.physics.location.velocity.Value = targetPlanetVel;

            Vector2 screenCoM = WorldView.ToLocalPosition(smoothGlobalPos);

            Vector2 localCoM = Vector2.zero;
            float totalMass = 0f;
            Vector2 weightedSum = Vector2.zero;

            if (cachedRemoteRocket.partHolder != null && cachedRemoteRocket.partHolder.parts != null)
            {
                foreach (var part in cachedRemoteRocket.partHolder.parts)
                {
                    float partMass = part.mass != null ? part.mass.Value : 1f;
                    Vector2 partPos = (Vector2)part.transform.localPosition;
                    
                    if (part.centerOfMass != null)
                    {
                        partPos += part.centerOfMass.Value;
                    }

                    weightedSum += partPos * partMass;
                    totalMass += partMass;
                }

                if (totalMass > 0f)
                {
                    localCoM = weightedSum / totalMass;
                }
            }

            Vector2 rotatedOffset = Quaternion.Euler(0f, 0f, smoothRot) * localCoM;
            Vector2 targetScreenRoot = screenCoM - rotatedOffset;

            cachedRemoteRocket.rb2d.angularVelocity = 0f;
            cachedRemoteRocket.rb2d.rotation = smoothRot;

            Vector2 positionDelta = targetScreenRoot - cachedRemoteRocket.rb2d.position;

            float distanceSnap = positionDelta.sqrMagnitude;

            if (distanceSnap > 225f)
            {
                cachedRemoteRocket.rb2d.position = targetScreenRoot;
                cachedRemoteRocket.rb2d.linearVelocity = Vector2.zero;
            }
            else if (Time.fixedDeltaTime > 0f)
            {
                float distance = Mathf.Sqrt(distanceSnap);
                float adaptiveSpeedCap = Mathf.Lerp(40f, 300f, distance / 15f);

                cachedRemoteRocket.rb2d.linearVelocity = Vector2.ClampMagnitude(positionDelta / Time.fixedDeltaTime, adaptiveSpeedCap);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Physics update skipped: " + e.Message);
        }
    }
    // Rocket Child Sync Visual
    public void LateUpdate()
    {
        if (!hasReceivedFirstPacket) return;

        if (cachedRemoteRocket == null)
        {
            cachedRemoteRocket = FindObjectsByType<Rocket>(FindObjectsSortMode.None)
                .FirstOrDefault(r => r.rocketName == "RemotePeer");
            if (cachedRemoteRocket == null) return;
        }

        if (cachedRemoteEngines == null || cachedRemoteEngines.Length == 0)
        {
            cachedRemoteEngines = cachedRemoteRocket.GetComponentsInChildren<EngineModule>(true);

            foreach (var engine in cachedRemoteEngines)
            {
                if (engine != null)
                {
                    engine.enabled = false;
                }
            }
        }

        if (cachedRemoteRocket.throttle != null)
        {
            cachedRemoteRocket.throttle.throttlePercent.Value = targetThrottle;
        }

        try {

            int count = Mathf.Min(cachedRemoteEngines.Length, targetEngineStates.Length);

            for (int i = 0; i < count; i++)
            {
                var engine = cachedRemoteEngines[i];
                if (engine == null) continue;

                bool isOn = targetEngineStates[i];
                float effectiveThrottle = isOn ? targetThrottle : 0f;

                ((Rocket.INJ_Throttle)engine).Throttle = effectiveThrottle;

                engine.engineOn.Value = isOn;

                engine.throttle_Out.Value = effectiveThrottle;

                var flameModules = engine.GetComponentsInChildren<FlameModule>(true);
                foreach (var flame in flameModules)
                {
                    if (flame != null)
                    {
                        flame.throttle = effectiveThrottle;
                    }
                }
            }

            if (cachedRemoteWheels == null || cachedRemoteWheels.Length == 0)
            {
                cachedRemoteWheels = cachedRemoteRocket.GetComponentsInChildren<WheelModule>(true);
            }
            if (cachedRemoteRocket.arrowkeys != null)
            {
                cachedRemoteRocket.arrowkeys.rcs.Value = targetRcsState;
            }

            if (cachedRemoteRcs == null || cachedRemoteRcs.Length == 0)
            {
                cachedRemoteRcs = cachedRemoteRocket.GetComponentsInChildren<RcsModule>(true);
            }

            if (cachedRemoteRcs != null)
            {
                foreach (var rcs in cachedRemoteRcs)
                {
                    if (rcs == null) continue;
                    
                    rcs.TurnAxis = targetTurnAxis;
                    rcs.DirectionalAxis = targetDirectionalAxis;
                }
            }

            if (cachedRemoteWheels != null && targetWheelStates != null)
            {
                int wheelCount = Mathf.Min(cachedRemoteWheels.Length, targetWheelStates.Length);
                for (int i = 0; i < wheelCount; i++)
                {
                    var wheel = cachedRemoteWheels[i];
                    if (wheel == null) continue;

                    wheel.on.Value = targetWheelStates[i];
                    wheel.angularVelocity = targetWheelVelocities[i];
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Skipped visual update during initialization: " + e.Message);
        }
    }
}