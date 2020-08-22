// queremos admitir diferentes tipos de movimiento:
// * Navmesh
// * Controlador de personaje
//   (* Cuerpo rígido)
// etc.
//
// => Entity.cs necesita alguna funcionalidad común para trabajar con todos ellos.
// => ¡esto hace que intercambiar sistemas de movimiento sea muy fácil!
using UnityEngine;

public abstract class Movement : NetworkBehaviourNonAlloc
{
    // la velocidad es útil para animaciones, etc.
    // => no es una propiedad porque la mayoría de los sistemas de movimiento manejan sus propios
    // 'velocidad' variable internamente, y establecerlos también (queremos solo lectura)
    public abstract Vector3 GetVelocity();

    // actualmente en movimiento? importante para ciertas acciones que no se pueden lanzar
    // mientras te mueves, etc.
    public abstract bool IsMoving();
    
    // .speed vive en la Entidad y depende del vestimenta, etc.
    // => aquí simplemente lo aplicamos (por ejemplo, a NavMeshAgent.speed)
    public abstract void SetSpeed(float speed);

    // mira una transformación mientras solo gira en el eje Y (para evitar inclinaciones extrañas)
    // => abstracto porque no todos los sistemas de movimiento pueden usar el mismo método.
    public abstract void LookAtY(Vector3 position);

    // restablecer todo el movimiento. solo detente y párate.
    public abstract void Reset();


    // deformar a un área diferente
    // => establecer transform.position no es lo suficientemente bueno. por ejemplo,
    // El movimiento NavMeshAgent siempre necesita llamar a agent.Warp. de lo contrario,
    // el agente podría quedarse atascado en una pared entre la posición y el destino, etc.
    public abstract void Warp(Vector3 destination);

    // ¿este sistema de movimiento admite navegación / búsqueda de ruta?
    // -> algunos sistemas pueden no admitirlo nunca
    // -> algunos pueden soportarlo mientras están conectados a tierra, etc.
    public abstract bool CanNavigate();

    // navegar a lo largo de una ruta a un destino
    public abstract void Navigate(Vector3 destination, float stoppingDistance);

    // al generar necesitamos saber si la última posición guardada sigue siendo 
    // válida para este tipo de movimiento.
    // * El movimiento NavMesh solo debería aparecer en NavMesh
    // * El movimiento del CharacterController debería aparecer en una malla, etc.
    public abstract bool IsValidSpawnPoint(Vector3 position);

    // a veces necesitamos saber el destino válido más cercano para un punto que 
    // podría estar detrás de una pared, etc.
    public abstract Vector3 NearestValidDestination(Vector3 destination);

    // ¿deberíamos mirar automáticamente al objetivo durante el combate?
    // p.ej. si se mueve detrás de nosotros
    // -> usualmente bueno para sistemas de movimiento de navegación
    // -> generalmente no es bueno para los sistemas de movimiento del controlador de personaje
    public abstract bool DoCombatLookAt();

}