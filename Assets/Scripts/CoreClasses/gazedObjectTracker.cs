
using UnityEngine;

public class gazedObjectTracker : MonoBehaviour
{

    private static gazedObjectTracker instance;
    public manipObject gazedAtManipObject;
    public Vector3 correction;
    public GameObject gazeIndicator;
    public GameObject calibrationPlane;
    public GameObject calibrationPlaneCenter;

    void Awake()
    {
      // Check if instance already exists and set it if it doesn't
      if (instance == null)
      {
        instance = this;
      }
      else if (instance != this)
      {
        // Destroy this instance because it is a duplicate
        Destroy(gameObject);
      }

      // Optionally, persist this instance across scenes
      // DontDestroyOnLoad(gameObject);
    }

    public static gazedObjectTracker Instance
    {
      get
      {        
        return instance;
      }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

  // Update is called once per frame
  void Update()
  {
    // reset every frame
    gazedAtManipObject = null;
    gazeIndicator.SetActive(false);

    Ray ray = new Ray(transform.position, transform.forward);
    RaycastHit hit;
    int layerMask = 1 << 9; // layerMask 9 = manipOnly

    if (Physics.Raycast(ray, out hit, 2f, layerMask)){ 

      // if looking at calib plane and one trigger is pressed
      if (hit.collider.gameObject == calibrationPlane)
      {
        
        gazeIndicator.transform.position = hit.point;
        gazeIndicator.SetActive(isHalfPressed());

        if (Time.frameCount % 30 == 0 && isFullPressed())
        {
          Vector3 localizedHitPoint = transform.InverseTransformPoint(hit.point);
          Vector3 localizedPlaneCenter = transform.InverseTransformPoint(calibrationPlaneCenter.transform.position);
          correction.x = localizedPlaneCenter.x - localizedHitPoint.x;
          correction.y = localizedPlaneCenter.y - localizedHitPoint.y;
        }

        return;
      }
    }

    transform.Translate(correction); 

    ray = new Ray(transform.position, transform.forward);
    layerMask = 1 << 9; // layerMask 9 = manipOnly

    gazeIndicator.SetActive(false);

    if (Physics.Raycast(ray, out hit, 2f, layerMask))
    {
      manipObject targetObject = hit.collider.GetComponent<manipObject>();
      if (targetObject != null)
      {
        gazedAtManipObject = targetObject;
        gazeIndicator.transform.position = hit.point;
        gazeIndicator.SetActive(isHalfPressed());
      }

    }
  }

  bool isHalfPressed(){
    return (Input.GetAxis("triggerL") > 0.05 || Input.GetAxis("triggerR") > 0.05);
  }

  bool isFullPressed(){
    return (Input.GetAxis("triggerL") > 0.7 || Input.GetAxis("triggerR") > 0.7);
  }
}

