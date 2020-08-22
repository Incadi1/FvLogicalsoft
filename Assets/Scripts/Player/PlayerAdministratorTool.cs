
// estadísticas principales del juego del jugador / acciones / controles.
using UnityEditor;
using UnityEngine;
using Mirror;


public class PlayerAdministratorTool : NetworkBehaviourNonAlloc
{
    [Header("Components")]
    public Player player;

    // // nota: el indicador isAdministrator está en Player.cs

    // datos del servidor a través de SyncVar y SyncToOwner es la solución más fácil
    [HideInInspector, SyncVar] public int connections;
    [HideInInspector, SyncVar] public int maxConnections;
    [HideInInspector, SyncVar] public int onlinePlayers;
    [HideInInspector, SyncVar] public float uptime;
    [HideInInspector, SyncVar] public int tickRate;

    // ayudantes de velocidad de tick
    int tickRateCounter;
    double tickRateStart;

    // Datos del Servidor /////////////////////////////////////////////////////////////
    public override void OnStartServer()
    {
        // Validacion: solo para ADMINs
        if (!player.isAdministrator) return;

        // envía datos al cliente cada pocos segundos. use syncInterval para ello. // send data to client every few seconds. use syncInterval for it.
        InvokeRepeating(nameof(RefreshData), syncInterval, syncInterval);
    }

    [ServerCallback]
    void Update()
    {
        // Validacion: solo para ADMINs
        if (!player.isAdministrator) return;

        // mide la tasa de tics para tener una idea de la carga del servidor
        ++tickRateCounter;
        if (NetworkTime.time >= tickRateStart + 1)
        {
            // guardar tasa de ticks. se sincronizará con el cliente automáticamente.
            tickRate = tickRateCounter;

            // start counting again
            tickRateCounter = 0;
            tickRateStart = NetworkTime.time;
        }
    }

    [Server]
    void RefreshData()
    {
        // Validacion: solo para ADMINs
        if (!player.isAdministrator) return;

        // actualiza los vars de sincronización. se sincronizará con el cliente automáticamente.
        connections = NetworkServer.connections.Count;
        maxConnections = NetworkManager.singleton.maxConnections;
        onlinePlayers = Player.onlinePlayers.Count;
        uptime = Time.realtimeSinceStartup;
    }
    /* [Command]
     public void CmdSendGlobalMessage(string message)
     {
         // Validacion: solo para ADMINs
         if (!player.isAdministrator) return;

         player.chat.SendGlobalMessage(message);
     }
    */
    [Command]
    public void CmdShutdown()
    {
        // Validacion: solo para ADMINs
        if (!player.isAdministrator) return;

        NetworkManagerFV.Quit();
    }

    [Command]
    public void CmdSummon(string otherPlayer)
    {
        // Validacion: solo para ADMINs
        if (!player.isAdministrator) return;

        // summon other to self and add chat message so the player knows why
        // it happened
        if (Player.onlinePlayers.TryGetValue(otherPlayer, out Player other))
        {
            other.movement.Warp(player.transform.position);
          //  other.chat.TargetMsgInfo("Un Admin le ha convocado.");
        }
    }

    [Command]
    public void CmdRemove(string otherPlayer)
    {
        // Validacion: solo para ADMINs
        if (!player.isAdministrator) return;

        // kill other and add chat message so the player knows why it happened
        if (Player.onlinePlayers.TryGetValue(otherPlayer, out Player other))
        {
            // other.health.current = 0;
            // other.chat.TargetMsgInfo("A GM killed you.");
            other.connectionToClient.Disconnect();

        }
    }

    // validacion //////////////////////////////////////////////////////////////
    void OnValidate()
    {
        // ¡los datos de la herramienta gm solo deberían sincronizarse con el propietario!
        // ¡los observadores no deberían saberlo!
        if (syncMode != SyncMode.Owner)
        {
            syncMode = SyncMode.Owner;
#if UNITY_EDITOR
            Undo.RecordObject(this, name + " " + GetType() + " Cambio de modo de sincronización de componentes a Propietario.");
#endif
        }
    }
}