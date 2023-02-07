﻿/*
PAW :
- [lbl] Status : ready / tracking (target) / tracking (manual) / no effector / invalid effector / no servo 
> Servo configuration
  - [--servo selector--]
  - [btn] Remove servo
  - [btn] Add servo -> enter selection mode
  - [btn] Servo tracking mode : position <> rotation
> Effector configuration
  - [btn] Select effector -> enter selection mode
  - [-- effector node selector --] -> all nodes + "part center"
  - [-- effector offset dir selector --] -> up/down/left/right/forward/back
  - [-- effector offset slider --] -> float range
> Rest positions
  - [--rest pos selector--]
  - [btn] Go to selected
  - [btn] Delete selected
  - [btn] Add current as new
> Execution control
  - [btn] Keyboard control : enabled/disabled
  - [btn] Tracking : enabled/disabled [T] (if mode == manual, doesn't actually move the arm)
  - [btn] Control mode : Coordinates / Target  
  - [btn] Tracking mode : Continuous / Manual [M]
  - [btn] Execute [E] (only if mode == manual)
> Target control
    - [btn] Target : [GO/Part name] -> enter selection mode, if surface mode put pos/rot gizmo on GO surface point
    - [btn] Target mode : part / surface
    > (if part target)
      - [-- target node selector --] -> all nodes + "part center"
      - [-- target offset dir selector --] -> X/Y/Z
      - [-- target offset slider --] -> float range

> Coordinates control
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


using Expansions.Serenity;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyRobotics
{
    public class ModuleEasyRobotics : PartModule
    {
        private List<ServoJoint> ikChain = new List<ServoJoint>();
        private bool configurationIsValid = false;
        private ServoJoint uiSelectedServo;
        private Part effectorPart;
        private BasicTransform effector;
        private Transform targetObject;
        private Part targetPart;
        private BasicTransform target;
        private Vector3 targetObjectPosOffset;
        private Quaternion targetObjectRotOffset;

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
        private const string PAWGROUP_EXECUTION = "ikExecution";
        private const string PAWGROUP_TARGET = "ikTarget";

        private static string LOC_PAWGROUP_SERVOCONFIG = "IK Servo Configuration";
        private static string LOC_PAWGROUP_EFFECTORCONFIG = "IK Effector Configuration";
        private static string LOC_PAWGROUP_EXECUTION = "IK Execution Control";
        private static string LOC_PAWGROUP_TARGET = "IK Target";

        private static string[] _dirOptions;
        private static string[] DirOptions => 
            _dirOptions ?? (_dirOptions = new[] 
                { LOC_PAW_UP, LOC_PAW_DOWN, LOC_PAW_FORWARD, LOC_PAW_BACK, LOC_PAW_RIGHT, LOC_PAW_LEFT });
 
        [KSPField(guiActive = true, guiActiveEditor = true)]
        public string pawStatus;
        private static string LOC_PAW_STATUS = "IK Status";
        private static string LOC_PAW_STATUS_READY = "Ready";
        private static string LOC_PAW_STATUS_TRACKING = "Tracking";
        private static string LOC_PAW_STATUS_NOEFFECTOR = "No effector selected";
        private static string LOC_PAW_STATUS_NOSERVOS = "No servos selected";
        private static string LOC_PAW_STATUS_INVALID = "Invalid servo chain";
        private static string LOC_PAW_STATUS_NOTARGET = "No target selected";

        /// <summary> Servo selector, int value match servo list index </summary>
        [KSPField(guiActive = true, guiActiveEditor = true)]
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawSelectedServoIndex;
        private BaseField pawSelectedServoIndex_Field;
        private UI_ChooseOption pawSelectedServoIndex_UIControl;
        private static string LOC_PAW_SERVOSELECTOR = "Servo selector";
        private static string LOC_PAW_NONE = "None";

        /// <summary> Servo tracking mode position/rotation</summary>
        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawSelectedServoMode() => OnUISelectedServoModeToggle();
        private BaseEvent pawSelectedServoMode_Event;
        private static string LOC_PAW_SERVOMODE = "Servo tracking mode";
        private static string LOC_PAW_SERVOMODE_POSITION = "Position";
        private static string LOC_PAW_SERVOMODE_ROTATION = "Rotation";

        /// <summary> Remove currently selected servo from servos list </summary>
        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawSelectedServoRemove() => OnUISelectedServoRemove();
        private BaseEvent pawSelectedServoRemove_Event;
        private static string LOC_PAW_SERVOREMOVE = "Remove servo";

        /// <summary> Enter servo selection mode </summary>
        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawServoSelect() => OnUIAddServos();
        private static string LOC_PAW_SERVOSELECT = "Add servos";
        
        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawEffectorSelect() => OnUISelectEffector();
        private BaseEvent pawEffectorSelect_Event;
        private static string LOC_PAW_EFFECTORSELECT = "Effector";

        [KSPField(guiActive = true, guiActiveEditor = true)]
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawEffectorNode;
        private BaseField pawEffectorNode_Field;
        private UI_ChooseOption pawEffectorNode_UIControl;
        private static string LOC_PAW_EFFECTORNODE = "Effector node";
        private static string LOC_PAW_DOCKINGNODE = "Docking node";
        private static string LOC_PAW_GRAPPLENODE = "Grapple node";
        private AttachNode virtualEffectorDockingNode;

        [KSPField(guiActive = true, guiActiveEditor = true)]
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawEffectorDir;
        private BaseField pawEffectorDir_Field;
        private UI_ChooseOption pawEffectorDir_UIControl;
        private static string LOC_PAW_EFFECTORDIR = "Effector direction";
        private static string LOC_PAW_UP = "UP";
        private static string LOC_PAW_DOWN = "DOWN";
        private static string LOC_PAW_FORWARD = "FORWARD";
        private static string LOC_PAW_BACK = "BACK";
        private static string LOC_PAW_RIGHT = "RIGHT";
        private static string LOC_PAW_LEFT = "LEFT";
        
        [KSPField(guiActive = true, guiActiveEditor = true, guiUnits = "m")]
        [UI_FloatRange(affectSymCounterparts = UI_Scene.None, minValue = 0f, maxValue = 2f, stepIncrement = 0.05f)]
        public float pawEffectorOffset;
        private BaseField pawEffectorOffset_Field;
        private static string LOC_PAW_EFFECTOROFFSET = "Effector offset";

        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawTrackingControlMode() => OnUIToggleTrackingControlMode();
        private BaseEvent pawTrackingControlMode_Event;
        private static string LOC_PAW_CONTROLMODE = "Control mode";
        private static string LOC_PAW_COORDS = "Coordinates";
        private static string LOC_PAW_TARGET = "Target";

        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawTrackingMode() => OnUIToggleTrackingMode();
        private BaseEvent pawTrackingMode_Event;
        private static string LOC_PAW_TRACKINGMODE = "Tracking mode";
        private static string LOC_PAW_CONTINOUS = "Continous";
        private static string LOC_PAW_MANUAL = "Manual";

        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawTrackingEnable() => OnUIToggleTracking();
        private BaseEvent pawTrackingEnable_Event;
        private static string LOC_PAW_TRACKING = "Tracking";
        private static string LOC_PAW_ENABLED = "Enabled";
        private static string LOC_PAW_DISABLED = "Disabled";

        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawTrackingManualExecute() => OnUIManualModeExecute();
        private BaseEvent pawTrackingManualExecute_Event;
        private static string LOC_PAW_EXECUTE = "Execute";

        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawTargetSelect() => OnUISelectTarget();
        private BaseEvent pawTargetSelect_Event;

        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawTargetMode() => OnUIToggleTargetMode();
        private BaseEvent pawTargetMode_Event;
        private static string LOC_PAW_TARGETMODE = "Target mode";
        private static string LOC_PAW_PART = "Part";
        private static string LOC_PAW_SURFACE = "Surface";

        [KSPField(guiActive = true, guiActiveEditor = true)]
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawTargetNode;
        private BaseField pawTargetNode_Field;
        private UI_ChooseOption pawTargetNode_UIControl;
        private static string LOC_PAW_TARGETNODE = "Target node";
        private AttachNode virtualTargetDockingNode;

        [KSPField(guiActive = true, guiActiveEditor = true)]
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawTargetDir;
        private BaseField pawTargetDir_Field;
        private UI_ChooseOption pawTargetDir_UIControl;
        private static string LOC_PAW_TARGETDIR = "Target direction";

        [KSPField(guiActive = true, guiActiveEditor = true, guiUnits = "m")]
        [UI_FloatRange(affectSymCounterparts = UI_Scene.None, minValue = 0f, maxValue = 2f, stepIncrement = 0.05f)]
        public float pawTargetOffset;
        private BaseField pawTargetOffset_Field;
        private static string LOC_PAW_TARGETOFFSET = "Target offset";

        [KSPField(isPersistant = true)]
        public bool trackingEnabled;

        [KSPField(isPersistant = true)]
        public ControlMode controlMode;
        public enum ControlMode { Target, Coordinates }

        [KSPField(isPersistant = true)]
        public TrackingMode trackingMode;
        public enum TrackingMode { Continuous, Manual }

        [KSPField(isPersistant = true)]
        public TargetMode targetMode;
        public enum TargetMode { Part, Surface }


        public override void OnAwake()
        {
            bool isEditor = HighLogic.LoadedSceneIsEditor;
            BasePAWGroup servoGroup = new BasePAWGroup(PAWGROUP_SERVOCONFIG, LOC_PAWGROUP_SERVOCONFIG, !isEditor);
            BasePAWGroup effectorGroup = new BasePAWGroup(PAWGROUP_EFFECTORCONFIG, LOC_PAWGROUP_EFFECTORCONFIG, !isEditor);
            BasePAWGroup executionGroup = new BasePAWGroup(PAWGROUP_EXECUTION, LOC_PAWGROUP_EXECUTION, isEditor);
            BasePAWGroup targetGroup = new BasePAWGroup(PAWGROUP_TARGET, LOC_PAWGROUP_TARGET, isEditor);

            foreach (BaseField baseField in Fields)
            {
                switch (baseField.name)
                {
                    case nameof(pawStatus):
                        baseField.guiName = LOC_PAW_STATUS;
                        break;
                    case nameof(pawSelectedServoIndex):
                        baseField.group = servoGroup;
                        baseField.guiName = LOC_PAW_SERVOSELECTOR;
                        baseField.OnValueModified += OnUISelectServo;
                        pawSelectedServoIndex_Field = baseField;
                        pawSelectedServoIndex_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        break;
                    case nameof(pawEffectorNode):
                        baseField.group = effectorGroup;
                        baseField.guiName = LOC_PAW_EFFECTORNODE;
                        baseField.OnValueModified += OnUIEffectorPositionChanged;
                        pawEffectorNode_Field = baseField;
                        pawEffectorNode_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        break;
                    case nameof(pawEffectorDir):
                        baseField.group = effectorGroup;
                        baseField.guiName = LOC_PAW_EFFECTORDIR;
                        baseField.OnValueModified += OnUIEffectorPositionChanged;
                        pawEffectorDir_Field = baseField;
                        pawEffectorDir_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        pawEffectorDir_UIControl.options = DirOptions;
                        break;
                    case nameof(pawEffectorOffset):
                        baseField.group = effectorGroup;
                        baseField.guiName = LOC_PAW_EFFECTOROFFSET;
                        baseField.OnValueModified += OnUIEffectorPositionChanged;
                        pawEffectorOffset_Field = baseField;
                        break;
                    case nameof(pawTargetNode):
                        baseField.group = targetGroup;
                        baseField.guiName = LOC_PAW_TARGETNODE;
                        baseField.OnValueModified += OnUITargetPositionChanged;
                        pawTargetNode_Field = baseField;
                        pawTargetNode_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        break;
                    case nameof(pawTargetDir):
                        baseField.group = targetGroup;
                        baseField.guiName = LOC_PAW_TARGETDIR;
                        baseField.OnValueModified += OnUITargetPositionChanged;
                        pawTargetDir_Field = baseField;
                        pawTargetDir_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        pawTargetDir_UIControl.options = DirOptions;
                        break;
                    case nameof(pawTargetOffset):
                        baseField.group = targetGroup;
                        baseField.guiName = LOC_PAW_TARGETOFFSET;
                        baseField.OnValueModified += OnUITargetPositionChanged;
                        pawTargetOffset_Field = baseField;
                        break;
                }
            }

            foreach (BaseEvent baseEvent in Events)
            {
                switch (baseEvent.name)
                {
                    case nameof(pawSelectedServoMode):
                        baseEvent.group = servoGroup;
                        baseEvent.guiName = LOC_PAW_SERVOMODE;
                        pawSelectedServoMode_Event = baseEvent;
                        break;
                    case nameof(pawSelectedServoRemove):
                        baseEvent.group = servoGroup;
                        baseEvent.guiName = LOC_PAW_SERVOREMOVE;
                        pawSelectedServoRemove_Event = baseEvent;
                        break;
                    case nameof(pawServoSelect):
                        baseEvent.group = servoGroup;
                        baseEvent.guiName = LOC_PAW_SERVOSELECT;
                        break;
                    case nameof(pawEffectorSelect):
                        baseEvent.group = effectorGroup;
                        baseEvent.guiName = LOC_PAW_EFFECTORSELECT;
                        pawEffectorSelect_Event = baseEvent;
                        break;
                    case nameof(pawTrackingControlMode):
                        baseEvent.group = executionGroup;
                        baseEvent.guiName = LOC_PAW_CONTROLMODE;
                        pawTrackingControlMode_Event = baseEvent;
                        break;
                    case nameof(pawTrackingMode):
                        baseEvent.group = executionGroup;
                        baseEvent.guiName = LOC_PAW_TRACKINGMODE;
                        pawTrackingMode_Event = baseEvent;
                        break;
                    case nameof(pawTrackingEnable):
                        baseEvent.group = executionGroup;
                        baseEvent.guiName = LOC_PAW_TRACKING;
                        pawTrackingEnable_Event = baseEvent;
                        break;
                    case nameof(pawTrackingManualExecute):
                        baseEvent.group = executionGroup;
                        baseEvent.guiName = LOC_PAW_EXECUTE;
                        pawTrackingManualExecute_Event = baseEvent;
                        break;
                    case nameof(pawTargetSelect):
                        baseEvent.group = targetGroup;
                        pawTargetSelect_Event = baseEvent;
                        break;
                    case nameof(pawTargetMode):
                        baseEvent.group = targetGroup;
                        pawTargetMode_Event = baseEvent;
                        break;
                }
            }
        }


        public override void OnStart(StartState state)
        {
            OnTargetPartChanged();
            OnEffectorPartChanged(out _);
            OnJointListChanged(true, out _);
            OnTrackingControlModeChanged();
            OnTrackingModeChanged();
            OnTrackingToggled(out _);
            OnTargetModeChanged();
            OnTargetPositionChanged();
        }


        private void OnUISelectServo(object obj)
        {
            uiSelectedServo?.servo.part.Highlight(false);

            if (ikChain.Count == 0)
                return;

            uiSelectedServo = ikChain[pawSelectedServoIndex];
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
            int previousCount = ikChain.Count;
            if (previousCount == 0)
                return;

            ikChain.RemoveAt(pawSelectedServoIndex);
            OnJointListChanged(previousCount > 0, out string error);

            if (error != null && effectorPart.IsNotNullOrDestroyed())
                PostScreenMessage(error, Color.red);
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

        private void OnUIToggleTrackingControlMode()
        {
            controlMode = controlMode == ControlMode.Coordinates ? ControlMode.Target : ControlMode.Coordinates;
            OnTrackingControlModeChanged();
        }

        private void OnUIToggleTrackingMode()
        {
            trackingMode = trackingMode == TrackingMode.Continuous ? TrackingMode.Manual : TrackingMode.Continuous;
            OnTrackingModeChanged();
        }

        private void OnUIToggleTracking()
        {
            trackingEnabled = !trackingEnabled;
            OnTrackingToggled(out string error);
            if (error != null)
                PostScreenMessage(error, Color.red);
        }

        private void OnUIManualModeExecute()
        {

        }

        private void OnUISelectTarget()
        {
            SelectionModeEnter(targetMode == TargetMode.Part ? SelectionMode.SelectPartTarget : SelectionMode.SelectSurfaceTarget);
        }

        private void OnUIToggleTargetMode()
        {
            targetMode = targetMode == TargetMode.Part ? TargetMode.Surface : TargetMode.Part;
            OnTargetModeChanged();
        }

        private void OnUITargetPositionChanged(object newVal)
        {
            OnTargetPositionChanged();
        }

        private void OnTrackingControlModeChanged()
        {
            switch (controlMode)
            {
                case ControlMode.Target:
                    pawTrackingControlMode_Event.guiName = $"{LOC_PAW_CONTROLMODE}: {LOC_PAW_TARGET}";
                    break;
                case ControlMode.Coordinates:
                    pawTrackingControlMode_Event.guiName = $"{LOC_PAW_CONTROLMODE}: {LOC_PAW_COORDS}";
                    break;
            }

            OnStatusChanged();
        }

        private void OnTrackingModeChanged()
        {
            switch (trackingMode)
            {
                case TrackingMode.Continuous:
                    pawTrackingMode_Event.guiName = $"{LOC_PAW_TRACKINGMODE}: {LOC_PAW_CONTINOUS}";
                    pawTrackingManualExecute_Event.guiActive = false;
                    pawTrackingManualExecute_Event.guiActiveEditor = false;
                    break;
                case TrackingMode.Manual:
                    pawTrackingMode_Event.guiName = $"{LOC_PAW_TRACKINGMODE}: {LOC_PAW_MANUAL}";
                    pawTrackingManualExecute_Event.guiActive = true;
                    pawTrackingManualExecute_Event.guiActiveEditor = true;
                    break;
            }

            OnStatusChanged();
        }

        private void OnTargetModeChanged()
        {
            switch (targetMode)
            {
                case TargetMode.Part:
                    pawTargetMode_Event.guiName = $"{LOC_PAW_TARGETMODE}: {LOC_PAW_PART}";
                    break;
                case TargetMode.Surface:
                    pawTargetMode_Event.guiName = $"{LOC_PAW_TARGETMODE}: {LOC_PAW_SURFACE}";
                    break;
            }
        }

        private void OnTrackingToggled(out string error)
        {
            if (trackingEnabled)
            {
                trackingEnabled = ConfigureIKChain(out error, out bool ikJointListChanged);
                if (ikJointListChanged)
                    UpdatePAWJointList();
            }
            else
            {
                error = null;
            }

            switch (trackingEnabled)
            {
                case true:
                    pawTrackingEnable_Event.guiName = $"{LOC_PAW_TRACKING}: {LOC_PAW_ENABLED}";
                    break;
                case false:
                    pawTrackingEnable_Event.guiName = $"{LOC_PAW_TRACKING}: {LOC_PAW_DISABLED}";
                    break;
            }

            OnStatusChanged();
        }

        private void OnSelectedServoModeChanged()
        {
            pawSelectedServoMode_Event.guiName = 
                $"{LOC_PAW_SERVOMODE}: {(uiSelectedServo.rotateToDirection ? LOC_PAW_SERVOMODE_ROTATION : LOC_PAW_SERVOMODE_POSITION)}";
        }

        private void OnJointListChanged(bool forcePAWUpdate, out string error)
        {
            if (trackingEnabled)
            {
                trackingEnabled = false;
                OnTrackingToggled(out _);
            }

            configurationIsValid = false;

            ConfigureIKChain(out error, out bool ikJointListChanged);

            if (forcePAWUpdate || ikJointListChanged)
                UpdatePAWJointList();

            OnStatusChanged();
        }

        private void UpdatePAWJointList()
        {
            int jointCount = ikChain.Count;
            string[] selectionOptions;
            int selectedIndex;

            if (jointCount == 0)
            {
                selectedIndex = 0;
                selectionOptions = new[] { LOC_PAW_NONE };

                pawSelectedServoIndex_Field.SetGUIActive(false);
                pawSelectedServoMode_Event.SetGUIActive(false);
                pawSelectedServoRemove_Event.SetGUIActive(false);
            }
            else
            {
                selectedIndex = Math.Min(pawSelectedServoIndex, jointCount - 1);
                selectionOptions = new string[jointCount];
                for (int i = 0; i < selectionOptions.Length; i++)
                    selectionOptions[i] = ikChain[i].servo.part.partInfo.title;

                pawSelectedServoIndex_Field.SetGUIActive(true);
                pawSelectedServoMode_Event.SetGUIActive(true);
                pawSelectedServoRemove_Event.SetGUIActive(true);
            }

            pawSelectedServoIndex_UIControl.options = selectionOptions;
            pawSelectedServoIndex_Field.SetValue(selectedIndex, this);

            if (part.PartActionWindow != null && part.PartActionWindow.isActiveAndEnabled)
                part.PartActionWindow.displayDirty = true;
        }

        private void OnEffectorPartChanged(out string error)
        {
            if (trackingEnabled)
            {
                trackingEnabled = false;
                OnTrackingToggled(out _);
            }

            pawEffectorOffset = 0f;

            if (effectorPart.IsNullOrDestroyed())
            {
                pawEffectorNode_Field.SetGUIActive(false);
                pawEffectorDir_Field.SetGUIActive(false);
                pawEffectorOffset_Field.SetGUIActive(false);

                if (effector != null)
                {
                    effector.SetParent(null);
                    effector = null;
                }
            }
            else
            {
                effector = new BasicTransform(null);

                pawEffectorNode_Field.SetGUIActive(true);
                pawEffectorDir_Field.SetGUIActive(true);
                pawEffectorOffset_Field.SetGUIActive(true);

                virtualEffectorDockingNode = GetVirtualAttachNodeForDockingOrGrappleNode(effectorPart);

                int optionCount = effectorPart.attachNodes.Count + (virtualEffectorDockingNode == null ? 1 : 2);
                string[] nodeNames = new string[optionCount];
                nodeNames[0] = LOC_PAW_NONE;
                int selectedNode = 0;
                for (int i = 0; i < effectorPart.attachNodes.Count; i++)
                {
                    AttachNode attachNode = effectorPart.attachNodes[i];
                    nodeNames[i + 1] = attachNode.id;
                    if (selectedNode == 0 && attachNode.attachedPart == null)
                        selectedNode = i + 1;
                }

                if (virtualEffectorDockingNode != null)
                {
                    selectedNode = optionCount - 1;
                    nodeNames[selectedNode] = virtualEffectorDockingNode.id;
                }

                pawEffectorNode_UIControl.options = nodeNames;
                pawEffectorNode_Field.SetValue(selectedNode, this);
            }

            pawEffectorSelect_Event.guiName = $"{LOC_PAW_EFFECTORSELECT}{(effectorPart != null ? $": {effectorPart.partInfo.title}" : string.Empty)}";

            if (part.PartActionWindow != null && part.PartActionWindow.isActiveAndEnabled)
                part.PartActionWindow.displayDirty = true;

            configurationIsValid = false;

            ConfigureIKChain(out error, out bool ikJointListChanged);

            if (ikJointListChanged)
                UpdatePAWJointList();

            OnStatusChanged();
        }

        private void OnTargetPartChanged()
        {
            pawTargetOffset = 0f;

            if (targetPart.IsNullOrDestroyed())
            {
                pawTargetNode_Field.SetGUIActive(false);
                pawTargetDir_Field.SetGUIActive(false);
                pawTargetOffset_Field.SetGUIActive(false);
                if (target != null)
                {
                    target.SetParent(null);
                    target = null;
                }
                targetObject = null;
            }
            else
            {
                target = new BasicTransform(null);
                targetObject = targetPart.transform;
                pawTargetNode_Field.SetGUIActive(true);
                pawTargetDir_Field.SetGUIActive(true);
                pawTargetOffset_Field.SetGUIActive(true);

                virtualTargetDockingNode = GetVirtualAttachNodeForDockingOrGrappleNode(targetPart);

                int optionCount = targetPart.attachNodes.Count + (virtualTargetDockingNode == null ? 1 : 2);
                string[] nodeNames = new string[optionCount];
                nodeNames[0] = LOC_PAW_NONE;
                int selectedNode = 0;
                for (int i = 0; i < targetPart.attachNodes.Count; i++)
                {
                    AttachNode attachNode = targetPart.attachNodes[i];
                    nodeNames[i + 1] = attachNode.id;
                    if (selectedNode == 0 && attachNode.attachedPart == null)
                        selectedNode = i + 1;
                }

                if (virtualTargetDockingNode != null)
                {
                    selectedNode = optionCount - 1;
                    nodeNames[selectedNode] = virtualTargetDockingNode.id;
                }

                pawTargetNode_UIControl.options = nodeNames;
                pawTargetNode_Field.SetValue(selectedNode, this);
            }

            pawTargetSelect_Event.guiName = $"{LOC_PAW_TARGET}{(targetPart.IsNotNullOrDestroyed() ? $": {targetPart.partInfo.title}" : string.Empty)}";

            if (part.PartActionWindow != null && part.PartActionWindow.isActiveAndEnabled)
                part.PartActionWindow.displayDirty = true;

            OnStatusChanged();
        }

        private static AttachNode GetVirtualAttachNodeForDockingOrGrappleNode(Part part)
        {
            AttachNode attachNode = null;
            for (int i = part.modules.Count; i-- > 0;)
            {
                PartModule pm = part.modules[i];
                if (pm is ModuleDockingNode dockingNode)
                {
                    attachNode = new AttachNode()
                    {
                        id = LOC_PAW_DOCKINGNODE,
                        nodeTransform = dockingNode.nodeTransform
                    };
                    break;
                }
                else if (pm is ModuleGrappleNode grappleNode)
                {
                    attachNode = new AttachNode()
                    {
                        id = LOC_PAW_GRAPPLENODE,
                        nodeTransform = grappleNode.nodeTransform
                    };
                    break;
                }
            }

            if (attachNode != null)
            {
                attachNode.position = part.transform.InverseTransformPoint(attachNode.nodeTransform.position);
                attachNode.orientation = part.transform.InverseTransformDirection(attachNode.nodeTransform.forward);
                attachNode.nodeTransform = null;
            }

            return attachNode;
        }

        private void OnEffectorPositionChanged()
        {
            if (effector == null)
                return;

            Vector3 effectorPos = effectorPart.transform.position;
            Quaternion effectorRot = effectorPart.transform.rotation;

            if (pawEffectorNode > 0)
            {
                int i = pawEffectorNode - 1;
                AttachNode node;
                if (virtualEffectorDockingNode != null && i == effectorPart.attachNodes.Count)
                    node = virtualEffectorDockingNode;
                else
                    node = effectorPart.attachNodes[i];

                effectorPos += effectorRot * node.position;
                effectorRot *= Quaternion.FromToRotation(Vector3.up, node.orientation);
            }

            effectorRot = RotateByDirIndex(effectorRot, pawEffectorDir);

            if (pawEffectorOffset > 0f)
                effectorPos += effectorRot * (Vector3.up * pawEffectorOffset);

            effector.SetPosAndRot(effectorPos, effectorRot);
        }

        private void OnTargetPositionChanged()
        {
            if (target == null)
                return;

            Vector3 targetPos = targetObject.position;
            Quaternion targetRot = targetObject.rotation;

            if (targetMode == TargetMode.Part)
            {
                if (pawTargetNode > 0)
                {
                    int i = pawTargetNode - 1;
                    AttachNode node;
                    if (virtualTargetDockingNode != null && i == targetPart.attachNodes.Count)
                        node = virtualTargetDockingNode;
                    else
                        node = targetPart.attachNodes[i];

                    targetPos += targetRot * node.position;
                    targetRot *= Quaternion.FromToRotation(Vector3.up, node.orientation);
                }

                targetRot = RotateByDirIndex(targetRot, pawTargetDir);

                if (pawTargetOffset > 0f)
                    targetPos += targetRot * (Vector3.up * pawTargetOffset);
            }

            target.SetPosAndRot(targetPos, targetRot);
        }

        private static Quaternion upToDown = Quaternion.FromToRotation(Vector3.up, Vector3.down);
        private static Quaternion upToForward = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
        private static Quaternion upToBack = Quaternion.FromToRotation(Vector3.up, Vector3.back);
        private static Quaternion upToRight = Quaternion.FromToRotation(Vector3.up, Vector3.right);
        private static Quaternion upToLeft = Quaternion.FromToRotation(Vector3.up, Vector3.left);

        private static Quaternion RotateByDirIndex(Quaternion initialRotation, int dirIndex)
        {
            switch (dirIndex)
            {
                case 1: initialRotation *= upToDown; break;
                case 2: initialRotation *= upToForward; break;
                case 3: initialRotation *= upToBack; break;
                case 4: initialRotation *= upToRight; break;
                case 5: initialRotation *= upToLeft; break;
            }

            return initialRotation;
        }

        private void OnStatusChanged()
        {
            if (ikChain.Count == 0)
            {
                pawStatus = LOC_PAW_STATUS_NOSERVOS;
            }
            else if (effectorPart.IsNullOrDestroyed())
            {
                pawStatus = LOC_PAW_STATUS_NOEFFECTOR;
            }
            else if (!configurationIsValid)
            {
                pawStatus = LOC_PAW_STATUS_INVALID;
            }
            else if (!trackingEnabled)
            {
                if (controlMode == ControlMode.Target && targetObject.IsNullOrDestroyed())
                    pawStatus = LOC_PAW_STATUS_NOTARGET;
                else
                    pawStatus = LOC_PAW_STATUS_READY;
            }
            else
            {
                pawStatus = LOC_PAW_STATUS_TRACKING;
                if (controlMode == ControlMode.Target)
                    pawStatus += $" ({LOC_PAW_TARGET})";
                else
                    pawStatus += $" ({LOC_PAW_COORDS})";

                if (trackingMode == TrackingMode.Continuous)
                    pawStatus += $" ({LOC_PAW_CONTINOUS})";
                else
                    pawStatus += $" ({LOC_PAW_MANUAL})";
            }
        }

        private static List<ServoJoint> lastIKChain = new List<ServoJoint>();
        private static Dictionary<Part, ServoJoint> jointDict = new Dictionary<Part, ServoJoint>();
        private static Stack<Part> partStack = new Stack<Part>();
        private enum Relation { Child, Parent }

        private bool ConfigureIKChain(out string error, out bool ikJointListChanged)
        {
            ikJointListChanged = false;

            if (effectorPart == null)
            {
                configurationIsValid = false;
                error = "No effector selected";
                return false;
            }

            if (ikChain.Count == 0)
            {
                configurationIsValid = false;
                error = "No servos selected";
                return false;
            }

            if (configurationIsValid)
            {
                error = null;
                return true;
            }

            DisableJointHierarchy();

            // put all joints in a dictionary
            jointDict.Clear();
            lastIKChain.Clear();
            int jointCount = ikChain.Count;
            for (int i = jointCount; i-- > 0;)
            {
                ServoJoint joint = ikChain[i];
                lastIKChain.Add(joint);
                jointDict.Add(joint.servo.part, joint);
            }

            // and clear the original list
            ikChain.Clear();

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
                    ikChain.Add(joint);

                    if (jointDict.Count == 0)
                        break;
                }

                nextParent = nextParent.parent;
            }

            // at least some servos are parents of the effector
            if (ikChain.Count > 0)
            {
                // but not all of them : abort
                if (jointDict.Count > 0)
                {
                    jointDict.Clear();
                    ikChain = lastIKChain;
                    lastIKChain.Clear();
                    configurationIsValid = false;
                    error = "Effector isn't at the end of the servo chain";
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
                        ikChain.Add(joint);

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
                    jointDict.Clear();
                    ikChain = lastIKChain;
                    lastIKChain.Clear();
                    configurationIsValid = false;
                    error = "Selected servos aren't in a continuous chain";
                    return false;
                }

                // effector is parent of servo chain
                effectorRelation = Relation.Parent;
            }

            // All joints ordered from effector to root, now :
            // - setup parent/child relationships
            // - check for servo inversion
            // - set servos and effector pos/rot
            for (int i = jointCount; i-- > 0;)
            {
                ServoJoint joint = ikChain[i];

                if (i > 0)
                    ikChain[i - 1].baseTransform.SetParent(joint.movingTransform);

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

            effector.SetParentKeepWorldPosAndRot(ikChain[0].movingTransform);

            for (int i = ikChain.Count; i-- > 0;)
            {
                if (ikChain[i] != lastIKChain[i])
                {
                    ikJointListChanged = true;
                    break;
                }
            }

            lastIKChain.Clear();
            configurationIsValid = true;
            error = null;
            return true;
        }

        private void DisableJointHierarchy()
        {
            effector?.SetParentKeepWorldPosAndRot(null);

            foreach (ServoJoint joint in ikChain)
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
        private string ControlLockID => _lockID ?? (_lockID = $"{nameof(ModuleEasyRobotics)}_{GetInstanceID()}");

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
                {
                    if (selectedPart == null)
                        break;

                    if (selectedPart == targetPart)
                        break;

                    targetPart = selectedPart;
                    OnTargetPartChanged();
                    PostScreenMessage($"<b>{selectedPart.partInfo.title}</b> selected as target", Color.yellow);
                    SelectionModeExit();
                        break;
                }
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
            int i = ikChain.Count;
            while (i-- > 0)
            {
                ServoJoint joint = ikChain[i];
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
            ikChain.Add(newJoint);
            OnJointListChanged(true, out string error);
            if (error != null && effectorPart.IsNotNullOrDestroyed())
                PostScreenMessage(error, Color.red);

            pawSelectedServoIndex_Field.SetValue(ikChain.Count - 1, this);
            message = $"Servo <b>{servo.part.partInfo.title}</b> added";
            return true;
        }

        private bool TryAddEffector(Part selectedPart, out string message)
        {
            int i = ikChain.Count;
            while (i-- > 0)
            {
                ServoJoint joint = ikChain[i];
                if (joint.servo.part == selectedPart)
                {
                    message = $"<b>{selectedPart.partInfo.title}</b> is a servo";
                    return false;
                }
            }

            effectorPart = selectedPart;
            OnEffectorPartChanged(out string error);
            if (error != null && ikChain.Count > 0)
                PostScreenMessage(error, Color.red);
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

            if (part.PartActionWindow.IsNotNullOrDestroyed() && part.PartActionWindow.isActiveAndEnabled)
            {
                if (uiSelectedServo != null)
                {
                    if (uiSelectedServo.servo.IsNullOrDestroyed())
                    {
                        uiSelectedServo = null;
                    }
                    else
                    {
                        uiSelectedServo.servo.part.Highlight(Color.red);
                    }
                }

                foreach (ServoJoint joint in ikChain)
                {
                    if (joint != uiSelectedServo)
                    {
                        joint.servo.part.Highlight(Color.yellow);
                    }
                }
            }
        }

        private void OnRenderObject()
        {
            for (int i = 0; i < ikChain.Count; i++)
            {
                ServoJoint ikJoint = ikChain[i];
                Vector3 pos = ikJoint.baseTransform.Position;
                Vector3 axis = ikJoint.baseTransform.Rotation * ikJoint.axis;
                DrawTools.DrawCircle(pos, axis, Color.red, 0.25f);
                Vector3 perp = ikJoint.baseTransform.Rotation * ikJoint.perpendicularAxis;
                DrawTools.DrawLine(pos, pos + perp * 0.25f, Color.yellow);

                if (configurationIsValid && i < ikChain.Count - 1)
                {
                    DrawTools.DrawLine(pos, ikChain[i + 1].baseTransform.Position, Color.blue);
                }
            }

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

            if (target != null)
            {
                Vector3 pos = target.Position;
                Quaternion rot = target.Rotation;
                Vector3 dir = rot * Vector3.up;
                DrawTools.DrawArrow(pos - dir, dir, Color.green);
                Vector3 forward = rot * new Vector3(0.0f, 0.0f, 0.25f);
                Vector3 right = rot * new Vector3(0.25f, 0.0f, 0.0f);
                DrawTools.DrawLine(pos + forward, pos - forward, Color.blue);
                DrawTools.DrawLine(pos + right, pos - right, Color.red);
            }
        }

        private void FixedUpdate()
        {
            if (!trackingEnabled)
                return;

            switch (controlMode)
            {
                case ControlMode.Target:
                    OnTargetPositionChanged();
                    foreach (ServoJoint servoJoint in ikChain)
                        servoJoint.Evaluate(effector, target);

                    break;
                case ControlMode.Coordinates:
                    break;
            }
        }
    }
}
