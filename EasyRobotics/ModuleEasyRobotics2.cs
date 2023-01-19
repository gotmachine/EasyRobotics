using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommNet.Network;
using Expansions.Serenity;
using UnityEngine;
using static ActiveJoint;
using static UnityEngine.GraphicsBuffer;

namespace EasyRobotics
{
    public class ModuleEasyRobotics : PartModule
    {
        private enum State
        {
            Inactive,
            SelectTarget,
            SelectEffector,
            SelectServo,
            Active
        }

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


        /// <summary>
        /// joints sorted from tip to root
        /// </summary>
        private List<IKJoint> joints = new List<IKJoint>();
        private List<BaseServo> servos = new List<BaseServo>();

        private State state = State.Inactive;

        private Part effectorPart;
        private Transform effector;
        private Transform target;
        private bool configChanged;

        private ConfigNode loadedConfig;

        public override void OnSave(ConfigNode node)
        {
            if (effectorPart != null)
            {
                ConfigNode effectorNode = node.AddNode("EFFECTOR");
                if (HighLogic.LoadedSceneIsEditor)
                    effectorNode.AddValue("craftId", effectorPart.craftID);
                else
                    effectorNode.AddValue("flightId", effectorPart.flightID);
            }

            for (int i = 0; i < joints.Count; i++)
            {
                IKJoint ikJoint = joints[i];
                ConfigNode jointNode = node.AddNode("IKJOINT");
                if (HighLogic.LoadedSceneIsEditor)
                    jointNode.AddValue("craftId", ikJoint.servo.part.craftID);
                else
                    jointNode.AddValue("flightId", ikJoint.servo.part.flightID);
            }
        }

        private Part FindPartByCraftId(uint craftID)
        {
            List<Part> parts = part.ship.parts;
            int i = parts.Count;
            while (i-- > 0)
                if (parts[i].craftID == craftID)
                    return parts[i];

            return null;
        }

        

        public override void OnLoad(ConfigNode node)
        {
            loadedConfig = node;
        }

        public override void OnStart(StartState state)
        {
            if (loadedConfig != null)
            {
                ConfigNode effectorNode = loadedConfig.GetNode("EFFECTOR");
                if (effectorNode != null)
                {
                    uint craftID = 0;
                    effectorNode.TryGetValue("craftId", ref craftID);
                    effectorPart = FindPartByCraftId(craftID);
                    effector = InstantiateEffector(effectorPart).transform;
                    effector.position = effectorPart.transform.position;
                    effector.rotation = effectorPart.transform.rotation;
                }

                ConfigNode[] jointNodes = loadedConfig.GetNodes("IKJOINT");

                int jointCount = jointNodes.Length;
                servos = new List<BaseServo>(jointCount);
                for (int i = jointCount - 1; i >= 0; i--)
                {
                    ConfigNode jointNode = jointNodes[i];
                    foreach (ConfigNode.Value jointNodeValue in jointNode.values)
                    {
                        if (jointNodeValue.name == "craftId")
                        {
                            uint craftId = uint.Parse(jointNodeValue.value);
                            Part servoPart = FindPartByCraftId(craftId);
                            servos.Add(servoPart.FindModuleImplementing<BaseServo>());
                        }
                    }
                }

                if (effector != null && servos.Count > 0)
                {
                    SetupChain();
                }
            }
        }


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
            foreach (IKJoint ikJoint in joints)
                ikJoint.gameObject.DestroyGameObject();

            servos.Clear();
            joints.Clear();

            state = State.SelectServo;
        }

        [KSPEvent(guiName = "Setup Chain", active = true, guiActive = true, guiActiveEditor = true)]
        public void SetupChain()
        {
            foreach (IKJoint ikJoint in joints)
                ikJoint.gameObject.DestroyGameObject();

            joints.Clear();
            SetupIKChain();
        }

        [KSPEvent(guiName = "Iterate", active = true, guiActive = true, guiActiveEditor = true)]
        public void Iterate()
        {
            foreach (IKJoint ikJoint in joints)
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

        private ScreenMessage currentMessage;

        private void QuitEditMode()
        {
            currentMessage.duration = 0f;
            currentMessage = null;
            state = State.Inactive;
        }

        private void Update()
        {
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


                    foreach (IKJoint ikJoint in joints)
                    {
                        ikJoint.Evaluate(effector, target);
                    }
                    break;
            }



        }

        private void OnRenderObject()
        {
            foreach (IKJoint ikJoint in joints)
            {
                //DrawTools.DrawTransform(ikJoint.transform);

                Vector3 pos = ikJoint.transform.position;

                //Vector3 axisOffset = ikJoint.transform.rotation * ikJoint.axis;
                //DrawTools.DrawArrow(pos - axisOffset, axisOffset * 2f, Color.red);

                //Vector3 parentAxisOffset = ikJoint.transform.parent.rotation * ikJoint.axis;
                //DrawTools.DrawArrow(pos - parentAxisOffset, parentAxisOffset * 2f, Color.blue);

                //Vector3 perpOffset = ikJoint.transform.rotation * ikJoint.perpendicularAxis;
                //DrawTools.DrawArrow(pos - perpOffset, perpOffset * 2f, Color.yellow);

                DrawTools.DrawArrow(pos - ikJoint.transform.up, ikJoint.transform.up * 2f, Color.red);
                DrawTools.DrawArrow(pos - ikJoint.transform.right, ikJoint.transform.right * 2f, Color.yellow);
            }
        }

        private void SetupIKChain()
        {
            configChanged = false;

            foreach (BaseServo baseServo in servos)
                joints.Add(InstantiateIKJoint(baseServo));

            IKJoint root = null;

            foreach (IKJoint ikJoint in joints)
            {
                Part part = ikJoint.servo.part;
                IKJoint parentJoint;
                do
                {
                    part = part.parent;
                    parentJoint = joints.Find(p => p.servo.part == part);

                } while (part != null && parentJoint == null);

                if (parentJoint != null)
                {
                    ikJoint.parent = parentJoint;
                    ikJoint.transform.SetParent(parentJoint.transform);
                }
                else
                {
                    ikJoint.transform.SetParent(ikJoint.servo.transform);
                    root = ikJoint;
                }
            }

            for (var i = joints.Count - 1; i >= 0; i--)
            {
                var ikJoint = joints[i];
                ikJoint.root = root;


                
            }

            List<IKJoint> copy = new List<IKJoint>();
            IKJoint parent = root;
            copy.Add(parent);
            joints.Remove(parent);
            do
            {
                for (int i = joints.Count - 1; i >= 0; i--)
                {
                    IKJoint ikJoint = joints[i];
                    if (ikJoint.parent == parent)
                    {
                        joints.RemoveAt(i);
                        copy.Insert(0, ikJoint);
                        parent = ikJoint;
                        break;
                    }
                }
            } while (joints.Count > 0);

            joints = copy;

            for (int i = joints.Count - 1; i >= 0; i--)
            {
                IKJoint ikJoint = joints[i];
                ikJoint.gameObject.name = $"IKJoint #{i + 1} ({ikJoint.servo.part.partInfo.name})";
                ikJoint.transform.position = ikJoint.servo.transform.position;
                ikJoint.transform.rotation = ikJoint.servo.transform.rotation;
                ikJoint.GetAxis();
            }

            effector.SetParent(joints[0].transform);
            effector.position = effectorPart.transform.position;
            effector.rotation = effectorPart.transform.rotation;
        }

        private IKJoint InstantiateIKJoint(BaseServo servo)
        {
            GameObject jointObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            jointObject.name = $"{servo.part.partInfo.name} (IKJoint)";
            UnityEngine.Object.Destroy(jointObject.GetComponent<Collider>());
            jointObject.GetComponent<MeshRenderer>().material = ServoMaterial;
            IKJoint ikJoint = jointObject.AddComponent<IKJoint>();
            ikJoint.Setup(servo);
            return ikJoint;
        }

        private GameObject InstantiateEffector(Part part)
        {
            GameObject effector = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effector.name = $"{part.partInfo.name} (IKEffector)";
            UnityEngine.Object.Destroy(effector.GetComponent<Collider>());
            effector.GetComponent<MeshRenderer>().material = ServoMaterial;
            return effector;
        }
    }
}
