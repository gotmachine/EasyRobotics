using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Expansions.Serenity;
using Steamworks;
using UnityEngine;
using Vectrosity;
using VehiclePhysics;
using static KSP.UI.Screens.RDNode;
using static SoftMasking.SoftMask;

// effector (part)
// -> movingObject 1
//   -> ikjoint 1 (servo)
//     -> movingObject 2
//       -> ikjoint 2 (servo)

namespace EasyRobotics
{
    public class ServoJoint
    {
        public BasicTransform baseTransform;
        public BasicTransform movingTransform;
        public BaseServo servo;

        BaseField servoTargetAngle;
        BaseField servoSoftMinMaxAngles;

        public Vector3 axis; // local space
        public Vector3 perpendicularAxis;
        public bool rotateToDirection;
        public bool isInverted;

        private float requestedAngle;


        private bool overlayEnabled;
        private VectorLine axisOverlay;

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

        public ServoJoint(BaseServo servo)
        {
            this.servo = servo;
            servoTargetAngle = servo.Fields["targetAngle"];
            servoSoftMinMaxAngles = servo.Fields["softMinMaxAngles"];
            UpdateAxis();
            baseTransform = new BasicTransform(null, servo.transform.position, servo.transform.rotation);
            movingTransform = new BasicTransform(baseTransform, Vector3.zero, Quaternion.AngleAxis(ServoAngle, axis), true);


        }

        public void UpdateAxis()
        {
            axis = servo.GetMainAxis();
            if (isInverted)
                axis *= -1f;

            perpendicularAxis = Perpendicular(axis);
        }

        private const float overlayRadius = 0.25f;
        private const int segmentsInAxisCircle = 16;
        private static Vector3[] axisOverlayDefaultPoints = new Vector3[segmentsInAxisCircle * 2 + 2];


        public bool OverlayEnabled
        {
            get => overlayEnabled;
            set
            {
                overlayEnabled = value;
                if (value)
                {
                    if (axisOverlay == null)
                    {
                        List<Vector3> axisPoints = new List<Vector3>(axisOverlayDefaultPoints);
                        axisOverlay = new VectorLine("IKServoOverlay", axisPoints, 1.5f, LineType.Discrete);
                        axisOverlay.MakeCircle(Vector3.zero, axis, overlayRadius, segmentsInAxisCircle);
                        axisPoints[segmentsInAxisCircle * 2] = perpendicularAxis * -overlayRadius;
                        axisPoints[segmentsInAxisCircle * 2 + 1] = perpendicularAxis * overlayRadius;
                        axisOverlay.SetColor(Color.red);
                        axisOverlay.layer = 2; // Ignore raycast
                    }

                    axisOverlay.active = true;
                }
                else
                {
                    if (axisOverlay != null)
                    {
                        axisOverlay.active = false;
                    }
                }
            }
        }

        public void SetOverlaySelected(bool selected)
        {
            if (selected)
            {
                axisOverlay.SetColor(Color.green);
                axisOverlay.lineWidth = 2.5f;
            }
            else
            {
                axisOverlay.SetColor(Color.red);
                axisOverlay.lineWidth = 1.5f;
            }
        }

        public void RenderOverlay()
        {
            baseTransform.GetPosAndRot(out Vector3 pos, out Quaternion rot);
            axisOverlay.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
            axisOverlay.Draw3D();
        }

        public void OnDestroy()
        {
            VectorLine.Destroy(ref axisOverlay);
        }

        public void UpdateDirection(BasicTransform effector, BasicTransform target)
        {
            // CCDIK step 1 : rotate ignoring all constraints
            if (rotateToDirection)
            {
                // Point the effector along the target direction
                // In case of a 5+ DoF chain, do this with the last servos to match target orientation,
                // while other servos are matching target direction


                movingTransform.Rotation = Quaternion.FromToRotation(effector.Up, -target.Up) * movingTransform.Rotation;
            }
            else
            {
                // Point the effector towards the target
                Vector3 directionToEffector = effector.Position - baseTransform.Position;
                Vector3 directionToTarget = target.Position - baseTransform.Position;
                Quaternion rotationOffset = Quaternion.FromToRotation(directionToEffector, directionToTarget);
                movingTransform.Rotation = rotationOffset * movingTransform.Rotation;
            }
        }

        public void ConstrainToAxis()
        {
            Vector3 from = movingTransform.Rotation * axis;
            Vector3 to = baseTransform.Rotation * axis;
            Quaternion correction = Quaternion.FromToRotation(from, to);
            movingTransform.Rotation = correction * movingTransform.Rotation;
        }

        public void ConstrainToMinMaxAngle()
        {
            Vector3 baseDir = perpendicularAxis;
            Vector3 newDir = movingTransform.LocalRotation * perpendicularAxis;
            requestedAngle = Vector3.SignedAngle(baseDir, newDir, axis);

            Vector2 minMax = servoSoftMinMaxAngles.GetValue<Vector2>(servo);
            float servoMinAngle = Math.Max(minMax.x, -179.99f);
            float servoMaxAngle = Math.Min(minMax.y, 179.99f);

            if (requestedAngle < servoMinAngle)
            {
                requestedAngle = servoMinAngle;
                movingTransform.LocalRotation = Quaternion.AngleAxis(servoMinAngle, axis);
            }
            else if (requestedAngle > servoMaxAngle)
            {
                requestedAngle = servoMaxAngle;
                movingTransform.LocalRotation = Quaternion.AngleAxis(servoMaxAngle, axis);
            }
        }

        public bool TargetAngleReached()
        {
            float currentAngle = servo.currentTransformAngle();
            float requestAngle = ServoAngle;
            float diff = Math.Abs(currentAngle - requestAngle);

            return diff < 2.5 || diff > 357.5;
        }

        public void SendRequestedAngleToServo()
        {
            ServoAngle = requestedAngle;
        }

        public void Evaluate(BasicTransform effector, BasicTransform target)
        {
            // CCDIK step 1 : rotate ignoring all constraints
            UpdateDirection(effector, target);
            ConstrainToAxis();
            ConstrainToMinMaxAngle();
        }

        public void SyncWithServoTransform(bool baseOnly = false)
        {
            
            Transform fixedTransform = isInverted ? servo.movingPartObject.transform : servo.part.transform;
            baseTransform.SetPosAndRot(fixedTransform.position, fixedTransform.rotation);
            if (!baseOnly)
            {
                movingTransform.LocalRotation = Quaternion.AngleAxis(servo.currentTransformAngle(), axis);
            }
            
        }

        public void SyncWithPartOrg(bool syncRootMode = false)
        {
            // TODO : this is wrong if root servo is attached by its moving part.
            // in our current solution, we handle inversions by inverting axis, 
            // this mean that in the case of an inverted part attachement, our baseTransform
            // is the stock moving part, while our MovingTransform is the stock "base" part.
            // This isn't an issue when we sync all servos in the chain at once, but it's an issue
            // when we want to only sync the root servo to keep up with its position changes while
            // not syncing the rest of the chain.
            // The problem is that since we setup out chain according to an arbitrary initial orgPos
            // when inverted, I can't find a reliable way to put things together back.
            // The problem is basically : what should the baseTransform rotation be, given that :
            // - on the stock side, it represent the moving object rotation
            // - the one we start with is arbitrarily rotated because we synced it with the part (which
            //   is in turn arbtrarily rotated)
            // I'm not sure it's actually solvable without storing the original arbitrary rotation offset
            // Instead,it might be necessary to just don't do the "inverted axis" trick, and to always
            // sync with the moving part when inverted. And either way, I'm not sure relying on orgpos/orgrot
            // is actually a good idea, as if the root is far away from the ik chain, we will have physics
            // deformation noise getting in the way.

            Part part = servo.part;
            Transform rootTransform = part.vessel.rootPart.transform;
            Quaternion rootRot = rootTransform.rotation;

            Vector3 pos = rootRot * part.orgPos + rootTransform.position;
            Quaternion rot = rootRot * part.orgRot;

            if (syncRootMode)
            {
                if (isInverted)
                    rot = servo.movingPartObject.transform.rotation * movingTransform.LocalRotation.Inverse();

                baseTransform.SetPosAndRot(pos, rot);
            }
            else
            {
                baseTransform.SetPosAndRot(pos, rot);
                movingTransform.LocalRotation = Quaternion.AngleAxis(servo.currentTransformAngle(), axis);
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
