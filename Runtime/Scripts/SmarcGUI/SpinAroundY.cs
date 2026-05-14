using UnityEngine;

public class SpinAroundY : MonoBehaviour
{
	[SerializeField] private float secondsPerRotation = 5f;

	private void Update()
	{
		if (secondsPerRotation <= 0f)
		{
			return;
		}

		float degreesPerSecond = 360f / secondsPerRotation;
		transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);
	}
}
