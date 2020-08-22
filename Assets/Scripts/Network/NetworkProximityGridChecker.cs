// comprobador de proximidad basado en cuadrícula. 30 veces más rápido que el corrector basado en esfera.
//
// usa 8 cuadrículas de vecindario para que todas las entidades no se carguen abruptamente. solamente
// los que están lejos están.
//
// punto de referencia con 1 jugador + 1000 monstruos = 1001 controles de proximidad
// SphereCast: 952ms, 8,3MB GC
// Cuadrícula: 31 ms, 4.7MB GC
//
// en otras palabras: ¡resultados muy notables con más de 1000 entidades!

using System.Collections.Generic;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(NetworkIdentity))]
public class NetworkProximityGridChecker : NetworkVisibility
{
    // variables estáticas comunes en todos los verificadores de cuadrícula ////////////////////////
    // rango de vista
    // -> tiene que ser estático porque necesitamos lo mismo para todos
    // -> no se puede mostrar en el Inspector porque Unity no serializa las estadísticas
    public static int visRange = 100;

    // si vemos 8 vecinos, entonces 1 entrada es visRange / 3
    public static int resolution => visRange / 3;

    // la cuadrícula
    static Grid2D<NetworkConnection> grid = new Grid2D<NetworkConnection>();

    //////////////////////////////////////////////////////////////////////////////// ////////////////////////////

    [TooltipAttribute("Con qué frecuencia (en segundos) este objeto debe actualizar el conjunto de jugadores que pueden verlo")]
    public float visUpdateInterval = 1; // en segundos

    [TooltipAttribute("Habilitar para obligar a este objeto a ocultarse de los jugadores")]
    public bool forceHidden;

    /// <summary>
    /// Enumeration of methods to use to check proximity.
    /// </summary>
    public enum CheckMethod
    {
        XZ_FOR_3D,
        XY_FOR_2D
    }

    [TooltipAttribute("Qué método usar para verificar la proximidad de los jugadores.")]
    public CheckMethod checkMethod = CheckMethod.XZ_FOR_3D;

    // previous position in the grid
    Vector2Int previous = new Vector2Int(int.MaxValue, int.MaxValue);

    // from original checker
    float m_VisUpdateTime;

    // called when a new player enters
    public override bool OnCheckObserver(NetworkConnection newObserver)
    {
        if (forceHidden)
            return false;

        // calculate projected positions
        Vector2Int projected = ProjectToGrid(transform.position);
        Vector2Int observerProjected = ProjectToGrid(newObserver.identity.transform.position);

        // distance needs to be at max one of the 8 neighbors, which is
        //   1 for the direct neighbors
        //   1.41 for the diagonal neighbors (= sqrt(2))
        // => use sqrMagnitude and '2' to avoid computations. same result.
        return (projected - observerProjected).sqrMagnitude <= 2;
    }

    Vector2Int ProjectToGrid(Vector3 position)
    {
        // simple rounding for now
        // 3D uses xz (horizontal plane)
        // 2D uses xy
        if (checkMethod == CheckMethod.XZ_FOR_3D)
        {
            return Vector2Int.RoundToInt(new Vector2(position.x, position.z) / resolution);
        }
        else
        {
            return Vector2Int.RoundToInt(new Vector2(position.x, position.y) / resolution);
        }
    }

    // note: this hides base.update, which is fine
    void Update()
    {
        if (!NetworkServer.active) return;

        // has connection to client? then we are a possible observer (player)
        // (monsters don't observer each other)
        if (connectionToClient != null)
        {
            // calculate current grid position
            Vector2Int current = ProjectToGrid(transform.position);

            // changed since last time?
            if (current != previous)
            {
                // update position in grid
                grid.Remove(previous, connectionToClient);
                grid.Add(current, connectionToClient);

                // save as previous
                previous = current;
            }
        }

        // possibly rebuild AFTER updating position in grid, so it's always up
        // to date. otherwise player might have moved and not be in current grid
        // hence OnRebuild wouldn't even find itself there
        if (Time.time - m_VisUpdateTime > visUpdateInterval)
        {
            netIdentity.RebuildObservers(false);
            m_VisUpdateTime = Time.time;
        }
    }

    void OnDestroy()
    {
        // try to remove from grid no matter what.
        // -> no NetworkServer.active check in case OnDestroy gets called after
        //    shutting down the server
        // -> ONLY if we are the connection's main player object. not if we are
        //    a pet that is owned by the player. otherwise we would remove the
        //    pet's connection (which is the player connection), so the player
        //    gets removed from observers until next rebuild, and all monsters/
        //    npcs respawn.
        //    see also: https://github.com/vis2k/uMMORPG/issues/33
        if (connectionToClient != null &&
            connectionToClient.identity == netIdentity)
            grid.Remove(ProjectToGrid(transform.position), connectionToClient);
    }

    public override void OnRebuildObservers(HashSet<NetworkConnection> observers, bool initial)
    {
        // if force hidden then return without adding any observers.
        if (forceHidden)
            return;

        // add everyone in 9 neighbour grid
        // -> pass observers to GetWithNeighbours directly to avoid allocations
        //    and expensive .UnionWith computations.
        Vector2Int current = ProjectToGrid(transform.position);
        grid.GetWithNeighbours(current, observers);
    }

    // called hiding and showing objects on the host
    public override void OnSetHostVisibility(bool visible)
    {
        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            rend.enabled = visible;
        }
    }
}
