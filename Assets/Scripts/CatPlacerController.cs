using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GoogleARCore;
using GoogleARCore.HelloAR;

public class CatPlacerController : MonoBehaviour
{
	/// <summary>
	/// The first-person camera being used to render the passthrough camera.
	/// </summary>
	public Camera m_firstPersonCamera;

	/// <summary>
	/// A prefab for tracking and visualizing detected planes.
	/// </summary>
	public GameObject m_trackedPlanePrefab;

	private List<TrackedPlane> m_newPlanes = new List<TrackedPlane>();


    /// <summary>
    /// Our cat to place when a raycast from a user touch hits a plane.
    /// </summary>
    public GameObject kittenPrefab;

	/// <summary>
	/// The Unity Update() method.
	/// </summary>
	public void Update()
	{
		_QuitOnConnectionErrors();

		// The tracking state must be FrameTrackingState.Tracking in order to access the Frame.
		if (Frame.TrackingState != FrameTrackingState.Tracking)
		{
			const int LOST_TRACKING_SLEEP_TIMEOUT = 15;
			Screen.sleepTimeout = LOST_TRACKING_SLEEP_TIMEOUT;
			return;
		}

		Screen.sleepTimeout = SleepTimeout.NeverSleep;
		Frame.GetNewPlanes(ref m_newPlanes);

		// Iterate over planes found in this frame and instantiate corresponding GameObjects to visualize them.
		for (int i = 0; i < m_newPlanes.Count; i++)
		{
			// Instantiate a plane visualization prefab and set it to track the new plane. The transform is set to
			// the origin with an identity rotation since the mesh for our prefab is updated in Unity World
			// coordinates.
			GameObject planeObject = Instantiate(m_trackedPlanePrefab, Vector3.zero, Quaternion.identity,
				transform);
            planeObject.GetComponent<CatPlaneVisualizer>().SetTrackedPlane(m_newPlanes[i]);

			// Apply a random grid rotation.
			planeObject.GetComponent<Renderer>().material.SetFloat("_UvRotation", Random.Range(0.0f, 360.0f));
		}


		Touch touch;
		if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
		{
			return;
		}

		TrackableHit hit;
		TrackableHitFlag raycastFilter = TrackableHitFlag.PlaneWithinBounds | TrackableHitFlag.PlaneWithinPolygon;

		if (Session.Raycast(m_firstPersonCamera.ScreenPointToRay(touch.position), raycastFilter, out hit))
		{
			// Create an anchor to allow ARCore to track the hitpoint as understanding of the physical
			// world evolves.
			var anchor = Session.CreateAnchor(hit.Point, Quaternion.identity);

			// Intantiate our cat t as a child of the anchor; it's transform will now benefit
			// from the anchor's tracking.
            var catObject = Instantiate(kittenPrefab, hit.Point, Quaternion.identity,
				anchor.transform);

			// Oure Kitten should look at the camera but still be flush with the plane.
			catObject.transform.LookAt(m_firstPersonCamera.transform);
			catObject.transform.rotation = Quaternion.Euler(0.0f,
				catObject.transform.rotation.eulerAngles.y, catObject.transform.rotation.z);

			// Use a plane attachment component to maintain Andy's y-offset from the plane
			// (occurs after anchor updates).
			catObject.GetComponent<PlaneAttachment>().Attach(hit.Plane);
		}
	}

	/// <summary>
	/// Quit the application if there was a connection error for the ARCore session.
	/// </summary>
	private void _QuitOnConnectionErrors()
	{
		// Do not update if ARCore is not tracking.
		if (Session.ConnectionState == SessionConnectionState.DeviceNotSupported)
		{
			_ShowAndroidToastMessage("This device does not support ARCore.");
			Application.Quit();
		}
		else if (Session.ConnectionState == SessionConnectionState.UserRejectedNeededPermission)
		{
			_ShowAndroidToastMessage("Camera permission is needed to run this application.");
			Application.Quit();
		}
		else if (Session.ConnectionState == SessionConnectionState.ConnectToServiceFailed)
		{
			_ShowAndroidToastMessage("ARCore encountered a problem connecting.  Please start the app again.");
			Application.Quit();
		}
	}

	/// <summary>
	/// Show an Android toast message.
	/// </summary>
	/// <param name="message">Message string to show in the toast.</param>
	/// <param name="length">Toast message time length.</param>
	private static void _ShowAndroidToastMessage(string message)
	{
		AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
		AndroidJavaObject unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

		if (unityActivity != null)
		{
			AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
			unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
			{
				AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity,
					message, 0);
				toastObject.Call("show");
			}));
		}
	}
}

