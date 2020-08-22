using UnityEngine;
using UnityEngine.AI;

public class NavMeshPathfindingIterationsPerFrame : MonoBehaviour
{
    public int iterations = 100; // default

    void Awake()
    {
        print("SConfiguración de las iteraciones NavMesh Pathfinding por cuadro de" + NavMesh.pathfindingIterationsPerFrame + "a" + iterations);
        NavMesh.pathfindingIterationsPerFrame = iterations;
    }
}
