using Expansions.Serenity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static UnityEngine.GraphicsBuffer;
using UnityEngine;

namespace EasyRobotics
{
    public class ModuleEasyRobotics4 : PartModule
    {
        private enum State
        {
            Inactive,
            SelectTarget,
            SelectEffector,
            SelectServo,
            SelectOrientationServo,
            Active
        }

        private List<IKJoint4> joints = new List<IKJoint4>();
        private List<BaseServo> servos = new List<BaseServo>();

        private IKJoint4 rootJoint;

        private Part effectorPart;
        private BasicTransform effector;
        private Transform target;
        private bool configChanged;

        private State state = State.Inactive;

        [KSPEvent(guiName = "Start tracking", active = true, guiActive = true, guiActiveEditor = true)]
        public void StartTracking() => state = State.Active;

        [KSPEvent(guiName = "Stop tracking", active = true, guiActive = true, guiActiveEditor = true)]
        public void StopTracking() => state = State.Inactive;

        [KSPEvent(guiName = "Select target", active = true, guiActive = true, guiActiveEditor = true)]
        public void SetTarget() => state = State.SelectTarget;

        [KSPEvent(guiName = "Select effector", active = true, guiActive = true, guiActiveEditor = true)]
        public void SetEffector() => state = State.SelectEffector;

        [KSPEvent(guiName = "Select servos", active = true, guiActive = true, guiActiveEditor = true)]
        public void SelectServos()
        {
            servos.Clear();
            joints.Clear();

            state = State.SelectServo;
        }

        [KSPEvent(guiName = "Setup chain", active = true, guiActive = true, guiActiveEditor = true)]
        public void SetupChain()
        {
            joints.Clear();
            SetupIKChain();
        }

        [KSPEvent(guiName = "Iterate", active = true, guiActive = true, guiActiveEditor = true)]
        public void Iterate()
        {
            foreach (IKJoint4 ikJoint in joints)
            {
                ikJoint.Evaluate(effector, target);
            }
        }

        [KSPField(guiActive = true, guiActiveEditor = true)]
        public int next;

        public bool done = false;

        [KSPEvent(guiName = "IterateOnebyOne", active = true, guiActive = true, guiActiveEditor = true)]
        public void IterateOnebyOne()
        {
            if (!done)
            {
                joints[next].UpdateDirection(effector, target);
                done = true;
            }
            else
            {
                joints[next].ConstrainToAxis();
                done = false;
                next = ++next % joints.Count;
            }
        }

        [KSPEvent(guiName = "ResetNext", active = true, guiActive = true, guiActiveEditor = true)]
        public void ResetNext()
        {
            next = 0;
        }

        [KSPEvent(guiName = "SetOrientationServos", active = true, guiActive = true, guiActiveEditor = true)]
        public void SetOrientationServos() => state = State.SelectOrientationServo;

        private void SetupIKChain()
        {
            for (int i = 0; i < servos.Count; i++)
            {
                BaseServo servo = servos[i];
                IKJoint4 joint = new IKJoint4(servo);
                joints.Add(joint);
                if (i > 0)
                {
                    joints[i - 1].baseTransform.SetParent(joint.movingTransform);

                    if (i == servos.Count - 1)
                    {
                        rootJoint = joint;
                    }
                }
            }

            for (int i = joints.Count - 1; i >= 0; i--)
            {
                IKJoint4 joint = joints[i];
                joint.baseTransform.name = $"IKJoint #{i} ({joint.servo.part.partInfo.name})";
                joint.movingTransform.name = $"IKJoint #{i} (MovingTransform)";
                joint.baseTransform.Position = joint.servo.transform.position;
                joint.baseTransform.Rotation = joint.servo.transform.rotation;
            }

            effector.SetParent(joints[0].movingTransform);
            effector.SetPosAndRot(effectorPart.transform.position, effectorPart.transform.rotation);
            configChanged = false;
        }

        private ScreenMessage currentMessage;

        private void QuitEditMode()
        {
            currentMessage.duration = 0f;
            currentMessage = null;
            state = State.Inactive;
        }

        private void Update()
        {
            if (rootJoint != null)
            {
                rootJoint.baseTransform.Position = rootJoint.servo.transform.position;
                rootJoint.baseTransform.Rotation = rootJoint.servo.transform.rotation;
            }

            switch (state)
            {
                case State.Inactive:
                    break;
                case State.SelectTarget:
                    {
                        if (currentMessage == null)
                            currentMessage = ScreenMessages.PostScreenMessage($"Selecting target\n[ENTER] to select\n[ESC] to end", float.MaxValue);

                        if (Input.GetKeyDown(KeyCode.Escape))
                        {
                            ScreenMessages.PostScreenMessage($"No target selected");
                            target = null;
                            QuitEditMode();
                        }
                        else if (Input.GetKeyDown(KeyCode.Return))
                        {
                            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                            {
                                ScreenMessages.PostScreenMessage($"{hit.transform.gameObject.name} selected as target");
                                target = hit.transform;
                                QuitEditMode();
                            }
                        }
                        break;
                    }
                case State.SelectEffector:
                    {
                        if (effector != null)
                            effector.SetParent(null);

                        effector = null;
                        effectorPart = null;

                        if (currentMessage == null)
                            currentMessage = ScreenMessages.PostScreenMessage($"Selecting effector\n[ENTER] to select\n[ESC] to end", float.MaxValue);

                        if (Input.GetKeyDown(KeyCode.Escape))
                        {
                            ScreenMessages.PostScreenMessage($"No effector selected");
                            configChanged = true;
                            QuitEditMode();
                        }
                        else if (Input.GetKeyDown(KeyCode.Return))
                        {
                            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                            {
                                Part part = FlightGlobals.GetPartUpwardsCached(hit.transform.gameObject);
                                if (part != null)
                                {
                                    effectorPart = part;
                                    effector = new BasicTransform(null, part.transform.position, part.transform.rotation);
                                    configChanged = true;
                                    ScreenMessages.PostScreenMessage($"{part.partInfo.title} selected as effector");
                                    QuitEditMode();
                                }
                            }
                        }
                        break;
                    }
                case State.SelectServo:
                    {
                        if (currentMessage == null)
                            currentMessage = ScreenMessages.PostScreenMessage($"Selecting servos\n[ENTER] to select\n[ESC] to end", float.MaxValue);

                        if (Input.GetKeyDown(KeyCode.Escape))
                        {
                            QuitEditMode();
                            break;
                        }

                        if (Input.GetKeyDown(KeyCode.Return))
                        {
                            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                            {
                                Part part = FlightGlobals.GetPartUpwardsCached(hit.transform.gameObject);
                                if (part != null)
                                {
                                    BaseServo servo = part.FindModuleImplementing<BaseServo>();
                                    if (servo != null)
                                    {
                                        servos.Add(servo);
                                        configChanged = true;
                                        ScreenMessages.PostScreenMessage($"{part.partInfo.title} added to kinematics chain");
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                    }
                case State.SelectOrientationServo:
                {
                    if (currentMessage == null)
                        currentMessage = ScreenMessages.PostScreenMessage($"Selecting orientation servos\n[ENTER] to select\n[ESC] to end", float.MaxValue);

                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        QuitEditMode();
                        break;
                    }

                    if (Input.GetKeyDown(KeyCode.Return))
                    {
                        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                        {
                            Part part = FlightGlobals.GetPartUpwardsCached(hit.transform.gameObject);
                            if (part != null)
                            {
                                BaseServo servo = part.FindModuleImplementing<BaseServo>();
                                if (servo != null)
                                {
                                    IKJoint4 joint = joints.Find(p => p.servo == servo);
                                    if (joint != null)
                                    {
                                        joint.rotateToDirection = !joint.rotateToDirection;
                                        ScreenMessages.PostScreenMessage($"{part.partInfo.title} joint set to rotate to direction : {joint.rotateToDirection}");
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    break;
                }

                case State.Active:
                    if (configChanged)
                    {
                        SetupChain();
                    }


                    foreach (IKJoint4 ikJoint in joints)
                    {
                        ikJoint.Evaluate(effector, target);
                    }
                    break;
            }



        }

        private void OnRenderObject()
        {
            foreach (IKJoint4 ikJoint in joints)
            {
                Vector3 pos = ikJoint.baseTransform.Position;
                Vector3 axis = ikJoint.baseTransform.Rotation * ikJoint.axis * 0.5f;
                DrawTools.DrawLine(pos - axis, pos + axis, Color.red);
                Vector3 perp = ikJoint.baseTransform.Rotation * ikJoint.perpendicularAxis;
                DrawTools.DrawLine(pos, pos + perp, Color.yellow);
                Vector3 perpmoved = ikJoint.movingTransform.Rotation * ikJoint.perpendicularAxis;
                DrawTools.DrawLine(pos, pos + perpmoved, Color.green);
            }

            if (effector != null)
            {
                DrawTools.DrawTransform(effector);
            }
        }
    }
}
