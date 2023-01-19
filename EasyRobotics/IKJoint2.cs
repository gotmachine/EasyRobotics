using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Expansions.Serenity;
using Steamworks;
using UnityEngine;
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

        private static Material _servoMaterial;

        private static Material ServoMaterial
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

        private static IKJoint2 InstantiateIKJoint(BaseServo servo)
        {
            GameObject jointObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            jointObject.name = $"{servo.part.partInfo.name} (IKJoint)";
            UnityEngine.Object.Destroy(jointObject.GetComponent<Collider>());
            jointObject.GetComponent<MeshRenderer>().material = ServoMaterial;
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
            GameObject movingObject = new GameObject("IKJoint MovingObject");
            movingTransform = movingObject.transform;
            transform.SetParent(movingTransform);
        }

        public void SetupTransform(Part previousInChain)
        {
            bool previousIsParent = false;
            Part currentParent = servo.part.parent;
            while (currentParent != null)
            {
                if (currentParent == previousInChain)
                {
                    previousIsParent = true;
                    break;
                }

                currentParent = currentParent.parent;
            }

            if (previousIsParent)
            {
                previousInChainAttachedByMovingPart = false;
                Part parent = servo.part.parent;

                foreach (AttachNode attachNode in servo.attachNodes)
                {
                    if (attachNode.attachedPart == parent)
                    {
                        previousInChainAttachedByMovingPart = true;
                        return;
                    }
                }

                foreach (string srfAttachMeshNames in servo.servoSrfMeshes)
                {
                    if (parent.srfAttachNode.srfAttachMeshName == srfAttachMeshNames)
                    {
                        previousInChainAttachedByMovingPart = true;
                        return;
                    }
                }
            }
            else
            {
                
            }
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
