using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            ConfigNode effectorNode = node.GetNode("EFFECTOR");
            if (effectorNode != null)
            {
                uint craftID = 0;
                effectorNode.TryGetValue("craftId", ref craftID);
                effectorPart = FindPartByCraftId(craftID);
                effector = effectorPart.transform;
            }

            ConfigNode[] jointNodes = node.GetNodes("IKJOINT");

            int jointCount = jointNodes.Length;
            joints = new List<IKJoint>(jointCount);
            IKJoint root = null;
            for (int i = jointCount - 1; i >= 0; i--)
            {
                ConfigNode jointNode = jointNodes[i];
                foreach (ConfigNode.Value jointNodeValue in jointNode.values)
                {
                    if (jointNodeValue.name == "craftId")
                    {
                        uint craftId = uint.Parse(jointNodeValue.value);
                        Part servoPart = FindPartByCraftId(craftId);
                        BaseServo servo = servoPart.FindModuleImplementing<BaseServo>();
                        IKJoint ikJoint = InstantiateIKJoint(servo);

                        ikJoint.transform.position = ikJoint.servo.part.transform.position;
                        ikJoint.transform.rotation = ikJoint.servo.part.transform.rotation;

                        if (i == jointCount - 1)
                        {
                            root = ikJoint;
                        }
                        else
                        {
                            ikJoint.parent = joints[0];
                            ikJoint.transform.SetParent(ikJoint.parent.transform, true);
                            ikJoint.root = root;
                        }
                        ikJoint.GetAxis();
                        joints.Insert(0, ikJoint);
                    }
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
                        if (currentMessage == null)
                            currentMessage = ScreenMessages.PostScreenMessage($"Selecting effector\n[ENTER] to select\n[ESC] to end", float.MaxValue);

                        if (Input.GetKeyDown(KeyCode.Escape))
                        {
                            ScreenMessages.PostScreenMessage($"No effector selected");
                            effector = null;
                            effectorPart = null;
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
                                    effector = part.transform;
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
                        SetupIKChain();
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
                DrawTools.DrawTransform(ikJoint.transform);

                Vector3 pos = ikJoint.transform.position;
                Vector3 offset = ikJoint.transform.rotation * ikJoint.axis;
                DrawTools.DrawLine(pos - offset, pos + offset, Color.yellow);
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
                    root = ikJoint;
                }
            }

            Vector3 rootOrgPos = root.servo.part.orgPos;
            Quaternion rootOrgRot = root.servo.part.orgRot;

            for (var i = joints.Count - 1; i >= 0; i--)
            {
                var ikJoint = joints[i];
                ikJoint.root = root;
                if (ikJoint.parent != null)
                {
                    ikJoint.transform.position = ikJoint.servo.part.orgPos - rootOrgPos;
                    ikJoint.transform.rotation = Quaternion.Inverse(rootOrgRot) * ikJoint.servo.part.orgRot;
                }

                ikJoint.GetAxis();
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
            root.transform.position = root.servo.transform.position;
            root.transform.SetParent(root.servo.transform, true);
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
    }
}
