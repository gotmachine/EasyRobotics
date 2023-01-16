using EditorGizmos;
using Expansions.Serenity;
using System.Collections.Generic;
using UnityEngine;


// source : https://github.com/zalo/MathUtilities/tree/master/Assets/IK/CCDIK

// so the plan is :
// - allow to add servos
// - first servo is base
// - for each servo :
//   - get relative position of the joint pivot from the base
//   - instantiate a gameobject at this position, add the IKJoint component
// - user select effector. Raycast to the collider surface ?
// - user select target. Raycast to the collider surface ?


namespace EasyRobotics
{
    public class ModuleEasyRoboticsOld : PartModule
    {
        private enum State
        {
            Inactive,
            SelectTarget,
            SelectEffector,
            SelectServo,
            Active
        }

        private State state = State.Inactive;

        public List<IKJoint> jointTipToBase = new List<IKJoint>();

        public Part effectorPart;
        public Transform effector; // this is the "end point" of the arm
        
        public List<BaseServo> servos = new List<BaseServo>();

        public Transform target; //

        [KSPEvent(guiName = "Set target", active = true, guiActive = true, guiActiveEditor = true)]
        public void SetTarget()
        {
            state = State.SelectTarget;
            if (target == null)
                target = ArrowRenderer.Create().transform;
        }

        [KSPEvent(guiName = "Set effector", active = true, guiActive = true, guiActiveEditor = true)]
        public void SetEffector()
        {
            state = State.SelectEffector;
            //if (effector == null)
            //    effector = ArrowRenderer.Create().transform;
        }

        [KSPEvent(guiName = "Add servo", active = true, guiActive = true, guiActiveEditor = true)]
        public void AddServo()
        {
            state = State.SelectServo;
        }

        [KSPEvent(guiName = "Clear servos", active = true, guiActive = true, guiActiveEditor = true)]
        public void ClearServos()
        {
            foreach (IKJoint ikJoint in jointTipToBase)
            {
                ikJoint.gameObject.DestroyGameObject();
            }

            servos.Clear();
            jointTipToBase.Clear();
        }

        [KSPEvent(guiName = "SetupChain", active = true, guiActive = true, guiActiveEditor = true)]
        public void SetupChain()
        {
            Material servoMaterial = new Material(Shader.Find("KSP/Alpha/Translucent"));
            servoMaterial.SetColor("_Color", new Color(1f, 1f, 0f, 0.3f));

            foreach (BaseServo baseServo in servos)
            {
                GameObject jointObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                jointObject.name = baseServo.part.partInfo.name + " (IKJoint)";
                UnityEngine.Object.Destroy(jointObject.GetComponent<Collider>());
                jointObject.GetComponent<MeshRenderer>().material = servoMaterial;
                //jointObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);


                IKJoint ikJoint = jointObject.AddComponent<IKJoint>();
                ikJoint.Setup(baseServo);
                jointTipToBase.Add(ikJoint);
            }

            IKJoint root = null;

            foreach (IKJoint ikJoint in jointTipToBase)
            {
                Part part = ikJoint.servo.part;
                IKJoint parentJoint;
                do
                {
                    part = part.parent;
                    parentJoint = jointTipToBase.Find(p => p.servo.part == part);

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

            for (var i = jointTipToBase.Count - 1; i >= 0; i--)
            {
                var ikJoint = jointTipToBase[i];
                ikJoint.root = root;
                if (ikJoint.parent != null)
                {
                    ikJoint.transform.position = ikJoint.servo.part.orgPos - rootOrgPos;
                    ikJoint.transform.rotation = Quaternion.Inverse(rootOrgRot) * ikJoint.servo.part.orgRot;
                }

                ikJoint.GetAxis();
                //ikJoint.transform.rotation = Quaternion.identity;
            }

            List<IKJoint> copy = new List<IKJoint>();
            IKJoint parent = root;
            copy.Add(parent);
            jointTipToBase.Remove(parent);
            do
            {
                for (int i = jointTipToBase.Count - 1; i >= 0; i--)
                {
                    IKJoint ikJoint = jointTipToBase[i];
                    if (ikJoint.parent == parent)
                    {
                        jointTipToBase.RemoveAt(i);
                        copy.Insert(0, ikJoint);
                        parent = ikJoint;
                        break;
                    }
                }
            } while (jointTipToBase.Count > 0);

            jointTipToBase = copy;

            Material material = new Material(Shader.Find("KSP/Alpha/Translucent"));
            material.SetColor("_Color", new Color(1f, 0f, 1f, 0.3f));
            GameObject effectorObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effectorObject.name = effectorPart.partInfo.name + " (Effector)";
            UnityEngine.Object.Destroy(effectorObject.GetComponent<SphereCollider>());
            effectorObject.GetComponent<MeshRenderer>().material = material;

            effectorObject.transform.SetParent(jointTipToBase[0].transform);
            effectorObject.transform.position = effectorPart.orgPos - rootOrgPos;
            effector = effectorObject.transform;

            root.transform.position = root.servo.transform.position;

        }

        [KSPEvent(guiName = "Start tracking", active = true, guiActive = true, guiActiveEditor = true)]
        public void StartTracking()
        {
            state = State.Active;
        }

        [KSPEvent(guiName = "Stop tracking", active = true, guiActive = true, guiActiveEditor = true)]
        public void StopTracking()
        {
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
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        target.gameObject.SetActive(true);
                        target.position = hit.point;
                        target.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

                        if (Input.GetMouseButtonDown(1))
                        {
                            //GizmoOffset.Attach(target, Quaternion.identity, OnGizmoMove, OnGizmoMoved);
                            ScreenMessages.PostScreenMessage($"{hit.transform.gameObject.name} selected as target");
                            target.SetParent(hit.transform, true);
                            state = State.Inactive;
                        }
                    }
                    else
                    {
                        target.gameObject.SetActive(false);
                    }
                    break;
                }
                case State.SelectEffector:
                {
                    if (!Input.GetMouseButtonDown(1))
                        break;

                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        Part part = FlightGlobals.GetPartUpwardsCached(hit.transform.gameObject);
                        if (part != null)
                        {
                            effectorPart = part;
                            ScreenMessages.PostScreenMessage($"{part.partInfo.title} selected as effector");
                            state = State.Inactive;
                            break;
                        }
                    }

                    ScreenMessages.PostScreenMessage($"No selector selected");
                    state = State.Inactive;
                    break;
                    }
                case State.SelectServo:
                {
                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        state = State.Inactive;
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
                                    ScreenMessages.PostScreenMessage($"{part.partInfo.title} added to kinematics chain");
                                    break;
                                }
                            }
                        }
                    }

                    ScreenMessages.PostScreenMessage($"Selecting servos\n[ENTER] to select\n[ESC] to end", Time.deltaTime);
                    break;
                }
                case State.Active:
                    foreach (IKJoint ikJoint in jointTipToBase)
                    {
                        ikJoint.Evaluate(effector, target);
                    }
                    break;
            }
        }
    }

    public class ArrowRenderer
    {
        public static GameObject Create()
        {
            Material material = new Material(Shader.Find("KSP/Alpha/Translucent"));
            material.SetColor("_Color", new Color(1f, 1f, 0f, 0.5f));
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.Destroy(sphere.GetComponent<SphereCollider>());
            sphere.GetComponent<MeshRenderer>().material = material;
            sphere.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            GameObject arrow = new GameObject("Arrow");
            arrow.transform.SetParent(sphere.transform, false);
            MeshRenderer arrowRenderer = arrow.AddComponent<MeshRenderer>();
            Material arrowMaterial = new Material(Shader.Find("KSP/Alpha/Translucent"));
            arrowMaterial.SetColor("_Color", new Color(1f, 0f, 0f, 0.5f));
            arrowRenderer.material = arrowMaterial;
            MeshFilter arrowFilter = arrow.AddComponent<MeshFilter>();
            arrowFilter.sharedMesh = CreateCone(5, 0.5f, 1f);
            

            return sphere;
        }

        static Mesh CreateCone(int subdivisions, float radius, float height)
        {
            Mesh mesh = new Mesh();

            Vector3[] vertices = new Vector3[subdivisions + 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[(subdivisions * 2) * 3];

            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0f);
            for (int i = 0, n = subdivisions - 1; i < subdivisions; i++)
            {
                float ratio = (float)i / n;
                float r = ratio * (Mathf.PI * 2f);
                float x = Mathf.Cos(r) * radius;
                float z = Mathf.Sin(r) * radius;
                vertices[i + 1] = new Vector3(x, 0f, z);

                Debug.Log(ratio);
                uv[i + 1] = new Vector2(ratio, 0f);
            }
            vertices[subdivisions + 1] = new Vector3(0f, height, 0f);
            uv[subdivisions + 1] = new Vector2(0.5f, 1f);

            // construct bottom

            for (int i = 0, n = subdivisions - 1; i < n; i++)
            {
                int offset = i * 3;
                triangles[offset] = 0;
                triangles[offset + 1] = i + 1;
                triangles[offset + 2] = i + 2;
            }

            // construct sides

            int bottomOffset = subdivisions * 3;
            for (int i = 0, n = subdivisions - 1; i < n; i++)
            {
                int offset = i * 3 + bottomOffset;
                triangles[offset] = i + 1;
                triangles[offset + 1] = subdivisions + 1;
                triangles[offset + 2] = i + 2;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }
    }
}
