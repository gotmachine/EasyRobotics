# EasyRobotics

### Overview

EasyRobotics is an **inverse kinematics** (IK) controller for the KSP Breaking Grounds DLC rotation and hinge servo parts. It allow to setup an **IK chain** made of several **servos** and of an **effector**. The controller adjust the servos target angle so the effector automatically reach a position and/or rotation in space which can be either manually defined or automatically set from a **target** part.

EasyRobotics is available from the Part Action Window (PAW) for the KAL-1000 part, as well as any modded part featuring the stock robotic controller.

### Download and installation

Compatible with **KSP 1.12.3** to **1.12.5** - Available on CKAN

**Manual installation**

- Go to the **[GitHub release page](https://github.com/gotmachine/EasyRobotics/releases)** and download the file named `EasyRobotics_x.x.x.zip`
- Open the downloaded *.zip archive
- Open the `GameData` folder of your KSP installation
- Delete any existing `EasyRobotics` folder in your `GameData` folder
- Copy the `EasyRobotics` folder found in the archive into your `GameData` folder

### License

MIT

### User manual

<img align="left" width="42" height="58" src="https://raw.githubusercontent.com/gotmachine/EasyRobotics/master/Images/KAL-1000.png">

All controls are available from the KAL-1000 Part Action Window, both in the editor and in flight.
<br/>The KAL-1000 part can be placed anywhere, but it must stay on the same vessel as the servos and effector parts, something to keep in mind for "walking arms" designs such as the ISS canadarm.

<img src="https://raw.githubusercontent.com/gotmachine/EasyRobotics/master/Images/PAW.png ">

#### IK chain setup
The first step for using the IK controller is to select which servos will be used, as well as which part is to be used as the effector.
This can be done from the `IK Configuration` section :



- `Select servos` : Enter the servo selection mode. Hover the mouse on a servo part, then press `ENTER` to add it, or `DELETE` to remove it. To get out of the selection mode, either click again on the button or press `ESC`. You may select as many servos as you want and in any order, but they must form a single "chain". Selected servos and their rotation axis can be visualized by enabling the `Servo gizmos` toggle.
- `Effector` : Enter the effector selection mode, similar controls as for the servo selection. You must have a single effector, and it can't be in the middle of the servo "chain".
- `Effector node, direction and offset` : Allow to change the controlled position and direction on the effector part, which can be visualized as green gizmo by enabling `Target/effector gizmos`
- `Learning rate` : The IK algorithm works by trying to move the servos a little bit to see if that new pose is closer or further away from the target. The learning rate is how big those small moves are. Increasing it can greatly help the algorithm to find a solution, but lowering it can be necessary to avoid the solution "jittering" around the target. How that value should be set depends on the chain configuration, it is recommended to do some editor-time testing to tune it.

#### IK Execution Control


Once the servos and effector are properly configured, the `Status` label in the `IK Execution Control` tab should show `ready`. That label will also indicate what the IK controller state is when it is active.

- `Control mode`
  - `Free` : The target (red gizmo) position is relative to the first servo of the chain. 
  - `Target` : The target is a part (see the "IK target" section below).
- `Tracking mode`
  - `Continous` : Unlock the `Tracking` toggle. When enabled, the IK controller is always active and will continously try to reach the target. 
  - `On request` : Unlock the `Request execution` button, allowing you to request a single execution of the IK controller.
- `Constraint`
  - `Position` : The effector will try to reach the target positon from any angle.
  - `Pos+Direction` : The effector will also try to match the direction of the target, but ignoring its "roll".
  - `Pos+Rotation` : The effector will try to match the full rotation of the target, including its "roll".
- `Position range` :
  - `Automatic` : The min/max value of the target position sliders will be set based on the robotic chain length.
  - `Value in m` : Custom min/max target position sliders value
- `Right/left`, `Up/down`, `Forward/back` : sliders controlling the absolute position of the target. The axes are relative to the current target rotation, and the value is relative to either the first servo in the `Free` control mode, or the targetted part in the `Target` control mode.
- `Pitch offset`, `Yaw offset`, `Roll offset` : incremental rotation offsets allowing to rotate the target freely.
- `Reset pos/rot offsets` : Reset all position and rotation offsets.
- `Reset all servos positions` : Instruct all controlled servos to go back to their original build angle.

#### IK Target
Allow to select a part as target, to be used with the `Target` control mode. Like for the effector, you can select an arbitrary node and orientation.

### Changelog

#### 1.0.0 - 15/05/2024
