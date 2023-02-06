/*
PAW :
- [lbl] Status : ready / tracking (target) / tracking (manual) / no effector / invalid effector / no servo 
> Configuration
  - [--servo selector--]
  - [btn] Remove servo
  - [btn] Add servo -> enter selection mode
  - [btn] Servo tracking mode : position <> rotation
> Configuration
  - [btn] Select effector -> enter selection mode
  - [-- effector node selector --] -> all nodes + "part center"
  - [-- effector offset dir selector --] -> up/down/left/right/forward/back
  - [-- effector offset slider --] -> float range
> Rest positions
  - [--rest pos selector--]
  - [btn] Go to selected
  - [btn] Delete selected
  - [btn] Add current as new
> Tracking
  - [btn] Keyboard control : enabled/disabled
  - [btn] Mode : continous/manual [M]
  - [btn] Tracking : enabled/disabled [T] (if mode = manual, doesn't actually move the arm)
  - [btn] Move to target [O] (only if mode = manual)
  - [btn] Control mode : manual / target
  > (if target)
    - [lbl] Target : [GO/Part name]
    - [btn] Select part target -> enter selection mode
    - [btn] Select surface target -> enter selection mode, put pos/rot gizmo on GO surface point
    > (if part target)
      - [-- target node selector --] -> all nodes + "part center"
      - [-- target offset dir selector --] -> X/Y/Z
      - [-- target offset slider --] -> float range
  > (if manual)
    - [btn] Reference frame : [Part name] -> enter selection mode
    - [-- linear range slider --] -> X/Y/Z min/max selection
    - [-- X slider --]
    - [-- Y slider --]
    - [-- Z slider --]
    - [-- roll slider --]
    - [-- pitch slider --]
    - [-- yaw slider --]

For selection mode, to ensure ESC key doesn't bring pause menu, lock ControlTypes.PAUSE
*/


using CommNet.Network;
using Expansions.Missions.Editor;
using Expansions.Serenity;
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;

namespace EasyRobotics
{
    public class ModuleEasyRobotics5 : PartModule
    {
        private List<ServoJoint> servos = new List<ServoJoint>();
        private bool servoChainIsDirty = true;
        private ServoJoint uiSelectedServo;
        private Part effectorPart;
        private BasicTransform effector;

        private enum SelectionMode
        {
            None,
            SelectServos,
            SelectEffector,
            SelectPartTarget,
            SelectSurfaceTarget,
            SelectReferenceFrame
        }

        private SelectionMode selectionMode;

        private const string PAWGROUP_SERVOCONFIG = "ikServoConfig";
        private const string PAWGROUP_EFFECTORCONFIG = "ikEffectorConfig";
        private const string PAWGROUP_RESTPOS = "ikRestPos";
        private const string PAWGROUP_TRACKING = "ikTracking";

        private static string LOC_PAWGROUP_SERVOCONFIG = "IK Servo Configuration";
        private static string LOC_PAWGROUP_EFFECTORCONFIG = "IK Effector Configuration";
        private static string LOC_PAWGROUP_RESTPOS = "IK Rest positions";
        private static string LOC_PAWGROUP_TRACKING = "IK Tracking";

        /// <summary> Servo selector, int value match servo list index </summary>
        [KSPField(guiActive = true, guiActiveEditor = true)]
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawConfigSelectedServoIndex;
        private BaseField pawConfigSelectedServoIndex_Field;
        private UI_ChooseOption pawConfigSelectedServoIndex_UIControl;
        private static string LOC_PAW_SERVOSELECTOR = "Servo selector";
        private static string LOC_PAW_NONE = "None";

        /// <summary> Servo tracking mode position/rotation</summary>
        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawConfigSelectedServoMode() => OnUISelectedServoModeToggle();
        private BaseEvent pawConfigSelectedServoMode_Event;
        private static string LOC_PAW_SERVOMODE = "Servo tracking mode";
        private static string LOC_PAW_SERVOMODE_POSITION = "Position";
        private static string LOC_PAW_SERVOMODE_ROTATION = "Rotation";

        /// <summary> Remove currently selected servo from servos list </summary>
        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawConfigSelectedServoRemove() => OnUISelectedServoRemove();
        private static string LOC_PAW_SERVOREMOVE = "Remove servo";

        /// <summary> Enter servo selection mode </summary>
        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawConfigServoSelect() => OnUIAddServos();
        private static string LOC_PAW_SERVOSELECT = "Add servos";
        
        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawConfigEffectorSelect() => OnUISelectEffector();
        private BaseEvent pawConfigEffectorSelect_Event;
        private static string LOC_PAW_EFFECTORSELECT = "Effector";

        [KSPField(guiActive = true, guiActiveEditor = true)]
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawConfigEffectorNode;
        private BaseField pawConfigEffectorNode_Field;
        private UI_ChooseOption pawConfigEffectorNode_UIControl;
        private static string LOC_PAW_EFFECTORNODE = "Effector node";

        [KSPField(guiActive = true, guiActiveEditor = true)]
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawConfigEffectorDir;
        private BaseField pawConfigEffectorDir_Field;
        private UI_ChooseOption pawConfigEffectorDir_UIControl;
        private static string LOC_PAW_EFFECTORDIR = "Effector direction";
        private static string LOC_PAW_UP = "up";
        private static string LOC_PAW_DOWN = "down";
        private static string LOC_PAW_FORWARD = "forward";
        private static string LOC_PAW_BACK = "back";
        private static string LOC_PAW_LEFT = "left";
        private static string LOC_PAW_RIGHT = "right";

        [KSPField(guiActive = true, guiActiveEditor = true, guiUnits = "m")]
        [UI_FloatRange(affectSymCounterparts = UI_Scene.None, minValue = 0f, maxValue = 2f, stepIncrement = 0.05f)]
        public float pawConfigEffectorOffset;
        private static string LOC_PAW_EFFECTOROFFSET = "Effector offset";

        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void StartTracking() => EnableJointHierarchy();

        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void StopTracking() => DisableJointHierarchy();



        [KSPField(isPersistant = true)]
        public TrackingMode trackingMode;

        public enum TrackingMode { Disabled, Target, Manual }



        public override void OnAwake()
        {
            bool isEditor = HighLogic.LoadedSceneIsEditor;
            BasePAWGroup servoGroup = new BasePAWGroup(PAWGROUP_SERVOCONFIG, LOC_PAWGROUP_SERVOCONFIG, !isEditor);
            BasePAWGroup effectorGroup = new BasePAWGroup(PAWGROUP_EFFECTORCONFIG, LOC_PAWGROUP_EFFECTORCONFIG, !isEditor);
            BasePAWGroup restPosGroup = new BasePAWGroup(PAWGROUP_RESTPOS, LOC_PAWGROUP_RESTPOS, false);
            BasePAWGroup trackingGroup = new BasePAWGroup(PAWGROUP_TRACKING, LOC_PAWGROUP_TRACKING, false);

            foreach (BaseField baseField in Fields)
            {
                switch (baseField.name)
                {
                    case nameof(pawConfigSelectedServoIndex):
                        baseField.group = servoGroup;
                        baseField.guiName = LOC_PAW_SERVOSELECTOR;
                        baseField.OnValueModified += OnUISelectServo;
                        pawConfigSelectedServoIndex_Field = baseField;
                        pawConfigSelectedServoIndex_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        break;
                    case nameof(pawConfigEffectorNode):
                        baseField.group = effectorGroup;
                        baseField.guiName = LOC_PAW_EFFECTORNODE;
                        baseField.OnValueModified += OnUIEffectorPositionChanged;
                        pawConfigEffectorNode_Field = baseField;
                        pawConfigEffectorNode_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        break;
                    case nameof(pawConfigEffectorDir):
                        baseField.group = effectorGroup;
                        baseField.guiName = LOC_PAW_EFFECTORDIR;
                        baseField.OnValueModified += OnUIEffectorPositionChanged;
                        pawConfigEffectorDir_Field = baseField;
                        pawConfigEffectorDir_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        pawConfigEffectorDir_UIControl.options =
                            new[] { LOC_PAW_UP, LOC_PAW_DOWN, LOC_PAW_FORWARD, LOC_PAW_BACK, LOC_PAW_LEFT, LOC_PAW_RIGHT };
                        break;
                    case nameof(pawConfigEffectorOffset):
                        baseField.group = effectorGroup;
                        baseField.guiName = LOC_PAW_EFFECTOROFFSET;
                        baseField.OnValueModified += OnUIEffectorPositionChanged;
                        break;
                }
            }

            foreach (BaseEvent baseEvent in Events)
            {
                switch (baseEvent.name)
                {
                    case nameof(pawConfigSelectedServoMode):
                        baseEvent.group = servoGroup;
                        baseEvent.guiName = LOC_PAW_SERVOMODE;
                        pawConfigSelectedServoMode_Event = baseEvent;
                        break;
                    case nameof(pawConfigSelectedServoRemove):
                        baseEvent.group = servoGroup;
                        baseEvent.guiName = LOC_PAW_SERVOREMOVE;
                        break;
                    case nameof(pawConfigServoSelect):
                        baseEvent.group = servoGroup;
                        baseEvent.guiName = LOC_PAW_SERVOSELECT;
                        break;
                    case nameof(pawConfigEffectorSelect):
                        baseEvent.group = effectorGroup;
                        baseEvent.guiName = LOC_PAW_EFFECTORSELECT;
                        pawConfigEffectorSelect_Event = baseEvent;
                        break;
                }
            }
        }

        public override void OnStart(StartState state)
        {

        }


        private void OnUISelectServo(object obj)
        {
            uiSelectedServo?.servo.part.Highlight(false);

            if (servos.Count == 0)
                return;

            uiSelectedServo = servos[pawConfigSelectedServoIndex];
            OnSelectedServoModeChanged();
        }

        private void OnUISelectedServoModeToggle()
        {
            if (uiSelectedServo == null)
                return;

            uiSelectedServo.rotateToDirection = !uiSelectedServo.rotateToDirection;
            OnSelectedServoModeChanged();
        }

        private void OnUISelectedServoRemove()
        {
            if (servos.Count == 0)
                return;

            servos.RemoveAt(pawConfigSelectedServoIndex);
            OnJointListChanged();
        }

        private void OnUIAddServos()
        {
            SelectionModeEnter(SelectionMode.SelectServos);
        }

        private void OnUISelectEffector()
        {
            SelectionModeEnter(SelectionMode.SelectEffector);
        }

        private void OnUIEffectorPositionChanged(object newVal)
        {
            OnEffectorPositionChanged();
        }

        private void OnSelectedServoModeChanged()
        {
            pawConfigSelectedServoMode_Event.guiName = 
                $"{LOC_PAW_SERVOMODE}: {(uiSelectedServo.rotateToDirection ? LOC_PAW_SERVOMODE_ROTATION : LOC_PAW_SERVOMODE_POSITION)}";
        }

        private void OnJointListChanged()
        {
            int jointCount = servos.Count;
            string[] selectionOptions;
            int selectedIndex;
            if (jointCount == 0)
            {
                selectedIndex = 0;
                selectionOptions = new[]{ LOC_PAW_NONE };
            }
            else
            {
                selectedIndex = Math.Min(pawConfigSelectedServoIndex, jointCount - 1);
                selectionOptions = new string[jointCount];
                for (int i = 0; i < selectionOptions.Length; i++)
                    selectionOptions[i] = servos[i].servo.part.partInfo.title;
            }

            pawConfigSelectedServoIndex_UIControl.options = selectionOptions;
            pawConfigSelectedServoIndex_Field.SetValue(selectedIndex, this);

            if (part.PartActionWindow != null && part.PartActionWindow.isActiveAndEnabled)
                part.PartActionWindow.displayDirty = true;

            servoChainIsDirty = true;
        }

        private void OnEffectorPartChanged()
        {
            pawConfigEffectorOffset = 0f;

            if (effectorPart == null)
            {
                pawConfigEffectorNode_Field.guiActive = false;
                pawConfigEffectorNode_Field.guiActiveEditor = false;
                effector.SetParent(null);
                effector = null;
            }
            else
            {

                effector = new BasicTransform(null);

                pawConfigEffectorNode_Field.guiActive = true;
                pawConfigEffectorNode_Field.guiActiveEditor = true;
                string[] nodeNames = new string[effectorPart.attachNodes.Count + 1];
                nodeNames[0] = LOC_PAW_NONE;
                int selectedNode = 0;
                for (int i = 0; i < effectorPart.attachNodes.Count; i++)
                {
                    AttachNode attachNode = effectorPart.attachNodes[i];
                    nodeNames[i + 1] = attachNode.id;
                    if (selectedNode == 0 && attachNode.attachedPart == null)
                        selectedNode = i + 1;
                }

                pawConfigEffectorNode_UIControl.options = nodeNames;
                pawConfigEffectorNode_Field.SetValue(selectedNode, this);

                OnEffectorPositionChanged();
            }

            pawConfigEffectorSelect_Event.guiName = $"{LOC_PAW_EFFECTORSELECT}{(effectorPart != null ? $": {effectorPart.partInfo.title}" : string.Empty)}";

            if (part.PartActionWindow != null && part.PartActionWindow.isActiveAndEnabled)
                part.PartActionWindow.displayDirty = true;

            servoChainIsDirty = true;
        }

        private void OnEffectorPositionChanged()
        {
            if (effector == null)
                return;

            
            Vector3 effectorPos = effectorPart.transform.position;
            Quaternion partRot = effectorPart.transform.rotation;
            Quaternion effectorRot = partRot;

            if (pawConfigEffectorNode > 0)
            {
                AttachNode node = effectorPart.attachNodes[pawConfigEffectorNode - 1];
                effectorPos += partRot * node.position;
                effectorRot *= Quaternion.FromToRotation(Vector3.up, node.orientation);
            }

            switch (pawConfigEffectorDir)
            {
                case 1:
                    effectorRot *= Quaternion.FromToRotation(Vector3.up, Vector3.down);
                    break;
                case 2:
                    effectorRot *= Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                    break;
                case 3:
                    effectorRot *= Quaternion.FromToRotation(Vector3.up, Vector3.back);
                    break;
                case 4:
                    effectorRot *= Quaternion.FromToRotation(Vector3.up, Vector3.left);
                    break;
                case 5:
                    effectorRot *= Quaternion.FromToRotation(Vector3.up, Vector3.right);
                    break;
            }

            if (pawConfigEffectorOffset > 0f)
                effectorPos += effectorRot * (Vector3.up * pawConfigEffectorOffset);

            effector.SetPosAndRot(effectorPos, effectorRot);
        }

        private static Dictionary<Part, ServoJoint> jointDict = new Dictionary<Part, ServoJoint>();
        private static Stack<Part> partStack = new Stack<Part>();

        private enum Relation { Child, Parent }

        private bool EnableJointHierarchy()
        {
            if (effectorPart == null)
            {
                PostScreenMessage("No effector selected", Color.red);
                return false;
            }

            if (servos.Count == 0)
            {
                PostScreenMessage("No servo selected", Color.red);
                return false;
            }

            DisableJointHierarchy();

            // put all joints in a dictionary
            jointDict.Clear();
            int jointCount = servos.Count;
            for (int i = jointCount; i-- > 0;)
            {
                ServoJoint joint = servos[i];
                jointDict.Add(joint.servo.part, joint);
            }

            // and clear the original list
            servos.Clear();

            Relation effectorRelation;

            // now we want to order the servo list following the part
            // hierarchy order, where the first servo is the closest from
            // the effector. There are three possibilities :
            // a. the servos are childs of the effector
            // b. the servos are parents of the effector
            // c. the effector is somewhere the middle, which is invalid

            // first try by assuming the servos are parents of the effector
            // as this is the cheapest to check and the most likely.
            Part nextParent = effectorPart.parent;
            while (nextParent != null)
            {
                if (jointDict.Remove(nextParent, out ServoJoint joint))
                {
                    servos.Add(joint);

                    if (jointDict.Count == 0)
                        break;
                }

                nextParent = nextParent.parent;
            }

            // at least some servos are parents of the effector
            if (servos.Count > 0)
            {
                // but not all of them : abort
                if (jointDict.Count > 0)
                {
                    // put everything back in the list
                    foreach (ServoJoint joint in jointDict.Values)
                        servos.Add(joint);

                    jointDict.Clear();

                    return false;
                }

                // effector is child of servo chain
                effectorRelation = Relation.Child;
            }
            // no servo is parent of the effector, so they must be childs
            else
            {
                // traverse the part hierarchy, depth first 
                partStack.Clear();
                partStack.Push(effectorPart);
                while (partStack.TryPop(out Part nextChild))
                {
                    if (jointDict.Remove(nextChild, out ServoJoint joint))
                    {
                        servos.Add(joint);

                        if (jointDict.Count == 0)
                            break;

                        // we want a single continous chain, so ignore sibling branches
                        partStack.Clear();
                    }

                    for (int i = nextChild.children.Count; i-- > 0;)
                        partStack.Push(nextChild.children[i]);
                }

                partStack.Clear();

                // if we didn't found all servos, this mean they aren't
                // in continous chain, so abort
                if (jointDict.Count > 0)
                {
                    foreach (ServoJoint joint in jointDict.Values)
                        servos.Add(joint);

                    jointDict.Clear();
                    return false;
                }

                // effector is parent of servo chain
                effectorRelation = Relation.Parent;
            }

            // All joints ordered from effector to root, we can setup parent/child relationships
            for (int i = jointCount; i-- > 0;)
            {
                ServoJoint joint = servos[i];

                if (i > 0)
                    servos[i - 1].baseTransform.SetParent(joint.movingTransform);

                // now check by which side each servo is attached
                bool parentIsAttachedToMovingPart = false;
                Part servoPart = joint.servo.part;
                Part parent = servoPart.parent;

                foreach (AttachNode node in joint.servo.attachNodes)
                {
                    if (node.attachedPart == parent)
                    {
                        parentIsAttachedToMovingPart = true;
                        break;
                    }
                }

                if (servoPart.srfAttachNode != null && servoPart.srfAttachNode.attachedPart == parent)
                {
                    foreach (string mesh in joint.servo.servoSrfMeshes)
                    {
                        if (mesh == servoPart.srfAttachNode.srfAttachMeshName)
                        {
                            parentIsAttachedToMovingPart = true;
                            break;
                        }
                    }
                }

                // - if the servo chain is ordered "downward" (with the effector as child) :
                //   - if the servo moving part is connected to the child, joint isn't inverted
                //   - if the servo moving part is connected to the parent, joint is inverted
                // - if the servo chain is ordered "upward" (with the effector as parent) :
                //   - if the servo moving part is connected to the child, joint is inverted
                //   - if the servo moving part is connected to the parent, joint isn't inverted
                joint.isInverted = effectorRelation == Relation.Child ? parentIsAttachedToMovingPart : !parentIsAttachedToMovingPart;

                joint.UpdateAxis();

                if (HighLogic.LoadedSceneIsEditor)
                    joint.SyncWithPartTransform();
                else
                    joint.SyncWithPartOrg();
            }

            effector.SetParent(servos[0].movingTransform);
            Transform effectorTransform = effectorPart.transform;
            effector.SetPosAndRot(effectorTransform.position, effectorTransform.rotation);

            return true;
        }

        private void DisableJointHierarchy()
        {
            effector?.SetParentKeepWorldPosAndRot(null);

            foreach (ServoJoint joint in servos)
            {
                joint.baseTransform.SetParentKeepWorldPosAndRot(null);
            }
        }

        private ScreenMessage selectionModeMessage;

        private static string LOC_SELECTMODE = "<b>[ENTER]</b> to select\n<b>[ESC]</b> to end";
        private static string LOC_SELECTMODE_SERVO = "Select servos parts to control";
        private static string LOC_SELECTMODE_EFFECTOR = "Select effector part";
        private static string LOC_SELECTMODE_TARGETPART = "Select target part";
        private static string LOC_SELECTMODE_TARGETOBJECT = "Select target object";
        private static string LOC_SELECTMODE_REFFRAME = "Select part to use as the reference frame in manual tracking mode";

        private void SelectionModeEnter(SelectionMode mode)
        {
            if (selectionMode != SelectionMode.None)
                return;

            selectionMode = mode;
            string message = string.Empty;
            switch (mode)
            {
                case SelectionMode.SelectServos:
                    message = $"{LOC_SELECTMODE_SERVO}\n{LOC_SELECTMODE}";
                    break;
                case SelectionMode.SelectEffector:
                    message = $"{LOC_SELECTMODE_EFFECTOR}\n{LOC_SELECTMODE}";
                    break;
                case SelectionMode.SelectPartTarget:
                    message = $"{LOC_SELECTMODE_TARGETPART}\n{LOC_SELECTMODE}";
                    break;
                case SelectionMode.SelectSurfaceTarget:
                    message = $"{LOC_SELECTMODE_TARGETOBJECT}\n{LOC_SELECTMODE}";
                    break;
                case SelectionMode.SelectReferenceFrame:
                    message = $"{LOC_SELECTMODE_REFFRAME}\n{LOC_SELECTMODE}";
                    break;

            }
            selectionModeMessage = ScreenMessages.PostScreenMessage(message, float.MaxValue);
            InputLockManager.SetControlLock(ControlTypes.PAUSE, ControlLockID);
        }

        private string _lockID;
        private string ControlLockID => _lockID ?? (_lockID = $"{nameof(ModuleEasyRobotics5)}_{GetInstanceID()}");

        private void SelectionModeExit()
        {
            if (selectionMode == SelectionMode.None)
                return;

            selectionModeMessage.duration = 0f;
            selectionModeMessage = null;
            InputLockManager.RemoveControlLock(ControlLockID);
            selectionMode = SelectionMode.None;
        }

        private void SelectionModeCheckInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SelectionModeExit();
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Return))
                return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
                return;

            Part selectedPart = FlightGlobals.GetPartUpwardsCached(hit.transform.gameObject);

            switch (selectionMode)
            {
                case SelectionMode.SelectServos:
                {
                    if (part == null)
                        break;

                    BaseServo servo = selectedPart.FindModuleImplementing<BaseServo>();
                    if (servo == null)
                    {
                        PostScreenMessage($"No servo on <b>{selectedPart.partInfo.title}</b>", Color.yellow);
                        break;
                    }

                    if (!TryAddServo(servo, out string message))
                    {
                        PostScreenMessage(message, Color.yellow);
                        break;
                    }

                    PostScreenMessage(message, Color.green);
                    break;
                }
                case SelectionMode.SelectEffector:
                {
                    if (selectedPart == null)
                        break;

                    if (selectedPart == effectorPart)
                        break;

                    if (!TryAddEffector(selectedPart, out string message))
                    {
                        PostScreenMessage(message, Color.yellow);
                        break;
                    }

                    PostScreenMessage(message, Color.green);
                    SelectionModeExit();
                    break;
                }
                case SelectionMode.SelectPartTarget:
                    if (selectedPart == null)
                        break;

                    break;
                case SelectionMode.SelectReferenceFrame:
                    if (selectedPart == null)
                        break;



                    break;

                case SelectionMode.SelectSurfaceTarget:
                    break;
            }

        }



        private bool TryAddServo(BaseServo servo, out string message)
        {
            int i = servos.Count;
            while (i-- > 0)
            {
                ServoJoint joint = servos[i];
                if (joint.servo == servo)
                {
                    message = $"Servo <b>{servo.part.partInfo.title}</b> is already added";
                    return false;
                }

                if (joint.servo.part == effectorPart)
                {
                    message = $"<b>{servo.part.partInfo.title}</b> is the effector";
                    return false;
                }
            }

            ServoJoint newJoint = new ServoJoint(servo);
            servos.Add(newJoint);
            OnJointListChanged();
            pawConfigSelectedServoIndex_Field.SetValue(servos.Count - 1, this);
            message = $"Servo <b>{servo.part.partInfo.title}</b> added";
            return true;
        }

        private bool TryAddEffector(Part selectedPart, out string message)
        {
            int i = servos.Count;
            while (i-- > 0)
            {
                ServoJoint joint = servos[i];
                if (joint.servo.part == selectedPart)
                {
                    message = $"<b>{selectedPart.partInfo.title}</b> is a servo";
                    return false;
                }
            }

            effectorPart = selectedPart;
            OnEffectorPartChanged();
            message = $"<b>{selectedPart.partInfo.title}</b> selected as effector";
            return true;
        }

        private static void PostScreenMessage(string message, Color color)
        {
            ScreenMessages.PostScreenMessage(message, 3f, ScreenMessageStyle.UPPER_CENTER, color);
        }

        private void Update()
        {
            if (selectionMode != SelectionMode.None)
                SelectionModeCheckInput();

            if (uiSelectedServo != null)
            {
                if (uiSelectedServo.servo == null)
                {
                    uiSelectedServo = null;
                }
                else
                {
                    uiSelectedServo.servo.part.Highlight(Color.red);
                }
            }

            foreach (ServoJoint joint in servos)
            {
                if (joint != uiSelectedServo)
                {
                    joint.servo.part.Highlight(Color.yellow);
                }
            }

            if (trackingMode == TrackingMode.Disabled)
            {
                if (HighLogic.LoadedSceneIsEditor)
                {
                    for (int i = 0; i < servos.Count; i++)
                        servos[i].SyncWithPartTransform();

                    if (effectorPart != null && effector != null)
                    {
                        OnEffectorPositionChanged();
                    }
                }
            }


        }

        private void OnRenderObject()
        {
            for (int i = 0; i < servos.Count; i++)
            {
                ServoJoint ikJoint = servos[i];
                Vector3 pos = ikJoint.baseTransform.Position;
                Vector3 axis = ikJoint.baseTransform.Rotation * ikJoint.axis;
                DrawTools.DrawCircle(pos, axis, Color.red, 0.25f);
                Vector3 perp = ikJoint.baseTransform.Rotation * ikJoint.perpendicularAxis;
                DrawTools.DrawLine(pos, pos + perp * 0.25f, Color.yellow);

                if (i < servos.Count - 1)
                {
                    DrawTools.DrawLine(pos, servos[i + 1].baseTransform.Position, Color.blue);
                }
            }


            //foreach (ServoJoint ikJoint in servos)
            //{
            //    Vector3 pos = ikJoint.baseTransform.Position;
            //    Vector3 axis = ikJoint.baseTransform.Rotation * ikJoint.axis * 0.5f;
            //    DrawTools.DrawLine(pos - axis, pos + axis, Color.red);
            //    Vector3 perp = ikJoint.baseTransform.Rotation * ikJoint.perpendicularAxis;
            //    DrawTools.DrawLine(pos, pos + perp, Color.yellow);
            //    Vector3 perpmoved = ikJoint.movingTransform.Rotation * ikJoint.perpendicularAxis;
            //    DrawTools.DrawLine(pos, pos + perpmoved, Color.green);
            //}

            if (effector != null)
            {
                Vector3 pos = effector.Position;
                Quaternion rot = effector.Rotation;
                Vector3 dir = rot * Vector3.up;
                DrawTools.DrawArrow(pos - dir, dir, Color.green);
                Vector3 forward = rot * new Vector3(0.0f, 0.0f, 0.25f);
                Vector3 right = rot * new Vector3(0.25f, 0.0f, 0.0f);
                DrawTools.DrawLine(pos + forward, pos - forward, Color.blue);
                DrawTools.DrawLine(pos + right, pos - right, Color.red);
            }
        }
    }
}
