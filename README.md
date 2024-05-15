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

#### IK chain setup
The first step for using the IK controller is to select which servos will be used, as well as which part is to be used as the effector.
This can be done from the `IK Configuration` section :

<img align="left" hspace="10" src="https://raw.githubusercontent.com/gotmachine/EasyRobotics/master/Images/IKConfig.png ">

- `Select servos` : Enter the servo selection mode. Hover the mouse on a servo part, then press `ENTER` to add it, or `DELETE` to remove it. To get out of the selection mode, either click again on the button or press `ESC`. You may select as many servos as you want and in any order, but they must form a single "chain". Selected servos and their rotation axis can be visualized by enabling the `Servo gizmos` toggle.
- `Effector` : Enter the effector selection mode, similar controls as for the servo selection. You must have a single effector, and it can't be in the middle of the servo "chain".
- `Effector node, direction and offset` : Allow to change the controlled position and direction on the effector part, which can be visualized as green gizmo by enabling `Target/effector gizmos`
- `Learning rate` : The IK algorithm works by trying to move the servos a little bit to see if that new pose is closer or further away from the target. The learning rate is how big those small moves are. Increasing it can greatly help the algorithm to find a solution, but lowering it can be necessary to avoid the solution "jittering" around the target. How that value should be set depends on the chain configuration, it is recommended to do some editor-time testing to tune it.

#### IK Execution Control
Once the servos and effector are properly configured, the `Status` label in the `IK Execution Control` tab should show `ready`. That label will also indicate what the IK controller state is when it is active.

<img align="left" hspace="10" src="https://raw.githubusercontent.com/gotmachine/EasyRobotics/master/Images/IKExec.png ">

- `Control mode` : Change how the target (red gizmo) is controled. `Free` means the target position is relative to the first servo of the chain. `Target` means the target a part (see the "IK target" section below).
- `Tracking mode` :
  - `Continous` unlock the `Tracking` toggle. When enabled, the IK controller is always active and will continously try to reach the target. 
  - `On request` unlock the `Request execution` button, allowing you to request a single execution of the IK controller. Requesting multiple executions is usually necessary for a solution to be found.


### Changelog

#### 1.0.0 - 15/05/2024
