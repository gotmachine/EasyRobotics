using System.Collections;
using UnityEngine;
using Vectrosity;
using Camera = UnityEngine.Camera;

namespace EasyRobotics
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    [DefaultExecutionOrder(9000)]
    public class VectrosityCameraHandler : MonoBehaviour
    {
        private void Start()
        {
            // In flight, set the Vectrosity camera to the flight scene camera
            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                GameEvents.OnCameraChange.Add(OnCameraChange);
                VectorLine.SetCamera3D(FlightCamera.fetch.cameras[0]);
            }
            // In editor and space center, set it to main camera
            // In tracking station, KSP will set it to PlanetariumCamera.Camera,
            // so don't do anything
            else if (HighLogic.LoadedScene != GameScenes.TRACKSTATION)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera.IsNotNullOrDestroyed())
                    VectorLine.SetCamera3D(mainCamera);
            }
        }

        private void OnDestroy()
        {
            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
                GameEvents.OnCameraChange.Remove(OnCameraChange);
        }

        // Only called while in flight, we use it to switch back the Vectrosity
        // camera to the planetarium camera when map view is toggled, as KSP
        // doesn't do it automatically and using it for drawing orbit lines
        // Note that this doesn't handle camera switches to the IVA camera, so
        // this can potentially cause issues if another plugin is also messing
        // with Vectrosity
        public void OnCameraChange(CameraManager.CameraMode cameraMode)
        {
            if (cameraMode == CameraManager.CameraMode.Map)
                VectorLine.SetCamera3D(PlanetariumCamera.Camera);
            else
                VectorLine.SetCamera3D(FlightCamera.fetch.cameras[0]);
        }
    }
}
