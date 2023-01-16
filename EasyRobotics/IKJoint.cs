using Expansions.Serenity;
using UnityEngine;

namespace EasyRobotics
{
    public class IKJoint : MonoBehaviour
    {
        public BaseServo servo;
        public IKJoint root;
        public IKJoint parent;
        public Vector3 axis; // local space
        Vector3 perpendicularAxis;
        public float maxAngle;
        public float currentAngle;
        public float constrainToNormalAngle;
        public int order;

        public Quaternion initialRotation;

        public void Setup(BaseServo servo)
        {
            this.servo = servo;

            maxAngle = 90f; // will need to do something more complicated anyway

        }

        // axis must be in world space
        public void GetAxis()
        {
            axis = servo.GetMainAxis();
            //axis = transform.TransformDirection(axis).normalized;
            perpendicularAxis = Perpendicular(axis);
            initialRotation = transform.localRotation;
        }

        public void Evaluate(Transform effector, Transform target, bool rotateToDirection = false)
        {
            // Rotate ignoring all constraints
            if (rotateToDirection)
            {
                // Point the effector along the target direction
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

            // Constrain to rotate around the axis
            Vector3 currentHingeAxis = transform.rotation * axis;
            Vector3 hingeAxis = transform.parent.rotation * axis;

            Quaternion hingeRotationOffset = Quaternion.FromToRotation(currentHingeAxis, hingeAxis);
            transform.rotation = hingeRotationOffset * transform.rotation;

            // Enforce Joint Limits
            Vector3 fromDirection = transform.rotation * perpendicularAxis;
            Vector3 toDirection = ConstrainToNormal(fromDirection, transform.parent.rotation * perpendicularAxis, maxAngle);

            transform.rotation = Quaternion.FromToRotation(fromDirection, toDirection) * transform.rotation;

            currentAngle = CurrentAngle();

            if (servo is ModuleRoboticRotationServo || servo is ModuleRoboticServoHinge)
                servo.Fields["targetAngle"].SetValue(currentAngle, servo);
        }

        private Vector3 Perpendicular(Vector3 vec)
        {
            return Mathf.Abs(vec.x) > Mathf.Abs(vec.z) ? new Vector3(-vec.y, vec.x, 0f)
                : new Vector3(0f, -vec.z, vec.y);
        }

        private Vector3 ConstrainToNormal(Vector3 direction, Vector3 normalDirection, float maxAngle)
        {
            if (maxAngle <= 0f) 
                return normalDirection.normalized * direction.magnitude; 
            if (maxAngle >= 180f) 
                return direction;

            constrainToNormalAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(direction.normalized, normalDirection.normalized), -1f, 1f)) * Mathf.Rad2Deg;

            return Vector3.Slerp(direction.normalized, normalDirection.normalized, (constrainToNormalAngle - maxAngle) / constrainToNormalAngle) * direction.magnitude;
        }

        private float CurrentAngle()
        {
            Vector3 fromDirection = transform.rotation * perpendicularAxis;
            Vector3 normalDirection = transform.parent.rotation * perpendicularAxis;

            float angle = Vector3.SignedAngle(normalDirection, fromDirection, transform.TransformDirection(axis).normalized);

            //float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(fromDirection.normalized, normalDirection.normalized), -1f, 1f)) * Mathf.Rad2Deg;

            //(transform.localRotation * Quaternion.Inverse(initialRotation)).ToAngleAxis(out float angle, out Vector3 angleAxis);

            //if (Vector3.Angle(servo.GetMainAxis(), angleAxis) > 90f)
            //{
            //    angle = -angle;
            //}

            //angle = Mathf.DeltaAngle(0f, angle);

            //if (parent == null)
            //{
            //    angle = -angle;
            //}

            return angle;
        }
    }
}