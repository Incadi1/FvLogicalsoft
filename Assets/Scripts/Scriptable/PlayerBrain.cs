// npc brain does nothing but stand around
using UnityEngine;
using Mirror;

[CreateAssetMenu(menuName = "FV Brain/Brains/Player", order = 999)]
public class PlayerBrain : CommonBrain
{
    [Tooltip("Estar aturdido interrumpe el lanzamiento. Habilite esta opción para continuar el reparto después.")]
    public bool continueCastAfterStunned = true;

    // events //////////////////////////////////////////////////////////////////
    public bool EventCancelAction(Player player)
    {
        bool result = player.cancelActionRequested;
        player.cancelActionRequested = false; // reset
        return result;
    }

    public bool EventRespawn(Player player)
    {
        bool result = player.respawnRequested;
        player.respawnRequested = false; // reset
        return result;
    }

   
    // states //////////////////////////////////////////////////////////////////
    string UpdateServer_IDLE(Player player)
    {
        // events sorted by priority (e.g. target doesn't matter if we died)
       
        if (EventCancelAction(player))
        {
            // the only thing that we can cancel is the target
            player.target = null;
            return "IDLE";
        }
        if (EventMoveStart(player))
        {
            return "MOVING";
        }
      
        if (EventMoveEnd(player)) { } // don't care
        if (EventRespawn(player)) { } // don't care
        if (EventTargetDisappeared(player)) { } // don't care
        return "IDLE"; // nothing interesting happened
    }

    string UpdateServer_MOVING(Player player)
    {
        // events sorted by priority (e.g. target doesn't matter if we died)
       
        if (EventMoveEnd(player))
        {
            // finished moving. do whatever we did before.
            return "IDLE";
        }
        if (EventCancelAction(player))
        {
            //player.movement.Reset(); <- done locally. doing it here would reset localplayer to the slightly behind server position otherwise
            return "IDLE";
        }
       
        if (EventMoveStart(player)) { } // don't care
        if (EventRespawn(player)) { } // don't care
        if (EventTargetDisappeared(player)) { } // don't care
        return "MOVING"; // nothing interesting happened
    }

    void UseNextTargetIfAny(Player player)
    {
        // use next target if the user tried to target another while casting
        // (target is locked while casting so skill isn't applied to an invalid
        //  target accidentally)
        if (player.nextTarget != null)
        {
            player.target = player.nextTarget;
            player.nextTarget = null;
        }
    }

    public override string UpdateServer(Entity entity)
    {
        Player player = (Player)entity;

        if (player.state == "IDLE") return UpdateServer_IDLE(player);
        if (player.state == "MOVING") return UpdateServer_MOVING(player);
     

        Debug.LogError("invalid state:" + player.state);
        return "IDLE";
    }
    public override void UpdateClient(Entity entity)
    {
        Player player = (Player)entity;

        if (player.state == "IDLE" || player.state == "MOVING")
        {
            if (player.isLocalPlayer)
            {
                // cancel action if escape key was pressed
                if (Input.GetKeyDown(player.cancelActionKey))
                {
                    player.movement.Reset(); // reset locally because we use rubberband movement
                    player.CmdCancelAction();
                }

            }
        }
        else Debug.LogError("invalid state:" + player.state);
    }
}
