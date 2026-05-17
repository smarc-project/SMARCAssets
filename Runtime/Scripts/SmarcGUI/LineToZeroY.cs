using UnityEngine;

public class LineToZeroY : MonoBehaviour
{
	public Material lineMaterial;
	LineRenderer lineRenderer;

	void Start()
	{
		lineRenderer = gameObject.AddComponent<LineRenderer>();
		lineRenderer.material = lineMaterial;
		lineRenderer.generateLightingData = true;
		lineRenderer.receiveShadows = false;
		lineRenderer.positionCount = 2;
		lineRenderer.SetPosition(0, transform.position);
		lineRenderer.SetPosition(1, new Vector3(transform.position.x, 0f, transform.position.z));
	}
	void Update()
	{
		lineRenderer.SetPosition(0, transform.position);
		lineRenderer.SetPosition(1, new Vector3(transform.position.x, 0f, transform.position.z));
	}
}
