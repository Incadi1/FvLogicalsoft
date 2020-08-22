// La clase Entity es bastante simple. Contiene algunas propiedades básicas de la entidad
//
// Las entidades también tienen una Entidad _target_ que no se puede sincronizar con un
// SyncVar. En su lugar, creamos un componente EntityTargetSync que se encarga de
// eso para nosotros.
//
// Las entidades usan una máquina determinista de estados finitos para manejar IDLE / MOVING 
// etc. estados y eventos. Usar un FSM determinista significa que reaccionamos
// a cada evento que puede suceder en todos los estados (en lugar de solo
// cuidar de los que nos importan en este momento). Esto significa un poco más
// código, pero también significa que evitamos todo tipo de situaciones extrañas como 'the
// el monstruo no reacciona a un objetivo muerto cuando lanza 'etc.
// El siguiente estado siempre se establece con el valor de retorno de UpdateServer
// función. Nunca se puede configurar fuera de él, para asegurarse de que todos los eventos sean
// realmente se maneja en la máquina de estado y no fuera de ella. De lo contrario, podemos ser
// tentado a establecer un estado en CmdBeingTrading, etc., pero es probable que se olvide de
// cosas especiales que hacer según el estado actual.
//
// Las entidades también necesitan un cuerpo rígido cinemático para que las funciones de OnTrigger puedan ser
// llamado. Tenga en cuenta que actualmente hay un error de Unity que ralentiza al agente
// cuando se tiene mucho FPS (más de 300) si la opción Interpolar del cuerpo rígido es
// habilitado. Por lo tanto, por ahora es importante deshabilitar la interpolación, que es una buena opción.
// idea en general para aumentar el rendimiento.
//
// Nota: en una arquitectura basada en componentes no necesariamente necesitamos Entity.cs,
// pero nos ayuda a evitar muchas llamadas GetComponent. 

using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Mirror;
using TMPro;

[Serializable] public class UnityEventEntity : UnityEvent<Entity> { }
[Serializable] public class UnityEventEntityInt : UnityEvent<Entity, int> { }

[RequireComponent(typeof(Movement))]
[RequireComponent(typeof(NetworkProximityGridChecker))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))] // kinematic, only needed for OnTrigger
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public abstract partial class Entity : NetworkBehaviourNonAlloc
{ 
    [Header("Components")]
    public Movement movement;
    public NetworkProximityGridChecker proxchecker;
    public Animator animator;
#pragma warning disable CS0109 // miembro no oculta miembro accesible
    new public Collider collider;
#pragma warning restore CS0109 // miembro no oculta miembro accesible
    public AudioSource audioSource;

    // máquina de estados finitos
    // -> estado solo escribible por clase de entidad para evitar todo tipo de confusión
    [Header("Brain")]
    public ScriptableBrain brain;
    [SyncVar, SerializeField] string _state = "IDLE";
    public string state => _state;

    // it's useful to know an entity's last combat time (did/was attacked)
    // e.g. to prevent logging out for x seconds after combat
    [SyncVar] public double lastCombatTime;


    // 'Entity' no puede ser SyncVar y NetworkIdentity causa errores cuando es nulo,
    // entonces usamos [SyncVar] GameObject y lo envolvemos por simplicidad
    [Header("Target")]
    [SyncVar] GameObject _target;
    public Entity target
    {
        get { return _target != null ? _target.GetComponent<Entity>() : null; }
        set { _target = value != null ? value.gameObject : null; }
    }

    [Header("Text Meshes")]
    public TextMeshPro stunnedOverlay;

    [Header("Events")]
    public UnityEventEntity onAggro;
    public UnityEvent onSelect; // llamado al hacer clic la primera vez
    public UnityEvent onInteract; // lamado al hacer clic la segunda vez

    protected virtual void Start()
    {
        // deshabilita el animador en el servidor. Este es un gran aumento de rendimiento
        // y definitivamente vale la pena una línea de código (1000 personajes: 22 fps => 32 fps)
        // (! isClient porque tampoco queremos hacerlo en modo host)
        // (OnStartServer aún no conoce isClient, Start es la única opción)
        if (!isClient) animator.enabled = false;
    }

    // la lógica de entidad se implementará con una máquina de estados finitos
    // -> deberíamos reaccionar a cada estado y a cada evento para que sea correcto
    // -> lo mantenemos funcional por simplicidad
    // nota: aún puede usar LateUpdate para actualizaciones que deberían ocurrir en cualquier caso

    // OnDrawGizmos only happens while the Script is not collapsed
    public virtual void OnDrawGizmos()
    {
        // forward to Brain. this is useful to display debug information.
        if (brain != null) brain.DrawGizmos(this);
    }


    // visibilidad /////////////////////////////////////////////// ///////////////
    // ocultar una entidad
    // nota: el uso de SetActive no funcionará porque no está sincronizado y provocaría 
    // que los objetos inactivos ya no reciban ninguna información
    // nota: esto no será visible en el servidor ya que siempre ve todo.
    [Server]
    public void Hide()
    {
        proxchecker.forceHidden = true;
    }

    [Server]
    public void Show()
    {
        proxchecker.forceHidden = false;
    }

    // ¿la entidad está actualmente oculta?
    // nota: generalmente el servidor es el único que usa forceHidden, el
    // el cliente generalmente no lo sabe y simplemente no ve el GameObject.
    public bool IsHidden() => proxchecker.forceHidden;

    public float VisRange() => NetworkProximityGridChecker.visRange;

    // selección e interacción ///////////////////////////////////////////// //// 
    // usa la función Unity OnMouseDown. No hay necesidad de rayos.
    void OnMouseDown()
    {
        // se unió al mundo todavía? (¿no selección de personaje?)
        // no sobre la IU? (evite apuntar a través de ventanas)
        // y en un estado donde el jugador local puede seleccionar cosas?
        if (Player.localPlayer != null &&
            !Utils.IsCursorOverUserInterface() &&
            (Player.localPlayer.state == "IDLE" ||
             Player.localPlayer.state == "MOVING"))
        {
            // borrar la habilidad solicitada en cualquier caso porque si hacemos 
            // clic en otro lugar, ya no nos importa
            Player.localPlayer.useSkillWhenCloser = -1;

            // establecer el indicador en cualquier caso (no solo la primera vez, 
            // porque podríamos haber hecho clic en el suelo mientras tanto. siempre 
            // configúrelo al seleccionar).
            Player.localPlayer.indicator.SetViaParent(transform);

            // hizo clic por primera vez: SELECCIONAR
            if (Player.localPlayer.target != this)
            {
                // apuntarlo
                Player.localPlayer.CmdSetTarget(netIdentity);

                // llama a OnSelect + hook
                OnSelect();
                onSelect.Invoke();
            }
            // hizo clic por segunda vez: INTERACT
            else
            {
                // llama a OnInteract + hook
                OnInteract();
                onInteract.Invoke();
            }
        }
    }

    protected virtual void OnSelect() { }
    protected abstract void OnInteract();

}
