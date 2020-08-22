using UnityEngine;

namespace Controller2k
{
    public struct MoveVector
    {
        public Vector3 moveVector;

        public bool canSlide;
       
        public MoveVector(Vector3 newMoveVector, bool newCanSlide = true)
        {
            moveVector = newMoveVector;
            canSlide = newCanSlide;
        }
    }
}