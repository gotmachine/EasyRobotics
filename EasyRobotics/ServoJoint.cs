using Expansions.Serenity;
using System;
using System.Collections.Generic;
using UnityEngine;
using static EasyRobotics.ModuleEasyRobotics;

namespace EasyRobotics
{
    public abstract class ServoJoint
    {
        public static double targetDistWeight = 1.0;
        public static double targetAngleWeight = 1.0;
        public static double effectorDistWeight = 0.2;
        public static double rootDistWeight = 0.2;
        public static double totalWeight = targetDistWeight + targetAngleWeight + effectorDistWeight + rootDistWeight;

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

        public abstract void ExecuteGradientDescent(BasicTransform effector, BasicTransform target, TrackingConstraint trackingConstraint, float learningRateFactor);

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
        float ServoCurrentAngle { get; }
        float ServoTargetAngle { get; set; }
        float RequestedAngle { get; set; }
    }

    public abstract class RotatingServoJoint<T> : ServoJoint<T>, IRotatingServoJoint where T : BaseServo
    {
        protected BaseField servoTargetAngleField;

        public float ServoCurrentAngle => servo.currentTransformAngle();

        public abstract float ServoTargetAngle { get; set; }

        public abstract Vector2 ServoMinMaxAngle { get; }

        public Vector3 Axis { get; protected set; }
        public Vector3 PerpendicularAxis { get; protected set; }
        public float RequestedAngle { get; set; }

        protected abstract bool HasAngleLimits { get; }

        private GameObject gizmo;
        private Material gizmoMaterial;
        private Quaternion upToAxis;

        public override void OnModified(List<ServoJoint> ikChain)
        {
            Axis = servo.GetMainAxis();

            if (IsAttachedByMovingPart)
                Axis *= -1f;

            upToAxis = Quaternion.FromToRotation(Vector3.up, Axis);

            if (Mathf.Abs(Axis.x) > Mathf.Abs(Axis.z))
                PerpendicularAxis = new Vector3(-Axis.y, Axis.x, 0f);
            else
                PerpendicularAxis = new Vector3(0f, -Axis.z, Axis.y);
        }

        // Basic explanation :
        // - for the current servo angle, get a normalized error value representing how far the effector is from the target
        // - move the servo angle by some increment
        // - check the error again
        // - if the error has decreased, move in the direction of the increment
        // - else, move in the opposite direction
        public override void ExecuteGradientDescent(BasicTransform effector, BasicTransform target, TrackingConstraint trackingConstraint, float learningRateFactor)
        {
            Vector3 baseDir = PerpendicularAxis;
            Vector3 newDir = movingTransform.LocalRotation * PerpendicularAxis;

            double currentAngle = Vector3.SignedAngle(baseDir, newDir, Axis);
            double currentError = Error(effector, target, trackingConstraint);

            double autoSamplingDistance = currentError.FromToRange(0.0, 1.0, 0.05, 1.0);
            double autoLearningRate = currentError.FromToRange(0.0, 1.0, 75.0, 150.0);

            double sampledAngle = currentAngle + autoSamplingDistance;
            movingTransform.LocalRotation = Quaternion.AngleAxis((float)sampledAngle, Axis);

            double newError = Error(effector, target, trackingConstraint);

            double gradient = (newError - currentError) / autoSamplingDistance;
            double newAngle = currentAngle - (autoLearningRate * learningRateFactor * gradient);
            float finalAngle = (float)newAngle;

            if (HasAngleLimits)
            {
                Vector2 minMax = ServoMinMaxAngle;
                finalAngle = Mathf.Clamp(finalAngle, minMax.x, minMax.y);
            }
            else
            {
                if (finalAngle > 180f)
                    finalAngle -= 360f;
                else if (finalAngle < -179.99f)
                    finalAngle += 360f;
            }

            RequestedAngle = finalAngle;
            movingTransform.LocalRotation = Quaternion.AngleAxis(finalAngle, Axis);
        }

        private double NormalizedDistance(BasicTransform effector, BasicTransform target)
        {
            double error = Lib.Distance(effector.Position, target.Position);
            return UtilMath.Clamp01(-1.0 / (0.1 * error + 1.0) + 1.0);
        }

        private static Quaternion upToDown = Quaternion.FromToRotation(Vector3.up, Vector3.down);
        private double NormalizedAngle(BasicTransform effector, BasicTransform target, TrackingConstraint trackingConstraint)
        {
            double angle;
            switch (trackingConstraint)
            {
                case TrackingConstraint.PositionAndDirection:
                    angle = (Vector3.Dot(effector.Up, -target.Up) - 1.0) * -0.5;
                    break;
                case TrackingConstraint.PositionAndRotation:
                    angle = 1.0 - Math.Abs(Quaternion.Dot(effector.Rotation, target.Rotation * upToDown));
                    break;
                default:
                    return 0.0;
            }

            return Math.Pow(UtilMath.Clamp01(angle), 0.5);
        }

        // As a poor-man collision avoidance mechanism, maximize the distance between each servo
        // and the effector. This is somewhat effective at ensuring the algorithm select a solution
        // where the arm is "behind" the effector.
        private double NormalizedEffectorDistance(BasicTransform effector)
        {
            double error = Lib.Distance(effector.Position, baseTransform.Position);
            return UtilMath.Clamp01(1.0 / (0.1 * error + 1.0));
        }

        // same, but maximizing each servo distance to the root
        private double NormalizedRootDistance()
        {
            BasicTransform rootTransform = baseTransform.Root;
            if (rootTransform == baseTransform)
                return 0.0;

            double error = Lib.Distance(rootTransform.Position, baseTransform.Position);
            return UtilMath.Clamp01(1.0 / (0.1 * error + 1.0));
        }

        // see https://www.desmos.com/calculator/zkdmdlejpc for a visualization of the distance / angle error functions
        private double Error(BasicTransform effector, BasicTransform target, TrackingConstraint trackingConstraint)
        {
            double error =
                NormalizedDistance(effector, target) * targetDistWeight
                + NormalizedAngle(effector, target, trackingConstraint) * targetAngleWeight
                + NormalizedEffectorDistance(effector) * effectorDistWeight
                + NormalizedRootDistance() * rootDistWeight;

            return UtilMath.Clamp01(error / totalWeight);
        }

        public override void SendRequestToServo()
        {
            ServoTargetAngle = RequestedAngle;
        }

        public override void SyncWithServoTransform(bool baseOnly = false)
        {
            Transform fixedTransform = IsAttachedByMovingPart ? servo.movingPartObject.transform : servo.part.transform;
            baseTransform.SetPosAndRot(fixedTransform.position, fixedTransform.rotation);

            if (!baseOnly)
                movingTransform.LocalRotation = Quaternion.AngleAxis(ServoCurrentAngle, Axis);
        }

        public override bool ServoTargetReached()
        {
            // we probably want something a bit smarter here, BaseServo.IsMoving() ?
            float currentAngle = ServoCurrentAngle;
            float requestAngle = ServoTargetAngle;
            float diff = Math.Abs(currentAngle - requestAngle);

            return (diff < 1f || diff > 359f) && servo.transformRateOfMotion < 0.2f;
        }

        public override void SetIKPose(Pose pose)
        {
            Vector2 minMax = ServoMinMaxAngle;

            if (pose == Pose.Zero)
            {
                RequestedAngle = Mathf.Clamp(0f, minMax.x, minMax.y);
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
                    RequestedAngle = minMax.x + totalAngle * 0.5f;

                }
                else
                {
                    // clamp to ±120° from neutral
                    float clampOffset = Mathf.Max(0f, (totalAngle - 240f) * 0.5f);
                    if (pose == Pose.Negative)
                        RequestedAngle = minMax.x + clampOffset;
                    else
                        RequestedAngle = minMax.y - clampOffset;
                }
            }

            movingTransform.LocalRotation = Quaternion.AngleAxis(RequestedAngle, Axis);

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

        protected override bool HasAngleLimits => !servo.allowFullRotation;

        public override Vector2 ServoMinMaxAngle => servo.softMinMaxAngles;

        public override float ServoTargetAngle
        {
            get => servo.JointTargetAngle;
            set => servoTargetAngleField.SetValue(value, servo);
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

        protected override bool HasAngleLimits => true;

        public override Vector2 ServoMinMaxAngle => servo.softMinMaxAngles;

        public override float ServoTargetAngle
        {
            get => servo.JointTargetAngle; 
            set => servoTargetAngleField.SetValue(value, servo);
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
}
