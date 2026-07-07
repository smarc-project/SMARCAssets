# SMARC Assets
This is a package containing all of the SMARC Unity assets and scripts including vehicles, dynamics, sensors and more. 

## Quick start

### Independently of SMaRC2
```
cd anywhere
git clone git@github.com:smarc-project/SMARCUnity.git
git clone git@github.com:smarc-project/SMARCAssets.git
```

### As part of SMaRC2
You can do the above, OR this:
```
cd smarc2
git submodule update --remote --init simulation/SMARCUnity
git submodule update --remote --init simulation/SMARCAssets
```

### Common for both:
Run Unity Hub:
  - Open Project: Navigate to SmarcUnity
  - Run SmarcUnity
  - Open a scene under `Assets/Scenes/`
  - Play

You can find more detailed instructions on using all available vehicles, sensors, connections, UIs etc. [here](./Documentation/README.md).


## ROS connection
- We use the [ROS-TCP-Endpoint](https://github.com/KKalem/ROS-TCP-Endpoint) package to speak to ROS2. 
  - You can use [this simple script](../scripts/unity_ros_bridge.sh) to run the bridge and then use `rviz2` and `rqt` to check what things look like in ROS.
- The ROS connection is especially useful when you are running headless.


### ROS Messages
These are generated from within the editor:
- Robotics -> Generate ROS Messages...
- Fill in the fields in the pop-up
  - Usually you can not generate these INTO the SMARCAssets package, so place the RosMessages folder anywhere for now
- Cut/Paste the generated RosMessages folder into `SMARCAssets/Runtime/Scripts/VehicleComponents/ROS/Core/RosMessages`


## How to cite
```
@INPROCEEDINGS{11139391,
  author={Kartašev, Mart and Dörner, David and Özkahraman, Özer and Ögren, Petter and Stenius, Ivan and Folkesson, John},
  booktitle={2025 Symposium on Maritime Informatics and Robotics (MARIS)}, 
  title={SMaRCSim: Maritime Robotics Simulation Modules}, 
  year={2025},
  volume={},
  number={},
  pages={1-4},
  keywords={Learning systems;Heuristic algorithms;Games;Planning;Vehicle dynamics;Informatics;Robots;Engines;Physics;Testing;Simulation;multi-domain;AUVs;learning-based methods;mission-planning},
  doi={10.1109/MARIS64137.2025.11139391}}
```


## Help!
If at any point, you needed help, got the help, and the help was not about using Unity (Good example:"How do I make my vehicle buoyant?". Bad example: "How do I move this object in the editor?"), please [open an issue in github](https://github.com/smarc-project/SMARCUnityAssets/issues) (and tag it with "Documentation" if you can). 
That way, we can identify gaps in our documentation and hopefully add them in for the next person.


# You are probably done here. Continue if this is not your first ever Unity project.


## Installation for non-SMaRC projects

We rely on a few non unity asset store packages, so follow the instructions below to install the package into your project.
It might seem like a lot of things to do, but all of the operations can be done in the Unity editor and should not take more than a few minutes.

We recommend using the the Editor Version **2023.1.13f1**. 


### Install Packages for Unity from Github

1. Open Package Manager window (Window | Package Manager)
2. Click `+` button on the upper-left of a window, and select "Add package from git URL..."
3. Enter the following URL and click `Add` button

```
https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
```
```
https://github.com/Unity-Technologies/ROS-TCP-Connector.git?path=/com.unity.robotics.ros-tcp-connector
```
```
https://github.com/Unity-Technologies/URDF-Importer.git?path=/com.unity.robotics.urdf-importer
```

### Install NuGet packages

Once you have installed NuGet, install the following packages.

1. Click the NuGet dropdown at the top of your Unity Editor (Sometimes requires editor restart to appear).
2. Manage NuGet packages
3. Search and install:
  *  MathNet.Numerics
  *  CoordinateSharp
  *  CsvHelper
  *  Microsoft.Data.Sqlite

For ROSBag playback, use the SQLite package set known to work in Unity with NuGetForUnity:
- `Microsoft.Data.Sqlite` `9.0.7`
- `SQLitePCLRaw.bundle_e_sqlite3` `2.1.10`

`Microsoft.Data.Sqlite` is required for the generic ROSBag playback tooling shipped in `SMARCAssets`.
`CsvHelper` is still needed by the SAM-specific ROSBag export utilities that remain in `SMARC-RL`.

### Configure the ROS Connector

Our codebase is scripted towards ROS 2. You will need to change some default settings to ensure your messages are compiled for ROS 2.
The code will work with ROS 1, but you will need to update the scripts to support ROS 1 messages yourself.

1. Open the ROS settings menu (Robotics | ROS Settings)
2. Change the `Protocol` to "ROS 2"
3. Click on "Apply"
4. Wait for the compilation to finish.

### Install SMARC Unity Assets

Once all the dependencies are installed and configured, you can install this package using the same method as before.

1. Open Package Manager window (Window | Package Manager)
2. Click `+` button on the upper-left of a window, and select "Add package from git URL..."
3. Enter the following URL and click `Add` button

```
https://github.com/smarc-project/SMARCUnityAssets
```
