﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Expansions.Serenity;
using Steamworks;
using UnityEngine;
using static KSP.UI.Screens.RDNode;
using static SoftMasking.SoftMask;

// effector (part)
// -> movingObject 1
//   -> ikjoint 1 (servo)
//     -> movingObject 2
//       -> ikjoint 2 (servo)

namespace EasyRobotics
{
    public class IKJoint2 : MonoBehaviour
    {
        public BaseServo servo;
        public bool previousInChainAttachedByMovingPart;
        public Transform movingTransform;
        BaseField servoTargetAngle;
        BaseField servoSoftMinMaxAngles;

        public Vector3 axis; // local space
        public Vector3 perpendicularAxis;

        private float ServoAngle
        {
            get => servoTargetAngle.GetValue<float>(servo);
            set => servoTargetAngle.SetValue(value, servo);
        }

        private Vector2 ServoMinMaxAngle => servoSoftMinMaxAngles.GetValue<Vector2>(servo);

        private static Material _servoMaterial;

        public static Material ServoMaterial
        {
            get
            {
                if (_servoMaterial == null)
                {
                    _servoMaterial = new Material(Shader.Find("KSP/Alpha/Translucent"));
                    _servoMaterial.SetColor("_Color", new Color(1f, 1f, 0f, 0.3f));
                }

                return _servoMaterial;
            }
        }

        public static IKJoint2 InstantiateIKJoint(BaseServo servo)
        {
            GameObject jointObject = new GameObject();
            IKJoint2 ikJoint = jointObject.AddComponent<IKJoint2>();
            ikJoint.Setup(servo);
            return ikJoint;
        }


        public void Setup(BaseServo servo)
        {
            this.servo = servo;
            servoTargetAngle = servo.Fields["targetAngle"];
            servoSoftMinMaxAngles = servo.Fields["softMinMaxAngles"];
            axis = servo.GetMainAxis();
            perpendicularAxis = Perpendicular(axis);
            GameObject movingObject = new GameObject();
            movingTransform = movingObject.transform;
            movingTransform.SetParent(transform);
            movingTransform.localPosition = Vector3.zero;
            movingTransform.localRotation = Quaternion.AngleAxis(ServoAngle, axis);
        }


        public void UpdateDirection(Transform effector, Transform target, bool rotateToDirection = false)
        {


            // CCDIK step 1 : rotate ignoring all constraints
            if (rotateToDirection)
            {
                // Point the effector along the target direction
                // In case of a 5+ DoF chain, do this with the last servos to match target orientation,
                // while other servos are matching target direction
                movingTransform.rotation = Quaternion.FromToRotation(effector.up, target.forward);
            }
            else
            {
                // Point the effector towards the target
                Vector3 directionToEffector = effector.position - transform.position;
                Vector3 directionToTarget = target.position - transform.position;
                Quaternion rotationOffset = Quaternion.FromToRotation(directionToEffector, directionToTarget);
                movingTransform.rotation = rotationOffset * movingTransform.rotation;
            }
        }

        public void ConstrainToAxis()
        {
            Vector3 from = movingTransform.rotation * axis;
            Vector3 to = transform.rotation * axis;
            Quaternion correction = Quaternion.FromToRotation(from, to);
            movingTransform.rotation = correction * movingTransform.rotation;
        }

        public void ConstrainToMinMaxAngle()
        {
            Vector3 baseDir = perpendicularAxis;
            Vector3 newDir = movingTransform.localRotation * perpendicularAxis;

            float requestedAngle = Vector3.SignedAngle(baseDir, newDir, axis);

            Vector2 minMax = servoSoftMinMaxAngles.GetValue<Vector2>(servo);
            float servoMinAngle = Math.Max(minMax.x, -179.99f);
            float servoMaxAngle = Math.Min(minMax.y, 179.99f);

            if (requestedAngle < servoMinAngle)
            {
                requestedAngle = servoMinAngle;
                movingTransform.localRotation = Quaternion.AngleAxis(servoMinAngle, axis);
            }
            else if (requestedAngle > servoMaxAngle)
            {
                requestedAngle = servoMaxAngle;
                movingTransform.localRotation = Quaternion.AngleAxis(servoMaxAngle, axis);
            }

            ServoAngle = requestedAngle;
        }

        public void Evaluate(Transform effector, Transform target, bool rotateToDirection = false)
        {
            // CCDIK step 1 : rotate ignoring all constraints
            UpdateDirection(effector, target, rotateToDirection);
            ConstrainToAxis();
            ConstrainToMinMaxAngle();
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
