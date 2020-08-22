// Toda la lógica del jugador se puso en esta clase.
// La clase de jugador predeterminada se encarga de la lógica básica 
// del jugador como la máquina de estado y otras propiedades.
// La clase también se encarga del manejo de la selección, que detecta 
// los clics del mundo en 3D y luego apunta/navega a algún lugar/interactúa 
// con alguien.
// Las animaciones no son manejadas por el NetworkAnimator porque todavía es 
// muy defectuoso y porque realmente no puede reaccionar al movimiento se detiene
// lo suficientemente rápido, lo que resulta en un moonwalking. No sincronizar
// animaciones a través de la red también nos ahorrará ancho de banda
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Mirror;
using TMPro;

public class SyncDictionaryIntDouble : SyncDictionary<int, double> { }

[Serializable] public class UnityEventPlayer : UnityEvent<Player> { }

//[RequireComponent(typeof(PlayerChat))]
[RequireComponent(typeof(PlayerAdministratorTool))]
//[RequireComponent(typeof(PlayerCompany))]
[RequireComponent(typeof(PlayerIndicator))] 
[RequireComponent(typeof(PlayerMeeting))]
[RequireComponent(typeof(NetworkName))]
public partial class Player : Entity
{
    // campos para todos los componentes del reproductor para evitar costosas llamadas GetComponent
    [Header("Components")]
    // public PlayerChat chat;
    public PlayerIndicator indicator;
    public PlayerAdministratorTool administratorTool;
   // public PlayerCompany company;
    public PlayerMeeting meeting;
    public Camera avatarCamera;

    [Header("Text Meshes")]
    public TextMeshPro nameOverlay;
    public Color nameOverlayDefaultColor = Color.white;
    public Color nameOverlaySpectatorColor = Color.magenta;
    public Color nameOverlayOwnerColor = Color.red;
    public Color nameOverlayMeetingColor = new Color(0.341f, 0.965f, 0.702f);
    public string nameOverlayAdministratorPrefix = "[GM] ";

    [Header("Icons")]
    public Sprite classIcon; // para la selección de personajes
    public Sprite portraitIcon; // para retrato arriba a la izquierda

    // alguna metainformación
    [HideInInspector] public string account = "";
    [HideInInspector] public string className = "";

    // mantén la bandera ADMIN aquí y los controles en PlayerAdministrator.cs:
    // -> necesitamos la bandera para el prefijo NameOverlay de todos modos
    // -> podría ser necesario fuera de PlayerAdministrator para otras 
    // mecánicas/controles específicos de ADMIN más adelante
    // -> de esta manera podemos usar SyncToObservers para el indicador, y
    // SyncToOwner para todo lo demás en el componente PlayerAdministrator. 
    // Esto es MUCHO más fácil.
    [SyncVar] public bool isAdministrator;

    // singlePlayer localton para un acceso más fácil desde scripts de interfaz de usuario, etc.     
    public static Player localPlayer;

    [Header("Interaction")]
    public float interactionRange = 4;
    public bool localPlayerClickThrough = true; // La selección de clic pasa por el jugador local. se siente mejor
    public KeyCode cancelActionKey = KeyCode.Escape;

    // el próximo objetivo que se establecerá si intentamos establecerlo mientras se convierte 
    // 'Entity' no puede ser SyncVar y NetworkIdentity causa errores cuando es nulo, por lo que 
    // usamos [SyncVar] GameObject y lo envolvemos por simplicidad
    [SyncVar] GameObject _nextTarget;
    public Entity nextTarget
    {
        get { return _nextTarget != null ? _nextTarget.GetComponent<Entity>() : null; }
        set { _nextTarget = value != null ? value.gameObject : null; }
    }
    // jugadores de caché para guardar muchos cálculos
    // (de lo contrario, tendríamos que iterar NetworkServer.objects todo el tiempo)
    // => en el servidor: todos los jugadores en línea
    // => en el cliente: todos los jugadores observados
    public static Dictionary<string, Player> onlinePlayers = new Dictionary<string, Player>();
    
    // algunos comandos deben tener demoras para evitar DDOS, uso excesivo de la base de datos o cupones 
    // de fuerza bruta, etc. Utilizamos un temporizador de acción riesgoso para todos.
    [SyncVar, HideInInspector] public double nextRiskyActionTime = 0; // doble para precisión a largo plazo


    // first allowed logout time after combat
    public double allowedLogoutTime => lastCombatTime + ((NetworkManagerFV)NetworkManager.singleton).combatLogoutDelay;
    public double remainingLogoutTime => NetworkTime.time < allowedLogoutTime ? (allowedLogoutTime - NetworkTime.time) : 0;


    //// variable auxiliar para recordar qué habilidad usar cuando caminamos lo suficientemente cerca
    [HideInInspector] public int useSkillWhenCloser = -1;


    // networkbehaviour ////////////////////////////////////////////////////////
    public override void OnStartLocalPlayer()
    {
        // establecer singleton
        localPlayer = this;

        // configurar objetivos de la cámara
        GameObject.FindWithTag("MinimapCamera").GetComponent<CopyPosition>().target = transform;
        if (avatarCamera) avatarCamera.enabled = true; // Cámara avatar para jugador local
    }

    protected override void Start()
    {
        // no hacer nada si no se genera (= para las vistas previas de selección de personajes)
        if (!isServer && !isClient) return;

        base.Start();
        onlinePlayers[name] = this;
    }

    void LateUpdate()
    {
        // pasar parámetros a la máquina de estado de animación
        // => pasar los estados directamente es la forma más confiable 
        // de evitar todo tipo de fallas como movimientos deslizantes, 
        // movimientos de ataque, etc.
        // => asegúrese de importar todas las animaciones en bucle como 
        // inactivo / ejecutar / atacar con 'tiempo de bucle' habilitado; 
        // de lo contrario, el cliente solo puede reproducirlo una vez
        // => El estado MOVING se establece directamente en el resultado 
        // local de IsMovement. de lo contrario, veríamos latencias de 
        // animación para el movimiento de la banda elástica si tenemos que 
        // esperar a que el servidor reciba el estado MOVING
        // => MOVING comprueba si! CASTING porque hay un caso en UpdateMOVING
        // -> SkillRequest donde todavía nos deslizamos a la posición final 
        // (lo cual es bueno), pero entonces deberíamos mostrar la animación 
        // de lanzamiento.
        // => se supone que los nombres de habilidades son parámetros booleanos 
        // en animator, por lo que no debemos preocuparnos por un número de 
        //animación, etc.
        if (isClient) //  no necesita animaciones en el servidor
        {
            // ahora pasa parámetros después de cualquier posible nuevo enlace
            foreach (Animator anim in GetComponentsInChildren<Animator>())
            {
                anim.SetBool("MOVING", movement.IsMoving());
            }
        }
        // (también actualizar para vistas previas de selección de personajes, etc.)
        if (!isServerOnly)
        {
            if (nameOverlay != null)
            {
                // solo los jugadores necesitan copiar nombres para superponer nombres. 
                // nunca cambia npcs.
                string prefix = isAdministrator ? nameOverlayAdministratorPrefix : "";
                nameOverlay.text = prefix + name;

                // buscar jugador local (nulo mientras está en la selección de personaje)
                if (localPlayer != null)
                {
                    // nota: el propietario tiene mayor prioridad (un jugador puede ser propietario 
                    // y espectador al mismo tiempo)
                    if (IsOwner())
                        nameOverlay.color = nameOverlayOwnerColor;
                    else if (IsSpectator())
                        nameOverlay.color = nameOverlaySpectatorColor;
                    // member of the same party
                    else if (localPlayer.meeting.InMeeting() &&
                             localPlayer.meeting.meeting.Contains(name))
                        nameOverlay.color = nameOverlayMeetingColor;
                    // otherwise default
                    else
                        nameOverlay.color = nameOverlayDefaultColor;
                }
            }
        }
    }
    void OnDestroy()
    {
        // intenta eliminar primero de los jugadores en línea, NO IMPORTA QUE
        // -> no podemos arriesgarnos a no eliminarlo nunca. haga esto antes 
        //    de cualquier devolución anticipada, etc.
        // -> SOLO eliminar si ESTE objeto fue guardado. esto evita un error en 
        //    el que un anfitrión selecciona una vista previa del personaje,
        //    luego se une al juego, luego solo después del final del cuadro se 
        //    destruye la vista previa, se llama a OnDestroy y la vista previa 
        //    realmente eliminaría al jugador mundial de los Jugadores en línea. 
        //    por lo tanto, hace imposible la gestión de gremios, etc.
        if (onlinePlayers.TryGetValue(name, out Player entry) && entry == this)
            onlinePlayers.Remove(name);

        // no hacer nada si no se genera (= para las vistas previas de selección de personajes)
        if (!isServer && !isClient) return;

        if (isLocalPlayer) //requiere al menos la corrección de errores de Unity 5.5.1 para funcionar
        {
            localPlayer = null;
        }
    }

    // algunos eventos cerebrales requieren Cmds que no pueden estar en ScriptableObject ////////
    [Command]
    public void CmdRespawn() { respawnRequested = true; }
    internal bool respawnRequested;

    [Command]
    public void CmdCancelAction() { cancelActionRequested = true; }
    internal bool cancelActionRequested;
    // movimiento /////////////////////////////////////////////// /////////////////
    // verifica si el movimiento está permitido actualmente
    // -> no en Movement.cs porque tendríamos que agregarlo a cada sistema
    // de movimiento del jugador. (no se puede usar un PlayerMovement.cs 
    // abstracto porque PlayerNavMeshMovement necesita heredar de NavMeshMovement ya)
    public bool IsMovementAllowed()
    {
        return (state == "IDLE" || state == "MOVING") &&
               !UIUtils.AnyInputActive();
    }

    // condicionales de modificacion de stands o toma de folletos si es propietario o 
    // espectador
    public bool IsSpectator()
    {
        Debug.Log("Es un espectador");
        return false;
    }

    public bool IsOwner()
    {
        Debug.Log("Es un propietario");
        return true;

    }

    // manejo de selección //////////////////////////////////////////////// ////////
    [Command]
    public void CmdSetTarget(NetworkIdentity ni)
    {
        // validacion
        if (ni != null)
        {
            // // ¿puede cambiarlo directamente o cambiarlo después de lanzarlo?
            if (state == "IDLE" || state == "MOVING" )
                target = ni.GetComponent<Entity>();
        }
    }

    // interaccion /////////////////////////////////////////////////////////////
    protected override void OnInteract()
    {
        // no es jugador local?
        if (this != localPlayer)
        {
            // usa puntos de colisionador para trabajar también con grandes entidades
            Vector3 destination = Utils.ClosestPoint(this, localPlayer.transform.position);
                localPlayer.movement.Navigate(destination, localPlayer.interactionRange);
          
        }
    }
}