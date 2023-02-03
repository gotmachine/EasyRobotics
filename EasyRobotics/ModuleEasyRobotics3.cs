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
    public class ModuleEasyRobotics3 : PartModule
    {
        private enum State
        {
            Inactive,
            SelectTarget,
            SelectEffector,
            SelectServo,
            Active
        }

        private List<IKJoint3> joints = new List<IKJoint3>();
        private List<BaseServo> servos = new List<BaseServo>();

        private IKJoint3 rootJoint;

        private Part effectorPart;
        private Transform effector;
        private BasicTransform effector2;
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
            foreach (IKJoint3 ikJoint in joints)
                ikJoint.gameObject.DestroyGameObject();

            servos.Clear();
            joints.Clear();

            state = State.SelectServo;
        }

        [KSPEvent(guiName = "Setup chain", active = true, guiActive = true, guiActiveEditor = true)]
        public void SetupChain()
        {
            foreach (IKJoint3 ikJoint in joints)
                ikJoint.gameObject.DestroyGameObject();

            joints.Clear();
            SetupIKChain();
        }

        [KSPEvent(guiName = "Iterate", active = true, guiActive = true, guiActiveEditor = true)]
        public void Iterate()
        {
            foreach (IKJoint3 ikJoint in joints)
            {
                ikJoint.Evaluate(effector, effector2, target);
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
                joints[next].UpdateDirection(effector, effector2, target);
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

        [KSPEvent(guiName = "LogChain", active = true, guiActive = true, guiActiveEditor = true)]
        public void LogChain()
        {
            next = 0;
        }

        private void SetupIKChain()
        {
            for (int i = 0; i < servos.Count; i++)
            {
                BaseServo servo = servos[i];
                IKJoint3 joint = IKJoint3.InstantiateIKJoint(servo);
                joints.Add(joint);
                if (i > 0)
                {
                    joints[i - 1].transform.SetParent(joint.movingTransform);
                    joints[i - 1].baseTransform.Parent = joint.movingTransform2;

                    if (i == servos.Count - 1)
                    {
                        joint.transform.SetParent(servo.transform);
                        rootJoint = joint;
                    }
                }
            }

            for (int i = joints.Count - 1; i >= 0; i--)
            {
                IKJoint3 joint = joints[i];
                joint.gameObject.name = $"IKJoint #{i} ({joint.servo.part.partInfo.name})";
                joint.movingTransform.gameObject.name = $"IKJoint #{i} (MovingTransform)";
                joint.transform.position = joint.servo.transform.position;
                joint.transform.rotation = joint.servo.transform.rotation;
                joint.baseTransform.Position = joint.servo.transform.position;
                joint.baseTransform.Rotation = joint.servo.transform.rotation;
            }

            effector.SetParent(joints[0].movingTransform);
            effector.position = effectorPart.transform.position;
            effector.rotation = effectorPart.transform.rotation;

            effector2.Parent = joints[0].movingTransform2;
            effector2.Position = effectorPart.transform.position;
            effector2.Rotation = effectorPart.transform.rotation;
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
                            Destroy(effector.gameObject);

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
                                    effector = InstantiateEffector(part).transform;
                                    effector.position = part.transform.position;
                                    effector.rotation = part.transform.rotation;
                                    effector2 = new BasicTransform(null);
                                    effector2.Position = part.transform.position;
                                    effector2.Rotation = part.transform.rotation;
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
                case State.Active:
                    if (configChanged)
                    {
                        SetupChain();
                    }


                    foreach (IKJoint3 ikJoint in joints)
                    {
                        ikJoint.Evaluate(effector, effector2, target);
                    }
                    break;
            }



        }

        private void OnRenderObject()
        {
            //foreach (IKJoint3 ikJoint in joints)
            //{
            //    Vector3 pos = ikJoint.transform.position;
            //    Vector3 axis = ikJoint.transform.rotation * ikJoint.axis * 0.5f;
            //    DrawTools.DrawLine(pos - axis, pos + axis, Color.red);
            //    Vector3 perp = ikJoint.transform.rotation * ikJoint.perpendicularAxis;
            //    DrawTools.DrawLine(pos, pos + perp, Color.yellow);
            //    Vector3 perpmoved = ikJoint.movingTransform.rotation * ikJoint.perpendicularAxis;
            //    DrawTools.DrawLine(pos, pos + perpmoved, Color.green);
            //}

            //if (effector != null)
            //{
            //    DrawTools.DrawTransform(effector);
            //}

            foreach (IKJoint3 ikJoint in joints)
            {
                Vector3 pos = ikJoint.baseTransform.Position;
                Vector3 axis = ikJoint.baseTransform.Rotation * ikJoint.axis * 0.5f;
                DrawTools.DrawLine(pos - axis, pos + axis, Color.red);
                Vector3 perp = ikJoint.baseTransform.Rotation * ikJoint.perpendicularAxis;
                DrawTools.DrawLine(pos, pos + perp, Color.yellow);
                Vector3 perpmoved = ikJoint.movingTransform2.Rotation * ikJoint.perpendicularAxis;
                DrawTools.DrawLine(pos, pos + perpmoved, Color.green);
            }

            if (effector != null)
            {
                DrawTools.DrawTransform(effector2);
            }
        }

        private GameObject InstantiateEffector(Part part)
        {
            GameObject effector = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effector.name = $"{part.partInfo.name} (IKEffector)";
            UnityEngine.Object.Destroy(effector.GetComponent<Collider>());
            effector.GetComponent<MeshRenderer>().material = IKJoint2.ServoMaterial;
            return effector;
        }
    }
}
