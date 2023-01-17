using Expansions.Serenity;
using UnityEngine;
using static HarmonyLib.AccessTools;

namespace EasyRobotics
{

    // source : https://github.com/zalo/MathUtilities/tree/master/Assets/IK/CCDIK
    public class IKJoint : MonoBehaviour
    {
        BaseField servoTargetAngle;
        BaseField servoSoftMinMaxAngles;
        public BaseServo servo;
        public IKJoint root;
        public IKJoint parent;
        public Vector3 axis; // local space
        Vector3 perpendicularAxis;
        //public float maxAngle;
        public float currentAngle;
        public float unclampedAngle;
        public float clampingFraction;
        public int order;

        public void Setup(BaseServo servo)
        {
            this.servo = servo;
            servoTargetAngle = servo.Fields["targetAngle"];
            servoSoftMinMaxAngles = servo.Fields["softMinMaxAngles"];
            currentAngle = servoTargetAngle.GetValue<float>(servo);
        }

        // axis must be in world space
        public void GetAxis()
        {
            axis = servo.GetMainAxis();
            //axis = transform.TransformDirection(axis).normalized;
            perpendicularAxis = Perpendicular(axis);

            // TODO : this align IKJoint to current servo angle, move it somewhere else ?
            currentAngle = servoTargetAngle.GetValue<float>(servo);
            Vector3 worldAxis = transform.TransformDirection(axis).normalized;
            Quaternion initialOffset = Quaternion.AngleAxis(currentAngle, worldAxis);
            transform.rotation = initialOffset * transform.rotation;


        }

        public void Evaluate(Transform effector, Transform target, bool rotateToDirection = false)
        {
            // CCDIK step 1 : rotate ignoring all constraints
            if (rotateToDirection)
            {
                // Point the effector along the target direction
                // In case of a 5+ DoF chain, do this with the last servos to match target orientation,
                // while other servos are matching target direction
                transform.rotation = Quaternion.FromToRotation(effector.up, target.forward);
            }
            else
            {
                // Point the effector towards the target
                Vector3 directionToEffector = effector.position - transform.position;
                Vector3 directionToTarget = target.position - transform.position;
                Quaternion rotationOffset = Quaternion.FromToRotation(directionToEffector, directionToTarget);
                transform.rotation = rotationOffset * transform.rotation;
            }

            // CCDIK step 2 : constrain to rotate around the axis
            Vector3 currentHingeAxis = transform.rotation * axis;
            Vector3 hingeAxis = transform.parent.rotation * axis;

            float sepAngle = Vector3.Angle(currentHingeAxis, hingeAxis);

            Quaternion hingeRotationOffset = Quaternion.FromToRotation(currentHingeAxis, hingeAxis);
            transform.rotation = hingeRotationOffset * transform.rotation;

            // CCDIK step 3 : enforce joint Limits
            Vector3 fromDirection = transform.parent.rotation * perpendicularAxis;
            Vector3 toDirection = transform.rotation * perpendicularAxis;

            unclampedAngle = Vector3.SignedAngle(fromDirection, toDirection, transform.TransformDirection(axis).normalized);
            //float currentAngle = servoTargetAngle.GetValue<float>(servo);
            Vector2 minMax = servoSoftMinMaxAngles.GetValue<Vector2>(servo);
            float servoMinAngle = minMax.x + 0.5f;
            float servoMaxAngle = minMax.y - 0.5f;
            Vector3 clampedDirection = toDirection;
            clampingFraction = 0f;

            //if (unclampedAngle < servoMinAngle)
            //{
            //    clampingFraction = (unclampedAngle - servoMinAngle) / unclampedAngle;
            //    clampedDirection = Vector3.Slerp(toDirection.normalized, fromDirection.normalized, clampingFraction) * toDirection.magnitude;
            //    transform.rotation = Quaternion.FromToRotation(toDirection, clampedDirection) * transform.rotation;
            //}
            //else if (unclampedAngle > servoMaxAngle)
            //{
            //    clampingFraction = (unclampedAngle - servoMaxAngle) / unclampedAngle;
            //    clampedDirection = Vector3.Slerp(toDirection.normalized, fromDirection.normalized, clampingFraction) * toDirection.magnitude;
            //    transform.rotation = Quaternion.FromToRotation(toDirection, clampedDirection) * transform.rotation;
            //}
            //else
            //{
            //    clampedDirection = toDirection;
            //}


            // Set servo angle
            currentAngle = Vector3.SignedAngle(fromDirection, clampedDirection, transform.TransformDirection(axis).normalized);
            servoTargetAngle.SetValue(currentAngle, servo);
        }

        private Vector3 Perpendicular(Vector3 vec)
        {
            if (Mathf.Abs(vec.x) > Mathf.Abs(vec.z))
                return new Vector3(-vec.y, vec.x, 0f);
            else
                return new Vector3(0f, -vec.z, vec.y);
        }
    }
}