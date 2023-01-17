using Expansions.Serenity;
using UnityEngine;
using static HarmonyLib.AccessTools;

namespace EasyRobotics
{
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

            Quaternion hingeRotationOffset = Quaternion.FromToRotation(currentHingeAxis, hingeAxis);
            transform.rotation = hingeRotationOffset * transform.rotation;

            // CCDIK step 3 : enforce joint Limits
            Vector3 fromDirection = transform.parent.rotation * perpendicularAxis;
            Vector3 toDirection = transform.rotation * perpendicularAxis;

            currentAngle = Vector3.SignedAngle(fromDirection, toDirection, transform.TransformDirection(axis).normalized);
            servoTargetAngle.SetValue(currentAngle, servo);

            return;

            unclampedAngle = Vector3.SignedAngle(fromDirection, toDirection, transform.TransformDirection(axis).normalized);
            //float currentAngle = servoTargetAngle.GetValue<float>(servo);
            Vector2 minMax = servoSoftMinMaxAngles.GetValue<Vector2>(servo);
            float servoMinAngle = minMax.x;
            float servoMaxAngle = minMax.y;
            Vector3 clampedDirection;
            clampingFraction = 0f;

            if (unclampedAngle < servoMinAngle)
            {
                clampingFraction = (unclampedAngle - servoMinAngle) / unclampedAngle;
                clampedDirection = Vector3.Slerp(toDirection.normalized, fromDirection.normalized, clampingFraction) * toDirection.magnitude;
            }
            else if (unclampedAngle > servoMaxAngle)
            {
                clampingFraction = (unclampedAngle - servoMaxAngle) / unclampedAngle;
                clampedDirection = Vector3.Slerp(toDirection.normalized, fromDirection.normalized, clampingFraction) * toDirection.magnitude;
            }
            else
                clampedDirection = toDirection;

            transform.rotation = Quaternion.FromToRotation(fromDirection, clampedDirection) * transform.rotation;

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