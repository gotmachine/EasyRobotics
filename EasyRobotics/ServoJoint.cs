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
using static EasyRobotics.ModuleEasyRobotics;
using static KSP.UI.Screens.RDNode;
using static SoftMasking.SoftMask;

// effector (part)
// -> movingObject 1
//   -> ikjoint 1 (servo)
//     -> movingObject 2
//       -> ikjoint 2 (servo)

namespace EasyRobotics
{
    public abstract class ServoJoint
    {
        public enum IkMode
        {
            Position,
            Rotation
        }

        public enum Pose
        {
            Zero,
            Neutral,
            Positive,
            Negative
        }

        public BasicTransform baseTransform;
        public BasicTransform movingTransform;
        public abstract BaseServo BaseServo { get; }
        public bool IsAttachedByMovingPart { get; set; }

        public abstract void OnModified(List<ServoJoint> ikChain);

        public abstract void ExecuteIK(BasicTransform effector, BasicTransform target, TrackingConstraint trackingConstraint);

        public abstract void SendRequestToServo();

        public abstract void SyncWithServoTransform(bool baseOnly = false);

        public abstract void SetIKPose(Pose pose);

        public abstract bool ServoTargetReached();

        public abstract void ShowGizmo(bool show);

        public abstract void SyncGizmo();

        public abstract void SetGizmoColor(Color color);



        public abstract void OnDestroy();
    }


    public abstract class ServoJoint<T> : ServoJoint where T : BaseServo
    {
        public T servo;
        public override BaseServo BaseServo => servo;
    }

    public interface IRotatingServoJoint
    {
        Vector3 Axis { get; }
        Vector3 PerpendicularAxis { get; }
        float ServoTargetAngle { get; }
        ServoJoint.IkMode IkMode { get; set; }
        void CycleIkMode();
    }

    public abstract class RotatingServoJoint<T> : ServoJoint<T>, IRotatingServoJoint where T : BaseServo
    {
        protected BaseField servoTargetAngleField;

        public abstract float ServoTargetAngle { get; protected set; }

        public abstract Vector2 ServoMinMaxAngle { get; }

        public Vector3 Axis { get; protected set; }
        public Vector3 PerpendicularAxis { get; protected set; }
        public IkMode IkMode { get; set; } = IkMode.Position;
        protected float requestedAngle;

        public void CycleIkMode() => IkMode = IkMode == IkMode.Position ? IkMode.Rotation : IkMode.Position;

        protected abstract bool AngleIsInverted { get; }

        private GameObject gizmo;
        private Material gizmoMaterial;
        private Quaternion upToAxis;

        public override void OnModified(List<ServoJoint> ikChain)
        {
            Axis = servo.GetMainAxis();

            if (IsAttachedByMovingPart)
                Axis *= -1f;

            upToAxis = Quaternion.FromToRotation(Vector3.up, Axis);

            //if (AngleIsInverted)
            //    Axis *= -1f;

            if (Mathf.Abs(Axis.x) > Mathf.Abs(Axis.z))
                PerpendicularAxis = new Vector3(-Axis.y, Axis.x, 0f);
            else
                PerpendicularAxis = new Vector3(0f, -Axis.z, Axis.y);
        }

        public override void ExecuteIK(BasicTransform effector, BasicTransform target, TrackingConstraint trackingConstraint)
        {
            // CCDIK step 1 : rotate ignoring axis/limits
            if (IkMode == IkMode.Rotation && trackingConstraint != TrackingConstraint.Position)
            {
                if (trackingConstraint == TrackingConstraint.PositionAndRotation)
                {
                    // ???
                    movingTransform.Rotation = (effector.Rotation.Inverse() * target.Rotation.Inverse()) * movingTransform.Rotation;
                }
                else if (trackingConstraint == TrackingConstraint.PositionAndDirection)
                {
                    // Point the effector along the target direction
                    // In case of a 5+ DoF chain, do this with the last servos to match target orientation,
                    // while other servos are matching target direction
                    movingTransform.Rotation = Quaternion.FromToRotation(effector.Up, -target.Up) * movingTransform.Rotation;
                }
            }
            else
            {
                // Point the effector towards the target
                Vector3 directionToEffector = effector.Position - baseTransform.Position;
                Vector3 directionToTarget = target.Position - baseTransform.Position;
                Quaternion rotationOffset = Quaternion.FromToRotation(directionToEffector, directionToTarget);
                movingTransform.Rotation = rotationOffset * movingTransform.Rotation;
            }

            // CCDIK step 2 : Constrain resulting rotation to axis
            Vector3 from = movingTransform.Rotation * Axis;
            Vector3 to = baseTransform.Rotation * Axis;
            Quaternion correction = Quaternion.FromToRotation(from, to);
            movingTransform.Rotation = correction * movingTransform.Rotation;

            // CCDIK step 3 : Constrain to min/max angle
            Vector3 baseDir = PerpendicularAxis;
            Vector3 newDir = movingTransform.LocalRotation * PerpendicularAxis;
            requestedAngle = Vector3.SignedAngle(baseDir, newDir, Axis);

            Vector2 minMax = ServoMinMaxAngle;
            float servoMinAngle = Math.Max(minMax.x, -179.99f);
            float servoMaxAngle = Math.Min(minMax.y, 179.99f);

            if (requestedAngle < servoMinAngle)
                requestedAngle = servoMinAngle;
            else if (requestedAngle > servoMaxAngle)
                requestedAngle = servoMaxAngle;

            movingTransform.LocalRotation = Quaternion.AngleAxis(requestedAngle, Axis);
        }

        public override void SendRequestToServo()
        {
            ServoTargetAngle = requestedAngle;
        }

        public override void SyncWithServoTransform(bool baseOnly = false)
        {

            Transform fixedTransform = IsAttachedByMovingPart ? servo.movingPartObject.transform : servo.part.transform;
            baseTransform.SetPosAndRot(fixedTransform.position, fixedTransform.rotation);
            if (!baseOnly)
            {
                movingTransform.LocalRotation = Quaternion.AngleAxis(servo.currentTransformAngle(), Axis);
            }

        }

        public override bool ServoTargetReached()
        {
            // we probably want something a bit smarter here, BaseServo.IsMoving() ?
            float currentAngle = servo.currentTransformAngle();
            float requestAngle = ServoTargetAngle;
            float diff = Math.Abs(currentAngle - requestAngle);

            return diff < 1f || diff > 359f;
        }

        public override void SetIKPose(Pose pose)
        {
            Vector2 minMax = ServoMinMaxAngle;

            if (pose == Pose.Zero)
            {
                requestedAngle = Mathf.Clamp(0f, minMax.x, minMax.y);
            }
            else
            {
                float totalAngle;
                if (minMax.x > 0f)
                    totalAngle = minMax.y - minMax.x;
                else if (minMax.y < 0f)
                    totalAngle = Mathf.Abs(minMax.x - minMax.y);
                else
                    totalAngle = Mathf.Abs(minMax.x) + minMax.y;

                // set to neutral
                if (pose == Pose.Neutral)
                {
                    requestedAngle = minMax.x + totalAngle * 0.5f;

                }
                else
                {
                    // clamp to ±120° from neutral
                    float clampOffset = Mathf.Max(0f, (totalAngle - 240f) * 0.5f);
                    if (pose == Pose.Negative)
                        requestedAngle = minMax.x + clampOffset;
                    else
                        requestedAngle = minMax.y - clampOffset;
                }
            }

            movingTransform.LocalRotation = Quaternion.AngleAxis(requestedAngle, Axis);

        }

        public override void ShowGizmo(bool show)
        {
            if (show)
            {
                if (gizmo.IsNullOrDestroyed())
                    InstantiateGizmo();

                gizmo.SetActive(true);
            }
            else
            {
                if (gizmo.IsNotNullOrDestroyed())
                    gizmo.SetActive(false);
            }
        }

        public override void SyncGizmo()
        {
            if (gizmo.IsNullOrDestroyed())
                return;

            baseTransform.GetPosAndRot(out Vector3 pos, out Quaternion rot);
            gizmo.transform.SetPositionAndRotation(pos, rot * upToAxis);
        }

        public override void SetGizmoColor(Color color)
        {
            if (gizmo.IsNullOrDestroyed())
                return;

            gizmoMaterial.SetColor(Assets.BurnColorID, color);
        }

        public override void OnDestroy()
        {
            if (gizmo.IsNotNullOrDestroyed())
                UnityEngine.Object.Destroy(gizmo);

            gizmo = null;
            gizmoMaterial = null;
        }

        private void InstantiateGizmo()
        {
            gizmo = UnityEngine.Object.Instantiate(Assets.RotationGizmoPrefab);
            gizmo.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            gizmoMaterial = gizmo.transform.GetChild(0).GetComponent<MeshRenderer>().material;
        }
    }

    public sealed class RotationServoJoint : RotatingServoJoint<ModuleRoboticRotationServo>
    {
        public RotationServoJoint(ModuleRoboticRotationServo servo)
        {
            this.servo = servo;
            servoTargetAngleField = servo.Fields[nameof(ModuleRoboticRotationServo.targetAngle)];
            OnModified(null);
            baseTransform = new BasicTransform(null, servo.transform.position, servo.transform.rotation);
            movingTransform = new BasicTransform(baseTransform, Vector3.zero, Quaternion.AngleAxis(ServoTargetAngle, Axis), true);
        }

        protected override bool AngleIsInverted => servo.inverted;

        public override Vector2 ServoMinMaxAngle => servo.softMinMaxAngles;

        public override float ServoTargetAngle
        {
            get => servo.JointTargetAngle;
            protected set => servoTargetAngleField.SetValue(value, servo);
        }
    }

    public sealed class HingeServoJoint : RotatingServoJoint<ModuleRoboticServoHinge>
    {
        public HingeServoJoint(ModuleRoboticServoHinge servo)
        {
            this.servo = servo;
            servoTargetAngleField = servo.Fields[nameof(ModuleRoboticServoHinge.targetAngle)];
            OnModified(null);
            baseTransform = new BasicTransform(null, servo.transform.position, servo.transform.rotation);
            movingTransform = new BasicTransform(baseTransform, Vector3.zero, Quaternion.AngleAxis(ServoTargetAngle, Axis), true);
        }

        protected override bool AngleIsInverted => false;

        public override Vector2 ServoMinMaxAngle => servo.softMinMaxAngles;

        public override float ServoTargetAngle
        {
            get => servo.JointTargetAngle;
            protected set => servoTargetAngleField.SetValue(value, servo);
        }
    }

    //public class LinearServoJoint : ServoJoint<ModuleRoboticServoPiston>
    //{
    //    private ServoJoint root;
    //    private BaseField servoTargetExtension;
    //    private BaseField servSoftMinMaxExtension;
    //    private int _axisIndex;

    //    private float ServoTargetExtension
    //    {
    //        get => servo.targetExtension;
    //        set => servoTargetExtension.SetValue(value, servo);
    //    }

    //    private Vector2 ServoMinMaxExtension => servo.softMinMaxExtension;

    //    private float IKCurrentExtension
    //    {
    //        get => movingTransform.LocalPosition[_axisIndex];
    //        set => movingTransform.LocalPosition = new Vector3 { [_axisIndex] = value };
    //    }

    //    private Vector3 IKWorldDir => (movingTransform.Rotation * movingTransform.LocalPosition).normalized;

    //    private float requestedExtension;

    //    public override void OnModified(List<ServoJoint> ikChain)
    //    {
    //        Vector3 axis = servo.GetMainAxis();
    //        for (int i = 0; i < 3; i++)
    //        {
    //            if (axis[i] != 0f)
    //            {
    //                _axisIndex = i;
    //                break;
    //            }
    //        }

    //        if (ikChain.Count == 0)
    //            root = null;
    //        else
    //            root = ikChain[0];
    //    }

    //    public override void ExecuteIK(BasicTransform effector, BasicTransform target, TrackingConstraint trackingConstraint)
    //    {
    //        Vector3 rootPos = root.baseTransform.Position;
    //        Vector3 rootToEffector = effector.Position - rootPos;
    //        Vector3 rootToTarget = target.Position - rootPos;
    //        float diff = rootToTarget.magnitude - rootToEffector.magnitude;
    //        float servoAngle = Vector3.Angle(rootToTarget, IKWorldDir);
    //        float angleFactor = servoAngle >= 0f ? 1f / Mathf.Cos(servoAngle) : 1f;
    //        if (diff > 0f)
    //        {
    //            // effector can't reach target
    //            float maxReach = ServoMinMaxExtension.y - IKCurrentExtension;
    //            requestedExtension = Mathf.Min(IKCurrentExtension, diff * angleFactor);
    //            IKCurrentExtension = requestedExtension;
    //        }
    //        else
    //        {
    //            float maxReach = ServoMinMaxExtension.y - IKCurrentExtension;
    //            // effector overshoot target
    //        }


    //    }

    //    public override void SendRequestToServo()
    //    {
    //        ServoTargetExtension = requestedExtension;
    //    }

    //    public override void SyncWithServoTransform(bool baseOnly = false)
    //    {
    //        Transform fixedTransform = IsAttachedByMovingPart ? servo.movingPartObject.transform : servo.part.transform;
    //        baseTransform.SetPosAndRot(fixedTransform.position, fixedTransform.rotation);
    //        if (!baseOnly)
    //        {
    //            movingTransform.LocalPosition = new Vector3 { [_axisIndex] = servo.currentExtension };
    //        }
    //    }

    //    public override void SetIKPose(Pose pose)
    //    {

    //    }
    //}

    /*
    public class ServoJointOld
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

        public ServoJointOld(BaseServo servo)
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

        public void UpdateDirection(BasicTransform effector, BasicTransform target, TrackingConstraint trackingConstraint)
        {
            // CCDIK step 1 : rotate ignoring axis/limits
            if (rotateToDirection)
            {
                if (trackingConstraint == TrackingConstraint.PositionAndRotation)
                {
                    // ???
                    movingTransform.Rotation = (effector.Rotation.Inverse() * target.Rotation.Inverse()) * movingTransform.Rotation;
                }
                else if (trackingConstraint == TrackingConstraint.PositionAndDirection)
                {
                    // Point the effector along the target direction
                    // In case of a 5+ DoF chain, do this with the last servos to match target orientation,
                    // while other servos are matching target direction
                    movingTransform.Rotation = Quaternion.FromToRotation(effector.Up, -target.Up) * movingTransform.Rotation;
                }
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

        public void Evaluate(BasicTransform effector, BasicTransform target, TrackingConstraint trackingConstraint)
        {
            // CCDIK step 1 : rotate ignoring all constraints
            UpdateDirection(effector, target, trackingConstraint);
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
    */
}
