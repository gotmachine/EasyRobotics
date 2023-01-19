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
        public Vector3 perpendicularAxis;
        //public float maxAngle;
        public float servoAngle;
        public float unclampedAngle;
        public float clampingFraction;
        public int order;

        public Quaternion toParent;
        public Vector3 lastDirection;

        public void Setup(BaseServo servo)
        {
            this.servo = servo;
            servoTargetAngle = servo.Fields["targetAngle"];
            servoSoftMinMaxAngles = servo.Fields["softMinMaxAngles"];
            servoAngle = servoTargetAngle.GetValue<float>(servo);
        }

        // axis must be in world space
        public void GetAxis()
        {
            axis = servo.GetMainAxis();
            //axis = transform.TransformDirection(axis).normalized;
            perpendicularAxis = Perpendicular(axis);

            float angle = Vector3.Angle(axis, perpendicularAxis);

            // TODO : this align IKJoint to current servo angle, move it somewhere else ?
            servoAngle = servoTargetAngle.GetValue<float>(servo);
            Vector3 worldAxis = transform.TransformDirection(axis).normalized;
            Quaternion axisOffset = Quaternion.FromToRotation(worldAxis, transform.up); // 18/01 -> inverted from and to
            Quaternion servoOffset = Quaternion.AngleAxis(servoAngle, worldAxis);
            Quaternion transformOffset = servoOffset * axisOffset;
            transform.rotation = transformOffset * transform.rotation;


        }

        public void SyncRotationWithServo()
        {
            servoAngle = servoTargetAngle.GetValue<float>(servo);
        }


        public void UpdateDirection(Transform effector, Transform target, bool rotateToDirection = false)
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
        }

        public void ConstrainToAxis()
        {
            Quaternion toParentAfter = Quaternion.FromToRotation(transform.parent.up, transform.up);
            Quaternion correction = toParent * Quaternion.Inverse(toParentAfter);
            transform.rotation = correction * transform.rotation;
        }

        public void ConstrainToMinMaxAngle()
        {
            Vector3 requestedDirection = transform.right;

            float requestedAngleOffset = Vector3.SignedAngle(requestedDirection, lastDirection, transform.up);
            float currentAngle = servoTargetAngle.GetValue<float>(servo);
            float requestedAngle = currentAngle + requestedAngleOffset;

            Vector2 minMax = servoSoftMinMaxAngles.GetValue<Vector2>(servo);
            float servoMinAngle = minMax.x;
            float servoMaxAngle = minMax.y;

            float limit = 0f;
            if (requestedAngle < servoMinAngle)
                limit = servoMinAngle;
            else if (requestedAngle > servoMaxAngle)
                limit = servoMaxAngle;

            if (limit != 0f)
            {
                clampingFraction = (requestedAngle - limit) / requestedAngle;
                Vector3 clampedDirection = Vector3.Slerp(requestedDirection.normalized, lastDirection.normalized, clampingFraction) * requestedDirection.magnitude;
                transform.rotation = Quaternion.FromToRotation(requestedDirection, clampedDirection) * transform.rotation;
                servoAngle = limit;
                servoAngle = Mathf.Clamp(servoAngle, servoMinAngle, servoMaxAngle);
            }
            else
            {
                clampingFraction = 0f;
                servoAngle = requestedAngle;
            }

            servoTargetAngle.SetValue(servoAngle, servo);
        }

        public void Evaluate(Transform effector, Transform target, bool rotateToDirection = false)
        {
            toParent = Quaternion.FromToRotation(transform.parent.up, transform.up);
            lastDirection = transform.right;

            // CCDIK step 1 : rotate ignoring all constraints
            UpdateDirection(effector, target, rotateToDirection);
            ConstrainToAxis();
            ConstrainToMinMaxAngle();
        }

        public void ConstrainMinMaxOld()
        {
            // CCDIK step 3 : enforce joint Limits
            Vector3 fromDirection = transform.parent.rotation * perpendicularAxis;
            Vector3 toDirection = transform.rotation * perpendicularAxis;

            unclampedAngle = Vector3.SignedAngle(fromDirection, toDirection, transform.up);
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
            servoAngle = Vector3.SignedAngle(fromDirection, clampedDirection, transform.TransformDirection(axis).normalized);
            servoTargetAngle.SetValue(servoAngle, servo);
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