using Expansions.Serenity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

// TODO :
// PRIORITY 1
// - finalize localization support
// - "help" button pointing to the wiki
// - Put together an user guide on the wiki
// PRIORITY 2
// - auto-binding / unbinding of axis groups to keyboard controls
// - auto-lock / auto-unlock servos ?
// PRIORITY 3
// - linear servos support
// - alternate poses checking (aka, get out of local minima)

namespace EasyRobotics
{
    public class ModuleEasyRobotics : PartModule, IShipConstructIDChanges
    {
        private bool persistedPartIdloading;
        private List<uint> persistedIkChainPartIds;
        private uint persistedEffectorPartId;
        private uint persistedTargetPartId;

        /// <summary>
        /// kinematic chain, from tip to root
        /// </summary>
        private List<ServoJoint> ikChain = new List<ServoJoint>();

        private bool configurationIsValid;
        private Part effectorPart;
        private BasicTransform effector;
        private Part targetPart;
        private BasicTransform target;
        private Vector3 targetObjectPosOffset;
        private Quaternion targetObjectRotOffset;

        private GameObject effectorGizmo;
        private GameObject targetGizmo;

        private enum SelectionMode
        {
            None,
            SelectServos,
            SelectEffector,
            SelectPartTarget
        }

        private SelectionMode selectionMode;

        private const string PAWGROUP_CONFIG = "ikConfig";
        private const string PAWGROUP_EXECUTION = "ikExecution";
        private const string PAWGROUP_TARGET = "ikTarget";
        private const string PAWGROUP_STOCKCONTROLLER = "roboticController";

        private static string LOC_PAWGROUP_CONFIG = "IK Configuration";
        private static string LOC_PAWGROUP_EXECUTION = "IK Execution Control";
        private static string LOC_PAWGROUP_TARGET = "IK Target";
        private static string AUTOLOC_PAWGROUP_STOCKCONTROLLER = "#autoLOC_6011075"; // "Robotic Controller"
        private static string LOC_PAW_NONE = "none";

        private static string[] _dirOptions;

        private static string[] DirOptions => _dirOptions ?? (_dirOptions = new[]
            { LOC_PAW_UP, LOC_PAW_DOWN, LOC_PAW_FORWARD, LOC_PAW_BACK, LOC_PAW_RIGHT, LOC_PAW_LEFT });

        private static string[] _trackingConstraintOptions;

        private static string[] TrackingConstraintOptions => _trackingConstraintOptions ?? (_trackingConstraintOptions = new[]
            { LOC_PAW_POSITION, LOC_PAW_POSANDDIR, LOC_PAW_POSANDROT });

        private static string[] _controlModeAllOptions;

        private static string[] ControlModeAllOptions => _controlModeAllOptions ?? (_controlModeAllOptions = new[]
            { LOC_PAW_CONTROLFREE, LOC_PAW_CONTROLTARGET });

        private static string[] _controlModeManualOption;

        private static string[] ControlModeManualOption => _controlModeManualOption ?? (_controlModeManualOption = new[]
            { LOC_PAW_CONTROLFREE });

        private static string[] _trackingModeOptions;

        private static string[] TrackingModeOptions => _trackingModeOptions ?? (_trackingModeOptions = new[]
            { LOC_PAW_CONTINOUS, LOC_PAW_ONREQUEST });

        [KSPField(guiActive = true, guiActiveEditor = true)] 
        [UI_Toggle(affectSymCounterparts = UI_Scene.None, enabledText = "", disabledText = "")]
        private bool pawServoSelect;
        private UI_Toggle pawServoSelect_UIControl;
        private static string LOC_PAW_SERVOSELECT = "Select servos";

        [KSPField(guiActive = true, guiActiveEditor = true)] 
        [UI_Toggle(affectSymCounterparts = UI_Scene.None)]
        private bool pawEffectorSelect;
        private UI_Toggle pawEffectorSelect_UIControl;
        private static string LOC_PAW_EFFECTORSELECT = "Effector";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true)] 
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawEffectorNode;
        private BaseField pawEffectorNode_Field;
        private UI_ChooseOption pawEffectorNode_UIControl;
        private static string LOC_PAW_EFFECTORNODE = "Effector node";
        private static string LOC_PAW_DOCKINGNODE = "Docking node";
        private static string LOC_PAW_GRAPPLENODE = "Grapple node";
        private AttachNode virtualEffectorDockingNode;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true)] 
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawEffectorDir;
        private BaseField pawEffectorDir_Field;
        private static string LOC_PAW_EFFECTORDIR = "Effector direction";
        private static string LOC_PAW_UP = "UP";
        private static string LOC_PAW_DOWN = "DOWN";
        private static string LOC_PAW_FORWARD = "FORWARD";
        private static string LOC_PAW_BACK = "BACK";
        private static string LOC_PAW_RIGHT = "RIGHT";
        private static string LOC_PAW_LEFT = "LEFT";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiUnits = "m")] 
        [UI_FloatRange(affectSymCounterparts = UI_Scene.None, minValue = 0f, maxValue = 2f, stepIncrement = 0.05f)]
        public float pawEffectorOffset;
        private BaseField pawEffectorOffset_Field;
        private static string LOC_PAW_EFFECTOROFFSET = "Effector offset";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true)] 
        [UI_Toggle(affectSymCounterparts = UI_Scene.None)]
        private bool pawServoGizmosEnabled = false;
        private static string LOC_PAW_SERVOGIZMOS = "Servo gizmos";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true)] 
        [UI_Toggle(affectSymCounterparts = UI_Scene.None)]
        private bool pawTrackingGizmosEnabled = true;
        private static string LOC_PAW_TRACKINGGIZMOS = "Target/effector gizmos";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiFormat = "P0")] 
        [UI_FloatRange(affectSymCounterparts = UI_Scene.None, minValue = 0.1f, maxValue = 2.5f, stepIncrement = 0.1f)]
        public float learningRateFactor = 1f;
        private static string LOC_PAW_LEARNINGRATE = "Learning rate";

        [KSPField(guiActive = true, guiActiveEditor = true)]
        public string pawStatus;
        private static string LOC_PAW_STATUS = "Status";
        private static string LOC_PAW_STATUS_READY = "Ready";
        private static string LOC_PAW_STATUS_TRACKING = "Tracking";
        private static string LOC_PAW_STATUS_NOEFFECTOR = "No effector selected";
        private static string LOC_PAW_STATUS_NOSERVOS = "No servos selected";
        private static string LOC_PAW_STATUS_INVALID = "Invalid servo chain";
        private static string LOC_PAW_STATUS_NOTARGET = "No target selected";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true)] 
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawControlMode;
        private BaseField pawControlMode_Field;
        private UI_ChooseOption pawControlMode_UIControl;
        private static string LOC_PAW_CONTROLMODE = "Control mode";
        private static string LOC_PAW_CONTROLFREE = "Free";
        private static string LOC_PAW_CONTROLTARGET = "Target";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true)] 
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawTrackingMode = (int)ExecutionMode.Continuous;
        private static string LOC_PAW_TRACKINGMODE = "Tracking mode";
        private static string LOC_PAW_CONTINOUS = "Continuous";
        private static string LOC_PAW_ONREQUEST = "On request";

        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawManualExecute() => OnUIManualModeExecute();
        private BaseEvent pawManualExecute_Event;
        private static string LOC_PAW_EXECUTE = "Request execution";


        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawResetOffsets() => OnUIResetPosRotOffsets();
        private static string LOC_PAW_RESETOFFSETS = "Reset pos/rot offsets";

        [KSPEvent(guiActive = true, guiActiveEditor = true)]
        private void pawResetToZero() => ResetToZero();
        private static string LOC_PAW_RESET = "Reset all servos positions";


        [KSPField(guiActive = true, guiActiveEditor = true)] 
        [UI_Toggle(affectSymCounterparts = UI_Scene.None)]
        private bool pawTargetSelect;
        private UI_Toggle pawTargetSelect_UIControl;
        private static string LOC_PAW_TARGET = "Target";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true)] 
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawTargetNode;
        private BaseField pawTargetNode_Field;
        private UI_ChooseOption pawTargetNode_UIControl;
        private static string LOC_PAW_TARGETNODE = "Target node";
        private AttachNode virtualTargetDockingNode;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true)]
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawTargetDir;
        private BaseField pawTargetDir_Field;
        private static string LOC_PAW_TARGETDIR = "Target direction";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true)] 
        [UI_ChooseOption(affectSymCounterparts = UI_Scene.None)]
        public int pawTrackingConstraint = (int)TrackingConstraint.PositionAndDirection;
        private static string LOC_PAW_CONSTRAINT = "Constraint";
        private static string LOC_PAW_POSITION = "Position";
        private static string LOC_PAW_POSANDDIR = "Pos+Direction";
        private static string LOC_PAW_POSANDROT = "Pos+Rotation";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiFormat = "0m;0m;Automatic")] 
        [UI_FloatRange(affectSymCounterparts = UI_Scene.None, minValue = 0f, maxValue = 100f, stepIncrement = 1f)]
        public float pawCoordinatesRange;
        private BaseField pawCoordinatesRange_Field;
        private static string LOC_PAW_COORDINATESRANGE = "Position range";

        [KSPAxisField(isPersistant = true, guiFormat = "0.00m", incrementalSpeed = 30f, axisMode = KSPAxisMode.Incremental, guiActive = true, guiActiveEditor = true)] 
        [UI_FloatRange(stepIncrement = 0.01f, affectSymCounterparts = UI_Scene.None)]
        public float pawCoordinatesX;
        private BaseAxisField pawCoordinatesX_Field;
        private UI_FloatRange pawCoordinatesX_UIControl;
        private static string LOC_PAW_MANUALX = "Right/left";

        [KSPAxisField(isPersistant = true, guiFormat = "0.00m", incrementalSpeed = 30f, axisMode = KSPAxisMode.Incremental, guiActive = true, guiActiveEditor = true)] 
        [UI_FloatRange(stepIncrement = 0.01f, affectSymCounterparts = UI_Scene.None)]
        public float pawCoordinatesY;
        private BaseAxisField pawCoordinatesY_Field;
        private UI_FloatRange pawCoordinatesY_UIControl;
        private static string LOC_PAW_MANUALY = "Up/down";

        [KSPAxisField(isPersistant = true, guiFormat = "0.00m", incrementalSpeed = 30f, axisMode = KSPAxisMode.Incremental, guiActive = true, guiActiveEditor = true)] 
        [UI_FloatRange(stepIncrement = 0.01f, affectSymCounterparts = UI_Scene.None)]
        public float pawCoordinatesZ;
        private BaseAxisField pawCoordinatesZ_Field;
        private UI_FloatRange pawCoordinatesZ_UIControl;
        private static string LOC_PAW_MANUALZ = "Forward/Back";

        [KSPAxisField(guiFormat = "0.0°", incrementalSpeed = 30f, minValue = -2.5f, maxValue = 2.5f, axisMode = KSPAxisMode.Incremental, guiActive = true, guiActiveEditor = true)] 
        [UI_FloatRange(stepIncrement = 0.0001f, minValue = -2.5f, maxValue = 2.5f, affectSymCounterparts = UI_Scene.None)]
        public float pawCoordinatesPitch;
        private BaseAxisField pawCoordinatesPitch_Field;
        private static string LOC_PAW_MANUALPITCH = "Pitch offset";

        [KSPAxisField(guiFormat = "0.0°", incrementalSpeed = 30f, minValue = -2.5f, maxValue = 2.5f, axisMode = KSPAxisMode.Incremental, guiActive = true, guiActiveEditor = true)] 
        [UI_FloatRange(stepIncrement = 0.0001f, minValue = -2.5f, maxValue = 2.5f, affectSymCounterparts = UI_Scene.None)]
        public float pawCoordinatesYaw;
        private BaseAxisField pawCoordinatesYaw_Field;
        private static string LOC_PAW_MANUALYAW = "Yaw offset";

        [KSPAxisField(guiFormat = "0.0°", incrementalSpeed = 30f, minValue = -2.5f, maxValue = 2.5f, axisMode = KSPAxisMode.Incremental, guiActive = true, guiActiveEditor = true)] 
        [UI_FloatRange(stepIncrement = 0.0001f, minValue = -2.5f, maxValue = 2.5f, affectSymCounterparts = UI_Scene.None)]
        public float pawCoordinatesRoll;
        private BaseAxisField pawCoordinatesRoll_Field;
        private static string LOC_PAW_MANUALROLL = "Roll offset";

        [KSPField(isPersistant = true, guiFormat = "0.0°", guiActive = true, guiActiveEditor = true)] 
        [UI_FloatRange(stepIncrement = 1f, minValue = -180f, maxValue = 180f, affectSymCounterparts = UI_Scene.None)]
        public float pawTargetRoll;
        private BaseField pawTargetRoll_Field;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true)] 
        [UI_Toggle(affectSymCounterparts = UI_Scene.None)]
        public bool trackingEnabled;
        private BaseField trackingEnabled_Field;
        private static string LOC_PAW_TRACKING = "Tracking";
        private static string LOC_PAW_ENABLED = "Enabled";
        private static string LOC_PAW_DISABLED = "Disabled";

        public enum TrackingConstraint
        {
            Position = 0,
            PositionAndDirection = 1,
            PositionAndRotation = 2
        }

        private TrackingConstraint CurrentTrackingConstraint => (TrackingConstraint)pawTrackingConstraint;

        public enum ControlMode
        {
            Free = 0,
            Target = 1
        }

        public ControlMode CurrentControlMode => (ControlMode)pawControlMode;

        public enum ExecutionMode
        {
            Continuous = 0,
            OnRequest = 1
        }

        public ExecutionMode CurrentExecutionMode => (ExecutionMode)pawTrackingMode;

        public override void OnAwake()
        {
            // onPartPersistentIdChanged can be called before OnStart(), so hook it right now
            GameEvents.onPartPersistentIdChanged.Add(PartPersistentIdChanged);

            bool isEditor = HighLogic.LoadedSceneIsEditor;
            BasePAWGroup configGroup = new BasePAWGroup(PAWGROUP_CONFIG, LOC_PAWGROUP_CONFIG, !isEditor);
            BasePAWGroup executionGroup = new BasePAWGroup(PAWGROUP_EXECUTION, LOC_PAWGROUP_EXECUTION, isEditor);
            BasePAWGroup targetGroup = new BasePAWGroup(PAWGROUP_TARGET, LOC_PAWGROUP_TARGET, isEditor);

            foreach (BaseField baseField in Fields)
            {
                switch (baseField.name)
                {
                    case nameof(pawStatus):
                        baseField.guiName = LOC_PAW_STATUS;
                        baseField.group = executionGroup;
                        break;
                    case nameof(pawServoSelect):
                        baseField.group = configGroup;
                        baseField.guiName = LOC_PAW_SERVOSELECT;
                        baseField.OnValueModified += OnUIAddServos;
                        pawServoSelect_UIControl = (UI_Toggle)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        break;
                    case nameof(pawEffectorSelect):
                        baseField.group = configGroup;
                        baseField.guiName = LOC_PAW_EFFECTORSELECT;
                        baseField.OnValueModified += OnUISelectEffector;
                        pawEffectorSelect_UIControl = (UI_Toggle)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        break;

                    case nameof(pawEffectorNode):
                        baseField.group = configGroup;
                        baseField.guiName = LOC_PAW_EFFECTORNODE;
                        baseField.OnValueModified += OnUIEffectorPositionChanged;
                        pawEffectorNode_Field = baseField;
                        pawEffectorNode_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        break;
                    case nameof(pawEffectorDir):
                        baseField.group = configGroup;
                        baseField.guiName = LOC_PAW_EFFECTORDIR;
                        baseField.OnValueModified += OnUIEffectorPositionChanged;
                        pawEffectorDir_Field = baseField;
                        UI_ChooseOption pawEffectorDir_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        pawEffectorDir_UIControl.options = DirOptions;
                        break;
                    case nameof(pawEffectorOffset):
                        baseField.group = configGroup;
                        baseField.guiName = LOC_PAW_EFFECTOROFFSET;
                        baseField.OnValueModified += OnUIEffectorPositionChanged;
                        pawEffectorOffset_Field = baseField;
                        break;
                    case nameof(pawServoGizmosEnabled):
                        baseField.group = configGroup;
                        baseField.guiName = LOC_PAW_SERVOGIZMOS;
                        baseField.OnValueModified += OnUIServoGizmosToggled;
                        break;
                    case nameof(pawTrackingGizmosEnabled):
                        baseField.group = configGroup;
                        baseField.guiName = LOC_PAW_TRACKINGGIZMOS;
                        baseField.OnValueModified += OnUITrackingGizmosToggled;
                        break;
                    case nameof(learningRateFactor):
                        baseField.group = configGroup;
                        baseField.guiName = LOC_PAW_LEARNINGRATE;
                        break;
                    case nameof(pawTargetSelect):
                        baseField.group = targetGroup;
                        baseField.guiName = LOC_PAW_TARGET;
                        baseField.OnValueModified += OnUISelectTarget;
                        pawTargetSelect_UIControl = (UI_Toggle)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
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
                        UI_ChooseOption pawTargetDir_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        pawTargetDir_UIControl.options = DirOptions;
                        break;
                    case nameof(pawTrackingConstraint):
                        baseField.group = executionGroup;
                        baseField.guiName = LOC_PAW_CONSTRAINT;
                        baseField.OnValueModified += OnUITrackingConstraintChanged;
                        UI_ChooseOption pawTrackingConstraint_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        pawTrackingConstraint_UIControl.options = TrackingConstraintOptions;
                        break;
                    case nameof(pawControlMode):
                        baseField.group = executionGroup;
                        baseField.guiName = LOC_PAW_CONTROLMODE;
                        baseField.OnValueModified += OnUIToggleControlMode;
                        pawControlMode_Field = baseField;
                        pawControlMode_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        break;
                    case nameof(pawTrackingMode):
                        baseField.group = executionGroup;
                        baseField.guiName = LOC_PAW_TRACKINGMODE;
                        baseField.OnValueModified += OnUIToggleExecutionMode;
                        UI_ChooseOption pawTrackingMode_UIControl = (UI_ChooseOption)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        pawTrackingMode_UIControl.options = TrackingModeOptions;
                        break;
                    case nameof(pawCoordinatesRange):
                        baseField.group = executionGroup;
                        baseField.guiName = LOC_PAW_COORDINATESRANGE;
                        baseField.OnValueModified += OnUICoordinatesRangeChanged;
                        pawCoordinatesRange_Field = baseField;
                        break;
                    case nameof(pawCoordinatesX):
                        baseField.group = executionGroup;
                        baseField.guiName = LOC_PAW_MANUALX;
                        baseField.OnValueModified += OnUICoordinatesChanged;
                        pawCoordinatesX_Field = (BaseAxisField)baseField;
                        pawCoordinatesX_UIControl = (UI_FloatRange)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        break;
                    case nameof(pawCoordinatesY):
                        baseField.group = executionGroup;
                        baseField.guiName = LOC_PAW_MANUALY;
                        baseField.OnValueModified += OnUICoordinatesChanged;
                        pawCoordinatesY_Field = (BaseAxisField)baseField;
                        pawCoordinatesY_UIControl = (UI_FloatRange)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        break;
                    case nameof(pawCoordinatesZ):
                        baseField.group = executionGroup;
                        baseField.guiName = LOC_PAW_MANUALZ;
                        baseField.OnValueModified += OnUICoordinatesChanged;
                        pawCoordinatesZ_Field = (BaseAxisField)baseField;
                        pawCoordinatesZ_UIControl = (UI_FloatRange)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        break;
                    case nameof(pawCoordinatesPitch):
                        baseField.group = executionGroup;
                        baseField.guiName = LOC_PAW_MANUALPITCH;
                        baseField.OnValueModified += OnUIManualPitch;
                        pawCoordinatesPitch_Field = (BaseAxisField)baseField;
                        break;
                    case nameof(pawCoordinatesYaw):
                        baseField.group = executionGroup;
                        baseField.guiName = LOC_PAW_MANUALYAW;
                        baseField.OnValueModified += OnUIManualYaw;
                        pawCoordinatesYaw_Field = (BaseAxisField)baseField;
                        break;
                    case nameof(pawCoordinatesRoll):
                        baseField.group = executionGroup;
                        baseField.guiName = LOC_PAW_MANUALROLL;
                        baseField.OnValueModified += OnUIManualRoll;
                        pawCoordinatesRoll_Field = (BaseAxisField)baseField;
                        break;
                    case nameof(pawTargetRoll):
                        baseField.group = executionGroup;
                        baseField.guiName = LOC_PAW_MANUALROLL;
                        baseField.OnValueModified += OnUITargetPositionChanged;
                        pawTargetRoll_Field = baseField;
                        break;
                    case nameof(trackingEnabled):
                        baseField.group = executionGroup;
                        baseField.guiName = LOC_PAW_TRACKING;
                        baseField.OnValueModified += OnUIToggleTracking;
                        trackingEnabled_Field = baseField;
                        UI_Toggle control = (UI_Toggle)(isEditor ? baseField.uiControlEditor : baseField.uiControlFlight);
                        control.enabledText = LOC_PAW_ENABLED;
                        control.disabledText = LOC_PAW_DISABLED;
                        break;
                }
            }

            foreach (BaseEvent baseEvent in Events)
            {
                switch (baseEvent.name)
                {
                    case nameof(pawManualExecute):
                        baseEvent.group = executionGroup;
                        baseEvent.guiName = LOC_PAW_EXECUTE;
                        pawManualExecute_Event = baseEvent;
                        break;
                    case nameof(pawResetOffsets):
                        baseEvent.group = executionGroup;
                        baseEvent.guiName = LOC_PAW_RESETOFFSETS;
                        break;
                    case nameof(pawResetToZero):
                        baseEvent.group = executionGroup;
                        baseEvent.guiName = LOC_PAW_RESET;
                        break;
                }
            }

            for (int i = part.modules.Count; i-- > 0;)
            {
                if (part.modules[i] is ModuleRoboticController)
                {
                    BasePAWGroup controllerGroup = new BasePAWGroup(PAWGROUP_STOCKCONTROLLER, AUTOLOC_PAWGROUP_STOCKCONTROLLER, !isEditor);
                    PartModule pm = part.modules[i];
                    for (int j = pm.fields.Count; j-- > 0;)
                        pm.fields[j].group = controllerGroup;
                    for (int j = pm.events.Count; j-- > 0;)
                        pm.events.GetByIndex(j).group = controllerGroup;
                    break;
                }
            }
        }

        public override void OnStartFinished(StartState state)
        {
            if (persistedPartIdloading)
            {
                if (!FlightGlobals.FindLoadedPart(persistedEffectorPartId, out effectorPart))
                    Debug.LogWarning($"[EasyRobotics] Couldn't find effector with part id {persistedEffectorPartId}");

                if (!FlightGlobals.FindLoadedPart(persistedTargetPartId, out targetPart))
                    Debug.LogWarning($"[EasyRobotics] Couldn't find target with part id {persistedTargetPartId}");

                persistedEffectorPartId = 0;
                persistedTargetPartId = 0;

                if (persistedIkChainPartIds != null)
                {
                    for (int i = 0; i < persistedIkChainPartIds.Count; i++)
                    {
                        if (!FlightGlobals.FindLoadedPart(persistedIkChainPartIds[i], out Part servoPart))
                        {
                            Debug.LogWarning($"[EasyRobotics] Couldn't find servo with part id {persistedIkChainPartIds[i]}");
                            continue;
                        }

                        BaseServo servo = servoPart.FindModuleImplementing<BaseServo>();
                        if (servo.IsNullRef())
                            continue;

                        ServoJoint newJoint;
                        if (servo is ModuleRoboticRotationServo rotationServo)
                            newJoint = new RotationServoJoint(rotationServo);
                        else if (servo is ModuleRoboticServoHinge hingeServo)
                            newJoint = new HingeServoJoint(hingeServo);
                        else
                            continue;

                        ikChain.Add(newJoint);
                    }

                    persistedIkChainPartIds = null;
                }

                persistedPartIdloading = false;
            }

            StartCoroutine(WaitForServosReadyAndStart());

            bool isFlight = HighLogic.LoadedSceneIsFlight;

            if (isFlight)
            {
                GameEvents.onPartWillDie.Add(OnPartWillDie);
                GameEvents.onPartDeCoupleNewVesselComplete.Add(OnVesselDecoupleOrUndock);
                GameEvents.onVesselsUndocking.Add(OnVesselDecoupleOrUndock);
                GameEvents.onSameVesselDock.Add(OnSameVesselDock);
            }
            else
            {
                GameEvents.onEditorPartEvent.Add(OnEditorPartEvent);
            }

            GameEvents.onPartActionUIShown.Add(OnPAWShown);
            GameEvents.onPartActionUIDismiss.Add(OnPAWDismiss);
        }

        private IEnumerator WaitForServosReadyAndStart()
        {
            while (true)
            {
                bool servosReady = true;
                for (int i = ikChain.Count; i-- > 0;)
                    servosReady &= ikChain[i].BaseServo.ServoInitComplete;

                if (servosReady)
                    break;

                yield return null;
            }

            bool trackingEnabledOnStart = trackingEnabled;

            OnEffectorPartChanged(out _);
            OnIKConfigurationBecameInvalid();
            OnTargetPartChanged();
            OnControlModeChanged();
            OnTrackingModeChanged();
            OnTrackingToggled(out _);
            OnTargetPositionChanged();

            if (trackingEnabledOnStart)
                trackingEnabled_Field.SetValue(true, this);
        }

        private void OnPAWShown(UIPartActionWindow paw, Part pawPart)
        {
            if (pawPart.RefNotEquals(part))
                return;

            if (pawTrackingGizmosEnabled)
            {
                ShowEffectorGizmo(true);
                ShowTargetGizmo(true);
            }

            if (pawServoGizmosEnabled)
                ShowChainGizmos(true);

            _pawUpdateCoroutine = StartCoroutine(PAWUpdateCoroutine());
        }

        private void OnPAWDismiss(Part pawPart)
        {
            if (pawPart.RefNotEquals(part))
                return;

            ShowEffectorGizmo(false);
            ShowTargetGizmo(false);
            ShowChainGizmos(false);

            if (_pawUpdateCoroutine != null)
            {
                StopCoroutine(_pawUpdateCoroutine);
                _pawUpdateCoroutine = null;
            }

            SelectionModeExit();
        }

        private Coroutine _pawUpdateCoroutine;

        private IEnumerator PAWUpdateCoroutine()
        {
            while (true)
            {
                if (!trackingEnabled)
                {
                    SyncAllTransforms();
                }
                else
                {
                    UpdateStatusOnTracking();
                }

                if (pawTrackingGizmosEnabled)
                {
                    if (effectorGizmo.IsNotNullOrDestroyed() && effectorGizmo.activeSelf)
                    {
                        effector.GetPosAndRot(out Vector3 pos, out Quaternion rot);
                        effectorGizmo.transform.SetPositionAndRotation(pos, rot);
                    }

                    if (targetGizmo.IsNotNullOrDestroyed() && targetGizmo.activeSelf)
                    {
                        target.GetPosAndRot(out Vector3 pos, out Quaternion rot);
                        targetGizmo.transform.SetPositionAndRotation(pos, rot);
                    }
                }

                if (pawServoGizmosEnabled)
                {
                    for (int i = ikChain.Count; i-- > 0;)
                    {
                        ikChain[i].SyncGizmo();
                    }
                }

                yield return null;
            }
        }

        private void OnDestroy()
        {
            if (selectionMode != SelectionMode.None)
                SelectionModeExit();

            if (targetGizmo.IsNotNullOrDestroyed())
                Destroy(targetGizmo);

            if (effectorGizmo.IsNotNullOrDestroyed())
                Destroy(effectorGizmo);

            for (int i = ikChain.Count; i-- > 0;)
                ikChain[i].OnDestroy();

            GameEvents.onPartPersistentIdChanged.Remove(PartPersistentIdChanged);
            GameEvents.onPartWillDie.Remove(OnPartWillDie);
            GameEvents.onPartDeCoupleNewVesselComplete.Remove(OnVesselDecoupleOrUndock);
            GameEvents.onVesselsUndocking.Remove(OnVesselDecoupleOrUndock);
            GameEvents.onEditorPartEvent.Remove(OnEditorPartEvent);
            GameEvents.onPartActionUIShown.Remove(OnPAWShown);
            GameEvents.onPartActionUIDismiss.Remove(OnPAWDismiss);
            GameEvents.onSameVesselDock.Remove(OnSameVesselDock);
        }

        private const string EFFECTORPARTID = "effPartId";
        private const string TARGETPARTID = "tgtPartId";
        private const string IKCHAINPARTIDS = "IK_CHAIN";

        public override void OnSave(ConfigNode node)
        {
            if (effectorPart.IsNotNullOrDestroyed())
                node.AddValue(EFFECTORPARTID, effectorPart.persistentId);

            if (targetPart.IsNotNullOrDestroyed())
                node.AddValue(TARGETPARTID, targetPart.persistentId);

            if (ikChain.Count > 0)
            {
                ConfigNode ikChainNode = node.AddNode(IKCHAINPARTIDS);
                for (int i = 0; i < ikChain.Count; i++)
                {
                    ServoJoint servoJoint = ikChain[i];
                    if (servoJoint is IRotatingServoJoint rotatingJoint)
                    {
                        ikChainNode.AddValue("id", servoJoint.BaseServo.part.persistentId);
                    }
                }
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            if (HighLogic.LoadedScene == GameScenes.LOADING)
                return;

            persistedPartIdloading = false;
            persistedEffectorPartId = 0;
            persistedEffectorPartId = 0;

            for (int i = node.values.Count; i-- > 0;)
            {
                ConfigNode.Value value = node.values[i];
                switch (value.name)
                {
                    case EFFECTORPARTID:
                        persistedPartIdloading |= uint.TryParse(value.value, out persistedEffectorPartId);
                        break;
                    case TARGETPARTID:
                        persistedPartIdloading |= uint.TryParse(value.value, out persistedTargetPartId);
                        break;
                }
            }

            for (int i = node.nodes.Count; i-- > 0;)
            {
                ConfigNode childNode = node.nodes[i];
                if (childNode.name == IKCHAINPARTIDS)
                {
                    persistedIkChainPartIds = new List<uint>(childNode.values.Count);
                    for (int j = 0; j < childNode.values.Count; j++)
                    {
                        if (uint.TryParse(childNode.values[j].value, out uint id) && id != 0)
                        {
                            persistedPartIdloading = true;
                            persistedIkChainPartIds.Add(id);
                        }
                    }
                }
            }
        }

        private void OnEditorPartEvent(ConstructionEventType eventType, Part part)
        {
            switch (eventType)
            {
                case ConstructionEventType.PartDeleted:
                case ConstructionEventType.PartSymmetryDeleted:
                    OnPartDying(part);
                    break;
                case ConstructionEventType.PartAttached:
                case ConstructionEventType.PartDetached:
                case ConstructionEventType.PartOffset:
                case ConstructionEventType.PartRotated:
                case ConstructionEventType.PartTweaked:
                    if (part.RefEquals(effectorPart))
                    {
                        OnIKConfigurationBecameInvalid();
                    }
                    else
                    {
                        for (int i = ikChain.Count; i-- > 0;)
                            if (ikChain[i].BaseServo.part.RefEquals(part))
                                OnIKConfigurationBecameInvalid();
                    }

                    break;
            }
        }

        private void OnPartWillDie(Part dyingPart)
        {
            OnPartDying(dyingPart);
        }

        private void OnVesselDecoupleOrUndock(Vessel oldVessel, Vessel newVessel)
        {
            if (vessel.RefNotEquals(oldVessel) && vessel.RefNotEquals(newVessel))
                return;

            List<Part> otherVesselParts = vessel.RefEquals(oldVessel) ? newVessel.parts : oldVessel.parts;

            for (int i = otherVesselParts.Count; i-- > 0;)
                OnPartRemoved(otherVesselParts[i]);
        }

        private void PartPersistentIdChanged(uint vesselId, uint oldId, uint newId)
        {
            if (!persistedPartIdloading)
                return;

            if (persistedEffectorPartId == oldId)
                persistedEffectorPartId = newId;

            if (persistedTargetPartId == oldId)
                persistedTargetPartId = newId;

            if (persistedIkChainPartIds != null)
            {
                for (int i = persistedIkChainPartIds.Count; i-- > 0;)
                {
                    if (persistedIkChainPartIds[i] == oldId)
                        persistedIkChainPartIds[i] = newId;
                }
            }
        }

        void IShipConstructIDChanges.UpdatePersistentIDs(Dictionary<uint, uint> changedIDs)
        {
            if (!persistedPartIdloading || changedIDs.Count == 0)
                return;

            uint newId;

            if (changedIDs.TryGetValue(persistedEffectorPartId, out newId))
                persistedEffectorPartId = newId;

            if (changedIDs.TryGetValue(persistedTargetPartId, out newId))
                persistedTargetPartId = newId;

            if (persistedIkChainPartIds != null)
            {
                for (int i = persistedIkChainPartIds.Count; i-- > 0;)
                {
                    if (changedIDs.TryGetValue(persistedIkChainPartIds[i], out newId))
                        persistedIkChainPartIds[i] = newId;
                }
            }
        }

        private void OnPartDying(Part dyingPart)
        {
            OnPartRemoved(dyingPart);

            if (ReferenceEquals(dyingPart, targetPart))
            {
                targetPart = null;
                OnTargetPartChanged();
            }
        }

        private void OnPartRemoved(Part removedPart)
        {
            if (ReferenceEquals(removedPart, effectorPart))
            {
                effectorPart = null;
                OnEffectorPartChanged(out _);
            }

            for (int i = ikChain.Count; i-- > 0;)
            {
                ServoJoint servoJoint = ikChain[i];
                if (ReferenceEquals(removedPart, servoJoint.BaseServo.part))
                {
                    ikChain[i].OnDestroy();
                    ikChain.RemoveAt(i);
                    OnIKConfigurationBecameInvalid();
                    break;
                }
            }
        }

        private void OnSameVesselDock(GameEvents.FromToAction<ModuleDockingNode, ModuleDockingNode> data)
        {
            if (CurrentExecutionMode != ExecutionMode.Continuous || !trackingEnabled)
                return;

            // TODO : check if the servo chain is now in a closed loop, as the user didn't necessarily select a docking port as target/effector 
            if (data.from.part.RefNotEquals(effectorPart) && data.to.part.RefNotEquals(effectorPart) && data.from.part.RefNotEquals(targetPart) && data.to.part.RefNotEquals(targetPart))
                return;

            if (trackingEnabled)
            {
                DisableTracking();
                PostScreenMessage("Same-vessel docking confirmed, disabling tracking...");
            }
        }

        private void OnUIAddServos(object _)
        {
            if (pawServoSelect)
                SelectionModeEnter(SelectionMode.SelectServos);
            else
                SelectionModeExit();
        }

        private void OnUISelectEffector(object _)
        {
            if (pawEffectorSelect)
                SelectionModeEnter(SelectionMode.SelectEffector);
            else
                SelectionModeExit();
        }

        private void OnUISelectTarget(object _)
        {
            if (pawTargetSelect)
                SelectionModeEnter(SelectionMode.SelectPartTarget);
            else
                SelectionModeExit();
        }

        private void OnUIServoGizmosToggled(object _)
        {
            ShowChainGizmos(pawServoGizmosEnabled);
        }

        private void OnUITrackingGizmosToggled(object _)
        {
            ShowEffectorGizmo(pawTrackingGizmosEnabled);
            ShowTargetGizmo(pawTrackingGizmosEnabled);
        }

        private void OnUIEffectorPositionChanged(object _) => OnEffectorPositionChanged();

        private void OnUIToggleControlMode(object _) => OnControlModeChanged();

        private void OnUIToggleExecutionMode(object _) => OnTrackingModeChanged();

        private void OnUIToggleTracking(object _)
        {
            OnTrackingToggled(out string error);
            if (error != null)
                PostScreenMessage(error, ScreenMessageType.Error);
        }

        private void OnUIManualModeExecute() => ExecuteIKOnce();

        private void OnUITrackingConstraintChanged(object _) => OnTrackingConstraintChanged();

        private void OnUITargetPositionChanged(object _) => OnTargetPositionChanged();

        private void OnUICoordinatesRangeChanged(object _) => OnCoordinatesRangeChanged();

        private void OnUICoordinatesChanged(object _) => OnTargetPositionChanged();

        private bool _hasManualRotChanged;
        private bool _isManualRotChanging;

        private enum RotationInput
        {
            Pitch,
            Yaw,
            Roll
        }

        private void OnUIManualPitch(object _)
        {
            _hasManualRotChanged = true;
            if (!_isManualRotChanging)
                StartCoroutine(HandleManualTargetRotation(RotationInput.Pitch));
        }

        private void OnUIManualYaw(object _)
        {
            _hasManualRotChanged = true;
            if (!_isManualRotChanging)
                StartCoroutine(HandleManualTargetRotation(RotationInput.Yaw));
        }

        private void OnUIManualRoll(object _)
        {
            _hasManualRotChanged = true;
            if (!_isManualRotChanging)
                StartCoroutine(HandleManualTargetRotation(RotationInput.Roll));
        }

        private IEnumerator HandleManualTargetRotation(RotationInput input)
        {
            _isManualRotChanging = true;
            while (_hasManualRotChanged)
            {
                float time = Time.time;
                while (time + 0.3f > Time.time)
                {
                    Quaternion offsetRot;
                    switch (input)
                    {
                        case RotationInput.Pitch:
                            offsetRot = Quaternion.AngleAxis(pawCoordinatesPitch, Vector3.right);
                            break;
                        case RotationInput.Yaw:
                            offsetRot = Quaternion.AngleAxis(pawCoordinatesYaw, Vector3.forward);
                            break;
                        default:
                            offsetRot = Quaternion.AngleAxis(pawCoordinatesRoll, Vector3.up);
                            break;
                    }

                    target.Rotation *= offsetRot;

                    Vector3 refPos = ikChain[ikChain.Count - 1].BaseServo.transform.position;
                    Vector3 targetPos = target.Rotation.Inverse() * (target.Position - refPos);
                    pawCoordinatesX = targetPos.x;
                    pawCoordinatesY = targetPos.y;
                    pawCoordinatesZ = targetPos.z;

                    yield return null;
                }

                _hasManualRotChanged = false;
                yield return null;
            }

            _isManualRotChanging = false;
            pawCoordinatesPitch = 0f;
            pawCoordinatesYaw = 0f;
            pawCoordinatesRoll = 0f;
        }

        private void OnControlModeChanged()
        {
            ResetPosRotOffsets();
            DisableTracking();
            UpdateExecutionPAWVisibility();
            OnStatusChanged();
        }

        private void OnUIResetPosRotOffsets()
        {
            ResetPosRotOffsets();
        }

        private void ResetPosRotOffsets()
        {
            pawTargetRoll = 0f;

            if (CurrentControlMode == ControlMode.Target)
            {
                pawCoordinatesX = 0f;
                pawCoordinatesY = 0f;
                pawCoordinatesZ = 0f;
            }
            else if (CurrentControlMode == ControlMode.Free)
            {
                if (targetPart.IsNotNullOrDestroyed())
                {
                    GetUserTargetPosAndRot(out _, out Quaternion targetRot);
                    Vector3 refPos = ikChain[ikChain.Count - 1].BaseServo.transform.position;
                    Vector3 targetPos = targetRot.Inverse() * (target.Position - refPos);
                    pawCoordinatesX = targetPos.x;
                    pawCoordinatesY = targetPos.y;
                    pawCoordinatesZ = targetPos.z;
                    target.Rotation = targetRot;
                }
                else if (ikChain.Count > 0)
                {
                    pawCoordinatesX = 0f;
                    pawCoordinatesY = 0f;
                    pawCoordinatesZ = 0f;
                    target.Rotation = ikChain[ikChain.Count - 1].BaseServo.transform.rotation;
                }
            }
        }

        private void OnTrackingModeChanged()
        {
            DisableTracking();
            OnStatusChanged();
            part.RefreshPAWLayout();
        }

        private void OnTrackingConstraintChanged()
        {
            UpdateExecutionPAWVisibility();
            DisableTracking();

            // TODO : switch target model to cross for position, cross+arrows for direction, full gizmo for rotation
        }

        private void DisableTracking()
        {
            if (trackingEnabled)
                trackingEnabled_Field.SetValue(false, this);
        }

        private void UpdateExecutionPAWVisibility()
        {
            switch (CurrentControlMode)
            {
                case ControlMode.Target:
                    pawCoordinatesPitch_Field.SetGUIActive(false);
                    pawCoordinatesRoll_Field.SetGUIActive(false);
                    pawCoordinatesYaw_Field.SetGUIActive(false);
                    pawTargetRoll_Field.SetGUIActive(CurrentTrackingConstraint == TrackingConstraint.PositionAndRotation);
                    break;
                case ControlMode.Free:
                    pawCoordinatesPitch_Field.SetGUIActive(CurrentTrackingConstraint != TrackingConstraint.Position);
                    pawCoordinatesRoll_Field.SetGUIActive(CurrentTrackingConstraint == TrackingConstraint.PositionAndRotation);
                    pawCoordinatesYaw_Field.SetGUIActive(CurrentTrackingConstraint != TrackingConstraint.Position);
                    pawTargetRoll_Field.SetGUIActive(false);
                    break;
            }

            part.RefreshPAWLayout();
        }

        private void OnTrackingToggled(out string error)
        {
            if (trackingEnabled)
            {
                if (!IsServoChainLocked(out error) && ConfigureIKChain(out error))
                {
                    _trackingCoroutine = StartCoroutine(TrackingCoroutine());
                }
                else
                {
                    trackingEnabled = false;
                }
            }
            else
            {
                if (_trackingCoroutine != null)
                {
                    StopCoroutine(_trackingCoroutine);
                    _trackingCoroutine = null;
                }

                for (int i = ikChain.Count; i-- > 0;)
                    if (ikChain[i] is IRotatingServoJoint rotatingJoint)
                        rotatingJoint.ServoTargetAngle = rotatingJoint.ServoCurrentAngle;

                error = null;
            }

            OnStatusChanged();
        }

        private bool IsServoChainLocked(out string error)
        {
            error = null;
            int jointCount = ikChain.Count;
            bool locked = false;
            for (int i = jointCount; i-- > 0;)
            {
                ServoJoint servoJoint = ikChain[i];
                if (!servoJoint.CanMove)
                {
                    if (error is null)
                        error = string.Empty;
                    else
                        error += '\n';

                    if (servoJoint.BaseServo.servoIsLocked)
                        error += $"{servoJoint.BaseServo.PartTitle()} is locked";
                    else
                        error += $"{servoJoint.BaseServo.PartTitle()} isn't motorized";

                    servoJoint.BaseServo.part.Blink(Color.red, 5f);
                    locked = true;
                }
            }

            return locked;
        }

        private void OnIKConfigurationBecameInvalid()
        {
            OnIKConfigurationBecameInvalid(out _);
        }

        private void OnIKConfigurationBecameInvalid(out string error)
        {
            if (trackingEnabled)
            {
                trackingEnabled = false;
                OnTrackingToggled(out _);
            }

            configurationIsValid = false;

            ConfigureIKChain(out error);

            OnStatusChanged();
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

                ShowEffectorGizmo(false);

                if (effector != null)
                {
                    effector.SetParent(null);
                    effector = null;
                }

                virtualEffectorDockingNode = null;
            }
            else
            {
                effector = new BasicTransform(null);

                pawEffectorNode_Field.SetGUIActive(true);
                pawEffectorDir_Field.SetGUIActive(true);
                pawEffectorOffset_Field.SetGUIActive(true);

                ShowEffectorGizmo(true);

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

            string effectorName = effectorPart != null ? effectorPart.PartTitle() : LOC_PAW_NONE;
            pawEffectorSelect_UIControl.enabledText = effectorName;
            pawEffectorSelect_UIControl.disabledText = effectorName;

            part.RefreshPAWLayout();

            configurationIsValid = false;

            ConfigureIKChain(out error);

            OnStatusChanged();
        }

        private void OnTargetPartChanged()
        {
            if (target == null)
            {
                target = new BasicTransform(null);
                ShowTargetGizmo(pawTrackingGizmosEnabled);
            }

            pawTargetRoll = 0f;

            if (targetPart.IsNullOrDestroyed())
            {
                pawTargetNode_Field.SetGUIActive(false);
                pawTargetDir_Field.SetGUIActive(false);
                virtualTargetDockingNode = null;

                pawControlMode_UIControl.options = ControlModeManualOption;
                pawControlMode_Field.SetValue((int)ControlMode.Free, this);

                if (trackingEnabled)
                {
                    trackingEnabled = false;
                    OnTrackingToggled(out _);
                }
            }
            else
            {
                pawTargetNode_Field.SetGUIActive(true);
                pawTargetDir_Field.SetGUIActive(true);
                pawControlMode_UIControl.options = ControlModeAllOptions;

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

            string targetName = targetPart.IsNotNullOrDestroyed() ? targetPart.PartTitle() : LOC_PAW_NONE;
            pawTargetSelect_UIControl.enabledText = targetName;
            pawTargetSelect_UIControl.disabledText = targetName;

            OnStatusChanged();
            part.RefreshPAWLayout(false);
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

            GetEffectorPartPosAndRot(out Vector3 effectorPos, out Quaternion effectorRot);
            effector.SetPosAndRot(effectorPos, effectorRot);
        }

        private void GetEffectorPartPosAndRot(out Vector3 pos, out Quaternion rot)
        {
            pos = effectorPart.transform.position;
            rot = effectorPart.transform.rotation;

            if (pawEffectorNode > 0)
            {
                int i = pawEffectorNode - 1;
                AttachNode node;
                if (virtualEffectorDockingNode != null && i == effectorPart.attachNodes.Count)
                    node = virtualEffectorDockingNode;
                else
                    node = effectorPart.attachNodes[i];

                pos += rot * node.position;
                rot *= Quaternion.FromToRotation(Vector3.up, node.orientation);
            }

            rot = RotateByDirIndex(rot, pawEffectorDir);

            if (pawEffectorOffset > 0f)
                pos += rot * (Vector3.up * pawEffectorOffset);
        }

        private void OnTargetPositionChanged()
        {
            switch (CurrentControlMode)
            {
                case ControlMode.Target:
                {
                    GetUserTargetPosAndRot(out Vector3 targetPos, out Quaternion targetRot);
                    target.SetPosAndRot(targetPos, targetRot);
                    break;
                }
                case ControlMode.Free:
                {
                    Vector3 targetPos = new Vector3(pawCoordinatesX, pawCoordinatesY, pawCoordinatesZ);
                    target.Position = GetRootServoTransformOrFallback().position + target.Rotation * targetPos;
                    break;
                }
            }
        }

        private Transform GetRootServoTransformOrFallback()
        {
            if (ikChain.Count == 0)
                return transform;

            ServoJoint rootServo = ikChain[ikChain.Count - 1];
            return rootServo.IsAttachedByMovingPart ? rootServo.BaseServo.movingPartObject.transform : rootServo.BaseServo.transform;
        }

        private void GetUserTargetPosAndRot(out Vector3 pos, out Quaternion rot)
        {
            Transform targetPartTransform = targetPart.transform;
            pos = targetPartTransform.position;
            rot = targetPartTransform.rotation;

            if (pawTargetNode > 0)
            {
                int i = pawTargetNode - 1;
                AttachNode node;
                if (virtualTargetDockingNode != null && i == targetPart.attachNodes.Count)
                    node = virtualTargetDockingNode;
                else
                    node = targetPart.attachNodes[i];

                pos += rot * node.position;
                rot *= Quaternion.FromToRotation(Vector3.up, node.orientation);
            }

            rot = RotateByDirIndex(rot, pawTargetDir);

            if (pawCoordinatesX != 0f || pawCoordinatesY != 0f || pawCoordinatesZ != 0f)
            {
                Vector3 posOffset = new Vector3(pawCoordinatesX, pawCoordinatesY, pawCoordinatesZ);
                pos += rot * posOffset;
            }

            if (pawTargetRoll != 0f)
                rot *= Quaternion.AngleAxis(pawTargetRoll, Vector3.up);
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
                case 1:
                    initialRotation *= upToDown;
                    break;
                case 2:
                    initialRotation *= upToForward;
                    break;
                case 3:
                    initialRotation *= upToBack;
                    break;
                case 4:
                    initialRotation *= upToRight;
                    break;
                case 5:
                    initialRotation *= upToLeft;
                    break;
            }

            return initialRotation;
        }

        private void OnStatusChanged()
        {
            bool canExecute = configurationIsValid && (targetPart.IsNotNullOrDestroyed() || CurrentControlMode == ControlMode.Free);

            switch (CurrentExecutionMode)
            {
                case ExecutionMode.Continuous:
                    pawManualExecute_Event.SetGUIActive(false);
                    trackingEnabled_Field.SetGUIActive(canExecute);
                    break;
                case ExecutionMode.OnRequest:
                    pawManualExecute_Event.SetGUIActive(canExecute);
                    trackingEnabled_Field.SetGUIActive(false);
                    break;
            }

            string servoSelectLabel = null;

            if (ikChain.Count == 0)
            {
                pawStatus = $"<color=orange><b>{LOC_PAW_STATUS_NOSERVOS}</b></color>";
                servoSelectLabel = pawStatus;
            }
            else if (effectorPart.IsNullOrDestroyed())
            {
                pawStatus = $"<color=orange><b>{LOC_PAW_STATUS_NOEFFECTOR}</b></color>";
            }
            else if (!configurationIsValid)
            {
                pawStatus = $"<color=orange><b>{LOC_PAW_STATUS_INVALID}</b></color>";
                servoSelectLabel = pawStatus;
            }
            else if (!trackingEnabled)
            {
                if (canExecute)
                    pawStatus = LOC_PAW_STATUS_READY;
                else
                    pawStatus = LOC_PAW_STATUS_NOTARGET;
            }
            else
            {
                pawStatus = LOC_PAW_STATUS_TRACKING;
            }

            if (servoSelectLabel == null)
                servoSelectLabel = $"{ikChain.Count} servos";

            pawServoSelect_UIControl.enabledText = servoSelectLabel;
            pawServoSelect_UIControl.disabledText = servoSelectLabel;
        }

        private void UpdateStatusOnTracking()
        {
            if (ikTargetReached)
            {
                if (servosTargetReached)
                    pawStatus = "Locked on target";
                else
                    pawStatus = "Waiting for servos to reach target";
            }
            else
            {
                pawStatus = $"Searching solution, error: {targetPosError:F2}m / {targetAngleError:F0}°";
            }
        }

        private void OnCoordinatesRangeChanged()
        {
            float manualModeRange;

            if (pawCoordinatesRange == 0f)
                manualModeRange = Mathf.Ceil(GetChainLength());
            else
                manualModeRange = pawCoordinatesRange;

            pawCoordinatesX_Field.minValue = -manualModeRange;
            pawCoordinatesX_Field.maxValue = manualModeRange;
            pawCoordinatesY_Field.minValue = -manualModeRange;
            pawCoordinatesY_Field.maxValue = manualModeRange;
            pawCoordinatesZ_Field.minValue = -manualModeRange;
            pawCoordinatesZ_Field.maxValue = manualModeRange;
            pawCoordinatesX_UIControl.minValue = -manualModeRange;
            pawCoordinatesX_UIControl.maxValue = manualModeRange;
            pawCoordinatesY_UIControl.minValue = -manualModeRange;
            pawCoordinatesY_UIControl.maxValue = manualModeRange;
            pawCoordinatesZ_UIControl.minValue = -manualModeRange;
            pawCoordinatesZ_UIControl.maxValue = manualModeRange;

            if (pawCoordinatesX > manualModeRange)
                pawCoordinatesX_Field.SetValue(manualModeRange, this);
            else if (pawCoordinatesX < -manualModeRange)
                pawCoordinatesX_Field.SetValue(-manualModeRange, this);

            if (pawCoordinatesY > manualModeRange)
                pawCoordinatesY_Field.SetValue(manualModeRange, this);
            else if (pawCoordinatesY < -manualModeRange)
                pawCoordinatesY_Field.SetValue(-manualModeRange, this);

            if (pawCoordinatesZ > manualModeRange)
                pawCoordinatesZ_Field.SetValue(manualModeRange, this);
            else if (pawCoordinatesZ < -manualModeRange)
                pawCoordinatesZ_Field.SetValue(-manualModeRange, this);
        }

        private static List<ServoJoint> lastIKChain = new List<ServoJoint>();
        private static Dictionary<Part, ServoJoint> jointDict = new Dictionary<Part, ServoJoint>();
        private static Stack<Part> partStack = new Stack<Part>();

        private enum Relation
        {
            Child,
            Parent
        }

        private bool ConfigureIKChain(out string error)
        {
            error = null;

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
                return true;

            DisableJointHierarchy();

            // put all joints in a dictionary
            jointDict.Clear();
            lastIKChain.Clear();

            int jointCount = ikChain.Count;

            for (int i = jointCount; i-- > 0;)
            {
                ServoJoint joint = ikChain[i];
                lastIKChain.Add(joint);
                jointDict.Add(joint.BaseServo.part, joint);
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
                    ikChain.Clear();
                    ikChain.AddRange(lastIKChain);
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
                    ikChain.Clear();
                    ikChain.AddRange(lastIKChain);
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
                Part servoPart = joint.BaseServo.part;
                Part parent = servoPart.parent;

                foreach (AttachNode node in joint.BaseServo.attachNodes)
                {
                    if (node.attachedPart == parent)
                    {
                        parentIsAttachedToMovingPart = true;
                        break;
                    }
                }

                if (servoPart.srfAttachNode != null && servoPart.srfAttachNode.attachedPart == parent)
                {
                    foreach (string mesh in joint.BaseServo.servoSrfMeshes)
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
                joint.IsAttachedByMovingPart = effectorRelation == Relation.Child ? parentIsAttachedToMovingPart : !parentIsAttachedToMovingPart;
                joint.OnModified();
                joint.SyncWithServoTransform();
            }

            effector.SetParentKeepWorldPosAndRot(ikChain[0].movingTransform);

            OnCoordinatesRangeChanged();

            lastIKChain.Clear();
            configurationIsValid = true;
            return true;
        }

        private float GetChainLength()
        {
            float chainLength = 0f;

            for (int i = ikChain.Count - 1; i-- > 0;)
                chainLength += ikChain[i].baseTransform.LocalPosition.magnitude;

            if (effector != null)
                chainLength += effector.LocalPosition.magnitude;

            return chainLength;
        }

        private void DisableJointHierarchy()
        {
            effector?.SetParentKeepWorldPosAndRot(null);

            foreach (ServoJoint joint in ikChain)
            {
                joint.baseTransform.SetParentKeepWorldPosAndRot(null);
            }
        }

        private void ResetToZero()
        {
            for (int i = ikChain.Count; i-- > 0;)
            {
                ikChain[i].SetIKPose(ServoJoint.Pose.Zero);
                ikChain[i].SendRequestToServo();
            }
        }

        private void SyncAllTransforms()
        {
            for (int i = ikChain.Count; i-- > 0;)
                ikChain[i].SyncWithServoTransform();

            if (effector != null)
                OnEffectorPositionChanged();

            if (target != null)
                OnTargetPositionChanged();
        }

        private ScreenMessage selectionModeMessage;

        private static string LOC_SELECTMODE_SELECT = "<b>[ENTER]</b> to select";
        private static string LOC_SELECTMODE_REMOVE = "<b>[DELETE]</b> to remove";
        private static string LOC_SELECTMODE_ESC = "<b>[ESC]</b> to end";
        private static string LOC_SELECTMODE_SERVO = "Select servos parts";
        private static string LOC_SELECTMODE_EFFECTOR = "Select effector part";
        private static string LOC_SELECTMODE_TARGETPART = "Select target part";

        private void SelectionModeEnter(SelectionMode mode)
        {
            if (selectionMode != SelectionMode.None)
                return;

            selectionMode = mode;
            string message = string.Empty;
            switch (mode)
            {
                case SelectionMode.SelectServos:
                    message = $"{LOC_SELECTMODE_SERVO}\n{LOC_SELECTMODE_SELECT}\n{LOC_SELECTMODE_REMOVE}\n{LOC_SELECTMODE_ESC}";
                    break;
                case SelectionMode.SelectEffector:
                    message = $"{LOC_SELECTMODE_EFFECTOR}\n{LOC_SELECTMODE_SELECT}\n{LOC_SELECTMODE_REMOVE}\n{LOC_SELECTMODE_ESC}";
                    break;
                case SelectionMode.SelectPartTarget:
                    message = $"{LOC_SELECTMODE_TARGETPART}\n{LOC_SELECTMODE_SELECT}\n{LOC_SELECTMODE_REMOVE}\n{LOC_SELECTMODE_ESC}";
                    break;
            }

            selectionModeMessage = ScreenMessages.PostScreenMessage(message, float.MaxValue);
            InputLockManager.SetControlLock(ControlTypes.PAUSE, ControlLockID);

            _selectionModeCoroutine = StartCoroutine(SelectionModeCoroutine());
        }

        private Coroutine _selectionModeCoroutine;
        private string _lockID;
        private string ControlLockID => _lockID ?? (_lockID = $"{nameof(ModuleEasyRobotics)}_{GetInstanceID()}");

        private void SelectionModeExit()
        {
            switch (selectionMode)
            {
                case SelectionMode.None:
                    return;
                case SelectionMode.SelectServos:
                    pawServoSelect = false;
                    for (int i = ikChain.Count; i-- > 0;)
                        ikChain[i].BaseServo.part.Highlight(Part.defaultHighlightNone);
                    break;
                case SelectionMode.SelectEffector:
                    pawEffectorSelect = false;
                    if (effectorPart.IsNotNullOrDestroyed())
                        effectorPart.Highlight(Part.defaultHighlightNone);
                    break;
                case SelectionMode.SelectPartTarget:
                    pawTargetSelect = false;
                    if (targetPart.IsNotNullOrDestroyed())
                        targetPart.Highlight(Part.defaultHighlightNone);
                    break;
            }

            selectionModeMessage.duration = 0f;
            selectionModeMessage = null;
            StartCoroutine(RemoveControlLock());
            selectionMode = SelectionMode.None;

            if (_selectionModeCoroutine != null)
                StopCoroutine(_selectionModeCoroutine);

            _selectionModeCoroutine = null;
        }

        private IEnumerator RemoveControlLock()
        {
            while (Input.GetKeyDown(KeyCode.Escape) || Input.GetKey(KeyCode.Escape))
                yield return null;

            InputLockManager.RemoveControlLock(ControlLockID);
        }

        private IEnumerator SelectionModeCoroutine()
        {
            while (SelectionModeCheckInput())
            {
                switch (selectionMode)
                {
                    case SelectionMode.SelectServos:
                        for (int i = ikChain.Count; i-- > 0;)
                            ikChain[i].BaseServo.part.Highlight(Color.red);
                        break;
                    case SelectionMode.SelectEffector:
                        if (effectorPart.IsNotNullOrDestroyed())
                            effectorPart.Highlight(Color.red);
                        break;
                    case SelectionMode.SelectPartTarget:
                        if (targetPart.IsNotNullOrDestroyed())
                            targetPart.Highlight(Color.red);
                        break;
                }

                yield return null;
            }

            SelectionModeExit();
        }

        private bool SelectionModeCheckInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                return false;

            KeyCode pressedKey;

            if (Input.GetKeyDown(KeyCode.Return))
                pressedKey = KeyCode.Return;
            else if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
                pressedKey = KeyCode.Delete;
            else
                return true;

            Part selectedPart = Mouse.HoveredPart;

            switch (selectionMode)
            {
                case SelectionMode.SelectServos:
                {
                    if (selectedPart.IsNullOrDestroyed())
                        break;

                    bool success = false;
                    string message;

                    if (pressedKey == KeyCode.Return)
                    {
                        success = TryAddServo(selectedPart, out message);
                    }
                    else if (TryRemoveServo(selectedPart, out message))
                    {
                        success = true;
                        selectedPart.Highlight(Part.defaultHighlightNone);
                    }

                    PostScreenMessage(message, success ? ScreenMessageType.Message : ScreenMessageType.Warning);
                    break;
                }
                case SelectionMode.SelectEffector:
                {
                    if (pressedKey == KeyCode.Delete && effectorPart.IsNotNullOrDestroyed())
                    {
                        effectorPart.Highlight(Part.defaultHighlightNone);
                        PostScreenMessage("No effector selected", ScreenMessageType.Warning);
                        effectorPart = null;
                        OnEffectorPartChanged(out _);
                        break;
                    }

                    if (selectedPart.IsNullOrDestroyed() || selectedPart == effectorPart)
                        break;

                    Part previousEffector = effectorPart;

                    if (!TryAddEffector(selectedPart, out string message))
                    {
                        PostScreenMessage(message, ScreenMessageType.Warning);
                        break;
                    }

                    if (previousEffector.IsNotNullOrDestroyed())
                        previousEffector.Highlight(Part.defaultHighlightNone);

                    PostScreenMessage(message);
                    return false;
                }
                case SelectionMode.SelectPartTarget:
                {
                    if (pressedKey == KeyCode.Delete && targetPart.IsNotNullOrDestroyed())
                    {
                        targetPart.Highlight(Part.defaultHighlightNone);
                        PostScreenMessage("No target selected", ScreenMessageType.Warning);
                        targetPart = null;
                        OnTargetPartChanged();
                        break;
                    }

                    if (selectedPart.IsNullOrDestroyed() || selectedPart == targetPart)
                        break;

                    if (targetPart.IsNotNullOrDestroyed())
                        targetPart.Highlight(Part.defaultHighlightNone);

                    targetPart = selectedPart;
                    OnTargetPartChanged();
                    PostScreenMessage($"<b>{selectedPart.PartTitle()}</b> selected as target");
                    return false;
                }
            }

            return true;
        }

        private bool TryAddServo(Part potentialServoPart, out string message)
        {
            BaseServo servo = potentialServoPart.FindModuleImplementing<BaseServo>();
            if (servo.IsNullOrDestroyed())
            {
                message = $"No servo on <b>{potentialServoPart.PartTitle()}</b>";
                return false;
            }

            int i = ikChain.Count;
            while (i-- > 0)
            {
                ServoJoint joint = ikChain[i];
                if (joint.BaseServo == servo)
                {
                    message = $"Servo <b>{servo.part.PartTitle()}</b> is already added";
                    return false;
                }

                if (joint.BaseServo.part == effectorPart)
                {
                    message = $"<b>{servo.part.PartTitle()}</b> is the effector";
                    return false;
                }
            }

            ServoJoint newJoint;
            if (servo is ModuleRoboticRotationServo rotationServo)
            {
                newJoint = new RotationServoJoint(rotationServo);
            }
            else if (servo is ModuleRoboticServoHinge hingeServo)
            {
                newJoint = new HingeServoJoint(hingeServo);
            }
            else
            {
                message = $"No rotation servo on <b>{potentialServoPart.PartTitle()}</b>";
                return false;
            }

            ikChain.Add(newJoint);
            OnIKConfigurationBecameInvalid(out string error);
            if (error != null && effectorPart.IsNotNullOrDestroyed())
                PostScreenMessage(error, ScreenMessageType.Error);

            if (IsServoChainLocked(out string lockedError))
                PostScreenMessage(lockedError, ScreenMessageType.Warning);

            message = $"Servo <b>{servo.PartTitle()}</b> added";
            return true;
        }

        private bool TryRemoveServo(Part potentialServoPart, out string message)
        {
            for (int i = ikChain.Count; i-- > 0;)
            {
                if (ikChain[i].BaseServo.part == potentialServoPart)
                {
                    message = $"Servo <b>{potentialServoPart.PartTitle()}</b> removed";

                    ikChain[i].OnDestroy();
                    ikChain.RemoveAt(i);
                    OnIKConfigurationBecameInvalid(out string error);

                    if (error != null && effectorPart.IsNotNullOrDestroyed())
                        PostScreenMessage(error, ScreenMessageType.Error);

                    return true;
                }
            }

            message = $"<b>{potentialServoPart.PartTitle()}</b> isn't in the chain";
            return false;
        }

        private bool TryAddEffector(Part selectedPart, out string message)
        {
            int i = ikChain.Count;
            while (i-- > 0)
            {
                ServoJoint joint = ikChain[i];
                if (joint.BaseServo.part == selectedPart)
                {
                    message = $"<b>{selectedPart.PartTitle()}</b> is a servo";
                    return false;
                }
            }

            effectorPart = selectedPart;
            OnEffectorPartChanged(out string error);
            if (error != null && ikChain.Count > 0)
                PostScreenMessage(error, ScreenMessageType.Error);
            message = $"<b>{selectedPart.PartTitle()}</b> selected as effector";
            return true;
        }

        private enum ScreenMessageType {Message, Warning, Error}

        private static void PostScreenMessage(string message, ScreenMessageType messageType = ScreenMessageType.Message)
        {
            Color color;
            switch (messageType)
            {
                case ScreenMessageType.Warning:
                    color = XKCDColors.Yellow;
                    break;
                case ScreenMessageType.Error:
                    color = XKCDColors.LightRed;
                    break;
                default:
                    color = XKCDColors.LimeGreen;
                    break;
            }

            ScreenMessages.PostScreenMessage(message, 3f, ScreenMessageStyle.UPPER_CENTER, color);
        }


        private Coroutine _trackingCoroutine;

        private bool servosTargetReached;
        private bool ikTargetReached;
        private float targetAngleError;
        private float targetPosError;

        private IEnumerator TrackingCoroutine()
        {
            while (true)
            {
                int jointCount = ikChain.Count;

                servosTargetReached = true;
                for (int i = 0; i < jointCount; i++)
                {
                    if (!ikChain[i].ServoTargetReached())
                    {
                        servosTargetReached = false;
                        break;
                    }
                }

                if (servosTargetReached && !IsEffectorSynced())
                {
                    SyncAllTransforms();
                }
                else
                {
                    if (jointCount > 0)
                        ikChain[jointCount - 1].SyncWithServoTransform(true);

                    if (target != null)
                        OnTargetPositionChanged();
                }


                float maxPosError = 0.025f;
                float maxAngleError = 1.5f;
                ikTargetReached = IsTargetPosReached(maxPosError) && IsTargetRotReached(maxAngleError);

                if (!ikTargetReached)
                {
                    for (int i = 100; i-- > 0;)
                    {
                        for (int j = 0; j < ikChain.Count; j++)
                            ikChain[j].ExecuteGradientDescent(effector, target, CurrentTrackingConstraint, learningRateFactor);

                        ikTargetReached = IsTargetPosReached(maxPosError) && IsTargetRotReached(maxAngleError);
                        if (ikTargetReached)
                        {
                            //iterations = (100 - i).ToString();
                            break;
                        }
                    }

                    for (int i = 0; i < jointCount; i++)
                        ikChain[i].SendRequestToServo();
                }

                targetPosError = GetTargetPosError();
                targetAngleError = GetTargetAngleError();

                yield return Lib.WaitForFixedUpdate;
            }
        }

        private static readonly ServoJoint.Pose[] Poses =
        {
            ServoJoint.Pose.Negative,
            ServoJoint.Pose.Positive
        };

        private static readonly int PosesCount = Poses.Length;

#if DEBUG
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "allow reposition")] [UI_Toggle(affectSymCounterparts = UI_Scene.None)]
#endif
        public bool allowReposition = false;

        private void ExecuteIKOnce()
        {
            SyncAllTransforms();

#if DEBUG
            Stopwatch watch = Stopwatch.StartNew();
#endif
            int jointCount = ikChain.Count;

            if (ExecuteGradientDescent(out int iteration, out float posError, out float angleError))
            {
#if DEBUG
                Debug.Log($"Target reached from current pose, iterations:{iteration} in {watch.Elapsed.TotalSeconds:F3}s for {jointCount} servos, posErr={posError:F2}, angleErr={angleError:F1}");
#endif
            }
            else
            {
#if DEBUG
                Debug.Log($"Target not reached from current pose, iterations:{iteration} in {watch.Elapsed.TotalSeconds:F3}s for {jointCount} servos, posErr={posError:F2}, angleErr={angleError:F1}");
#endif
                if (allowReposition)
                {
                    for (int i = jointCount; i-- > 0;)
                        ikChain[i].SetIKPose(ServoJoint.Pose.Neutral);

                    if (ExecuteGradientDescent(out iteration, out posError, out angleError))
                    {
#if DEBUG
                        Debug.Log($"Target reached from neutral pose, iterations:{iteration} in {watch.Elapsed.TotalSeconds:F3}s for {jointCount} servos, posErr={posError:F2}, angleErr={angleError:F1}");
#endif
                    }
                    else
                    {
                        int[] index = new int[jointCount];
                        float[] bestAngles = new float[jointCount];
                        float bestPosError = float.MaxValue;
                        float bestAngleError = float.MaxValue;
                        int poseCount = 0;
                        int totalIterations = 0;
                        bool solutionFound;
                        while (true)
                        {
                            poseCount++;

                            for (int i = jointCount; i-- > 0;)
                                ikChain[i].SetIKPose(Poses[index[i]]);

                            solutionFound = ExecuteGradientDescent(out iteration, out posError, out angleError);
                            totalIterations += iteration;
                            if (posError < bestPosError && angleError < bestAngleError)
                            {
                                bestPosError = posError;
                                bestAngleError = angleError;
                                for (int j = jointCount; j-- > 0;)
                                {
                                    float angle = ((IRotatingServoJoint)ikChain[j]).RequestedAngle;
                                    bestAngles[j] = angle;
                                }
                            }

                            if (solutionFound)
                            {
                                bestPosError = posError;
                                bestAngleError = angleError;
                                goto EndLoop;
                            }

                            for (int i = jointCount - 1;; i--)
                            {
                                if (i < 0)
                                    goto EndLoop;

                                index[i]++;
                                if (index[i] == PosesCount)
                                    index[i] = 0;
                                else
                                    break;
                            }
                        }

                        EndLoop:

                        if (!solutionFound)
                        {
                            for (int i = jointCount; i-- > 0;)
                            {
                                IRotatingServoJoint joint = ((IRotatingServoJoint)ikChain[i]);
                                ikChain[i].movingTransform.LocalRotation = Quaternion.AngleAxis(bestAngles[i], joint.Axis);
                                joint.RequestedAngle = bestAngles[i];
                            }
                        }
#if DEBUG
                        Debug.Log($"TargetReached={solutionFound}, iterations={totalIterations}, pose={poseCount} in {watch.Elapsed.TotalSeconds:F3}s for {jointCount} servos, posErr={bestPosError:F2}, angleErr={bestAngleError:F1}");
#endif
                    }
                }
            }

            for (int i = 0; i < ikChain.Count; i++)
                ikChain[i].SendRequestToServo();
        }

        private bool ExecuteGradientDescent(out int iteration, out float posError, out float angleError)
        {
            float maxPosErrorSqr = 0.01f * 0.01f;
            float maxAngleErrorNrm = 1f - Mathf.Cos(2.5f * Mathf.Deg2Rad * 0.5f);
            float posErrorSqr = float.MaxValue;
            float angleErrorNrm = float.MaxValue;

            int maxIterations = 2500;
            iteration = 0;
            int chainCount = ikChain.Count;
            int stalledIterations = 0;

            bool solved = false;

            while (iteration < maxIterations)
            {
                for (int j = 0; j < chainCount; j++)
                    ikChain[j].ExecuteGradientDescent(effector, target, CurrentTrackingConstraint, learningRateFactor);

                float newPosErrorSqr = Vector3.SqrMagnitude(effector.Position - target.Position);
                float newAngleErrorNrm = GetTargetAngleErrorNormalized();

                if (newPosErrorSqr <= maxPosErrorSqr && newAngleErrorNrm <= maxAngleErrorNrm)
                {
                    solved = true;
                    break;
                }

                if (newPosErrorSqr > posErrorSqr - 1e-6f && newAngleErrorNrm > angleErrorNrm - 1e-6f)
                {
                    stalledIterations++;
                }
                else
                {
                    posErrorSqr = newPosErrorSqr;
                    angleErrorNrm = newAngleErrorNrm;
                    stalledIterations = 0;
                }

                if (stalledIterations > 25)
                    break;

                iteration++;
            }

            posError = Mathf.Sqrt(posErrorSqr);
            angleError = Mathf.Acos(1f - angleErrorNrm) * Mathf.Rad2Deg * 2f;

            return solved;
        }

        private bool IsEffectorSynced()
        {
            GetEffectorPartPosAndRot(out Vector3 partPos, out _);
            return Lib.IsDistanceLowerOrEqual(partPos, effector.Position, 0.01f);
        }

        private bool IsTargetPosReached(float posThreshold)
        {
            float sqrPosError = Vector3.SqrMagnitude(effector.Position - target.Position);
            return sqrPosError <= posThreshold * posThreshold;
        }

        private float GetTargetPosError() => Vector3.Distance(effector.Position, target.Position);

        private float GetTargetAngleErrorNormalized()
        {
            switch (CurrentTrackingConstraint)
            {
                case TrackingConstraint.PositionAndDirection:
                    return (Vector3.Dot(effector.Up, -target.Up) - 1f) * -0.5f;
                case TrackingConstraint.PositionAndRotation:
                    return 1f - Math.Abs(Quaternion.Dot(effector.Rotation, target.Rotation * upToDown));
                default:
                    return 0f;
            }
        }

        private float GetTargetAngleError()
        {
            switch (CurrentTrackingConstraint)
            {
                case TrackingConstraint.PositionAndDirection:
                    return Vector3.Angle(effector.Up, -target.Up);
                case TrackingConstraint.PositionAndRotation:
                    return Quaternion.Angle(effector.Rotation, target.Rotation * upToDown);
                default:
                    return 0f;
            }
        }

        private bool IsTargetRotReached(float angleThreshold)
        {
            return GetTargetAngleError() <= angleThreshold;
        }

        private void ShowEffectorGizmo(bool show)
        {
            if (show && part.PartActionWindow.IsNotNullOrDestroyed() && part.PartActionWindow.isActiveAndEnabled)
            {
                if (effector == null)
                    return;

                if (effectorGizmo.IsNullOrDestroyed())
                {
                    effectorGizmo = Instantiate(Assets.TargetGizmoPrefab);
                    effectorGizmo.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                    effectorGizmo.GetChild("Y").GetComponent<MeshRenderer>().material.SetColor(Assets.BurnColorID, Color.green);
                }

                effectorGizmo.SetActive(true);
            }
            else if (effectorGizmo.IsNotNullOrDestroyed())
            {
                effectorGizmo.SetActive(false);
            }
        }

        private void ShowTargetGizmo(bool show)
        {
            if (show && part.PartActionWindow.IsNotNullOrDestroyed() && part.PartActionWindow.isActiveAndEnabled)
            {
                if (target == null)
                    return;

                if (targetGizmo.IsNullOrDestroyed())
                {
                    targetGizmo = Instantiate(Assets.TargetGizmoPrefab);
                    targetGizmo.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                    targetGizmo.GetChild("Y").GetComponent<MeshRenderer>().material.SetColor(Assets.BurnColorID, Color.red);
                }

                targetGizmo.SetActive(true);
            }
            else if (targetGizmo.IsNotNullOrDestroyed())
            {
                targetGizmo.SetActive(false);
            }
        }

        private void ShowChainGizmos(bool show)
        {
            for (int i = ikChain.Count; i-- > 0;)
                ikChain[i].ShowGizmo(show);
        }

#if DEBUG
        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Dump ikchain to log")]
        private void DumpIKChainToLog()
        {
            for (int i = 0; i < ikChain.Count; i++)
            {
                ServoJoint sj = ikChain[i];
                IRotatingServoJoint rs = (IRotatingServoJoint)sj;
                Debug.Log($"servo #{i} : pos={sj.baseTransform.Position} rot={sj.baseTransform.Rotation}");
                Debug.Log($"             Axis={rs.Axis}");
                Debug.Log($"             IsAttachedByMovingPart={sj.IsAttachedByMovingPart}");
            }
        }
#endif
    }
}
