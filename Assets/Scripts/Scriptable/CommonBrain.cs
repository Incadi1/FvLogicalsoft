using UnityEngine;
using Mirror;

public abstract class CommonBrain : ScriptableBrain
{
      // only fire when stopped moving
    public bool EventMoveEnd(Entity entity) =>
        entity.state == "MOVING" && !entity.movement.IsMoving();

    // only fire when started moving
    public bool EventMoveStart(Entity entity) =>
        entity.state != "MOVING" && entity.movement.IsMoving();

    public bool EventTargetDisappeared(Entity entity) =>
        entity.target == null;

}
