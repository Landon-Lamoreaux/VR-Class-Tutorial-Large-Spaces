using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TeleportationLocomotion : MonoBehaviour
{
    // Max number of segments in the parabola.
    [SerializeField]
    private int maxSegments = 30;

    // How much outward push.
    [SerializeField]
    private float force = 10;
    [SerializeField]
    private float lineSize = 0.1f;
    [SerializeField]
    private Color legalTeleportColor = Color.green;
    [SerializeField]
    private Color illegalTeleportColor = Color.magenta;

    // Reference to the parabola line.
    private LineRenderer line;

    // How close are the points, make smaller for smoother curves.
    [SerializeField]
    private float stepSize = 0.1f;

    // Hit information for this frame.
    private RaycastHit parabolaIntersection;

    // Current points in the parabola.
    private Vector3[] linePoints = new Vector3[2];

    // Flag to disallow doubling up of teleportation commands.
    private bool isTeleporting = false;

    // Player rig parameters.
    private GameObject feet;
    private GameObject head;
    private GameObject rightHand;
    private GameObject leftHand;
    private GameObject cameraOffset;

    // Super speed parameters.
    [SerializeField]
    private float teleportTime = 0.2f;
    private float startTime = 0;
    private Vector3 startLoc = Vector3.zero;

    [SerializeField]
    private GameObject decal;

    [SerializeField]
    private int maxSteepness = 20;

    [SerializeField]
    [Tooltip("If left at zero, the head's collider's distance will be used.")]
    private float minDistanceFromWall = 0;

    [SerializeField]
    private float strafeSpeed = 0.2f;
    [SerializeField]
    private int correctionIterations = 8;

    [SerializeField]
    private float turnSpeed = 20f;

    private void AttemptSideStep(Vector3 step)
    {
        // (1A) Where would our teleport point be.
        Vector3 newLoc = head.transform.position + step * strafeSpeed;
        RaycastHit hit;
        Physics.Raycast(newLoc, Vector3.down, out hit);
        // (1B) If legal to teleport, teleport!
        if (IsLegalToTeleport(hit))
        {
            TeleportTo(hit.point);
        }
        else
        {
            // (3) Binary search for legal teleport in between.
            Vector3 correctedLoc = head.transform.position;
            float magnitude = strafeSpeed / 2;
            for (int i = 1; i < correctionIterations; i++)
            {
                // Determine no teleport location.
                newLoc = head.transform.position + step * magnitude;
                Physics.Raycast(newLoc, Vector3.down, out hit);
                // If legal to teleport, try to move forward by half again.
                if (IsLegalToTeleport(hit))
                {
                    magnitude = magnitude + strafeSpeed / Mathf.Pow(2, i + 1);
                    correctedLoc = hit.point;
                }
                // If not legal to teleport, try to move backwards by half again.
                else
                {
                    magnitude = magnitude - strafeSpeed / Mathf.Pow(2, i + 1);
                }
            }
            TeleportTo(correctedLoc);
        }
    }


    // Start is called before the first frame update.
    void Start()
    {
        // Setup actions.
        PlayerInput filter = FindObjectOfType<PlayerInput>();
        if (filter == null)
        {
            Debug.Log("No player input in the scene. Teleportation will not work");
        }
        else
        {
            filter.actions["PrepTeleport"].performed += OnPrepTeleport;
            filter.actions["PostTeleport"].performed += OnPostTeleport;

            filter.actions["Turn"].performed += OnTurn;
            filter.actions["Walk"].performed += OnWalk;
            filter.actions["Strafe"].performed += OnStrafe;
        }

        // Find player params.
        head = Camera.main.gameObject;
        feet = GameObject.FindWithTag("Player");
        rightHand = GameObject.FindWithTag("Right Hand");
        leftHand = GameObject.FindWithTag("Left Hand");
        cameraOffset = GameObject.FindWithTag("Camera Offset");

        // Ceate the line.
        line = gameObject.AddComponent<LineRenderer>();
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.material = new Material(Shader.Find("Sprites/Default"));
        linePoints = new Vector3[maxSegments];

        // Apply the user’s preferences.
        line.positionCount = maxSegments;
        line.startWidth = lineSize;
        line.endWidth = lineSize;
        line.startColor = legalTeleportColor;
        line.endColor = legalTeleportColor;

        // Hide for now.
        line.enabled = false;

        // Create the decal.
        if (decal != null)
        {
            decal = Instantiate(decal);
            decal.SetActive(false);
        }

        // Sanity check. Nothing closer than the head.
        if (minDistanceFromWall < head.GetComponent<SphereCollider>().radius)
            minDistanceFromWall = head.GetComponent<SphereCollider>().radius;

    }

    public void OnTurn(InputAction.CallbackContext inputValue)
    {
        //float input = inputValue.ReadValue<float>();
        float input;
        if (inputValue.control.path.Contains("Keyboard"))
            input = inputValue.ReadValue<float>();
        else
        {
            input = inputValue.ReadValue<Vector2>().y;
            input = Mathf.RoundToInt(input);
        }

        float direction = -input * turnSpeed;

        // Shift to "origin".
        Vector3 point = feet.transform.position - head.transform.position;

        // Rotate around "origin" to find the new location.
        float theta = direction * Mathf.Deg2Rad;
        Vector3 newPoint2 = new Vector3(point.x * Mathf.Cos(theta) - point.z * Mathf.Sin(theta),
        point.y,
        point.z * Mathf.Cos(theta) + point.x * Mathf.Sin(theta));

        // Shift back.
        Vector3 point2 = head.transform.position + newPoint2;
        feet.transform.position = point2;

        // Apply rotate in revese to place head in the same spot.
        feet.transform.Rotate(new Vector3(0, -direction, 0));
    }
    public void OnWalk(InputAction.CallbackContext inputValue)
    {
        //float input = inputValue.ReadValue<float>();
        float input;
        if (inputValue.control.path.Contains("Keyboard"))
            input = inputValue.ReadValue<float>();
        else
        {
            input = inputValue.ReadValue<Vector2>().y;
            input = Mathf.RoundToInt(input);
        }

        // Figure out our direction.
        Vector3 forward = new Vector3(head.transform.forward.x, 0, head.transform.forward.z);
        forward.Normalize();
        AttemptSideStep(forward * input);
    }
    public void OnStrafe(InputAction.CallbackContext inputValue)
    {
        // Float input = inputValue.ReadValue<float>();
        float input;
        if (inputValue.control.path.Contains("Keyboard"))
            input = inputValue.ReadValue<float>();
        else
        {
            input = inputValue.ReadValue<Vector2>().x;
            input = Mathf.RoundToInt(input);
        }

        // Figure out our direction.
        Vector3 right = new Vector3(head.transform.right.x, 0, head.transform.right.z);
        right.Normalize();
        AttemptSideStep(right * input);

    }

    // Update is called once per frame.
    public void Update()
    {
        if (line.enabled)
        {
            UpdateLine();

            // Update feedback.
            if (IsLegalToTeleport(parabolaIntersection))
            {
                if (decal != null)
                {
                    decal.SetActive(true);

                    // Place at point, just a little above the plane.
                    decal.transform.position = parabolaIntersection.point + parabolaIntersection.normal * 0.01f;
                    decal.transform.rotation = Quaternion.LookRotation(-parabolaIntersection.normal);
                }

                line.startColor = legalTeleportColor;
                line.endColor = legalTeleportColor;
            }
            else
            {
                if (decal != null)
                {
                    // Turn off.
                    decal.SetActive(false);
                }

                line.startColor = illegalTeleportColor;
                line.endColor = illegalTeleportColor;
            }
        }

        if (isTeleporting)
        {
            // Move to next position.
            startTime += Time.deltaTime;
            Vector3 target = Vector3.Lerp(startLoc, parabolaIntersection.point, startTime / teleportTime);
            TeleportTo(target);

            // Check to see if movemetn has ended, and allow a new teleport.
            if (startTime > teleportTime)
                isTeleporting = false;
        }
    }
    private void TeleportTo(Vector3 target)
    {
        // Offset between play area and head location, same as before.
        Vector3 difference = feet.transform.position - head.transform.position;
        difference.y = 0;

        // Fix up ground height if going over bumps.
        RaycastHit groundHit;
        float playerHeight = head.transform.position.y - feet.transform.position.y;
        if (Physics.Raycast(head.transform.position, Vector3.down, out groundHit))
        {
            // Found a bump, change starting height to this height to smooth the efect.
            if (groundHit.distance < playerHeight)
            {
                startLoc.y = groundHit.point.y;
            }
        }
        // Final position, same as before.
        feet.transform.position = target + difference;
    }

    private void UpdateLine()
    {
        // Find starting point based on the object this is on.
        Vector3 startPoint = rightHand.transform.forward * rightHand.transform.lossyScale.z / 2;
        Vector3 currentPoint = startPoint + rightHand.transform.position;

        // Calculate initial a and v based on force.
        Vector3 a = Physics.gravity;
        Vector3 v = rightHand.transform.forward * force;

        // Do Euler steps.
        line.positionCount = maxSegments;
        for (int i = 0; i < maxSegments; i++)
        {
            linePoints[i] = currentPoint;
            v = v + a * stepSize;
            currentPoint = currentPoint + v * stepSize;
            
            // Ready to calculate hit point.
            if (i > 0)
            {
                // Figure out ray, and check.
                Ray r = new Ray(linePoints[i - 1], linePoints[i] - linePoints[i - 1]);
                float distance = (linePoints[i] - linePoints[i - 1]).magnitude;
                if (Physics.Raycast(r, out parabolaIntersection, distance))
                {
                    //stop on first hit
                    linePoints[i] = parabolaIntersection.point;
                    line.positionCount = i + 1;
                    break;
                }
            }
        }
        // Give the line render this position this frame.
        line.SetPositions(linePoints);
    }

    public void OnPrepTeleport(InputAction.CallbackContext context)
    {
        if (!isTeleporting)
            line.enabled = true;
    }

    public void OnPostTeleport(InputAction.CallbackContext context)
    {
        if(IsLegalToTeleport(parabolaIntersection))
        {
            // Note that we are currently moving.
            isTeleporting = true;
            startTime = 0;

            // Find starting ground height.
            RaycastHit hitGround;
            if (Physics.Raycast(head.transform.position, Vector3.down, out hitGround))
            {
                startLoc = hitGround.point;
            }
            line.enabled = false;
        }

        // Turn off the decal.
        if (decal != null)
            decal.SetActive(false);
    }

    private bool IsLegalToTeleport(RaycastHit hit)
    {
        // We have a intersection.
        if (hit.collider != null)
        {
            // Check how flat the interaction plane is.
            float groundAngle = Mathf.Abs(Vector3.Angle(Vector3.up, hit.normal));
            Vector3 newHeadPoint = new Vector3(hit.point.x, hit.point.y + head.transform.position.y - feet.transform.position.y, hit.point.z);
            float distance = GetClosestPointForHeadAt(newHeadPoint, minDistanceFromWall);

            // If too steep, and not currently teleporting, this location is OK!
            if (groundAngle < maxSteepness && !isTeleporting && distance > minDistanceFromWall)
            {
                return true;
            }
        }
        return false;
    }


    
    // (4) helper function to convert the list of layers into a bit mask needed for physics casts
    private int MakeLayerMask(int[] list)
    {
        int mask = 0;
        foreach (int layer in list)
        {
            int bitPosition = 1 << layer;
            mask |= bitPosition;
        }
        return mask;
    }
    private float GetClosestPointForHeadAt(Vector3 location, float maxCheckDistance)
    {
        // Assume no hit.
        float distance = float.PositiveInfinity;
        
        // (1) Calculate step size
        float circumference = 2 * Mathf.PI * maxCheckDistance;
        float headSize = head.GetComponent<SphereCollider>().radius;
        float stepSize = (2 * Mathf.PI * 2 * headSize) / (circumference);
        float rayLength = maxCheckDistance - headSize;
        
        // (2) Check initial head area, check all layers BUT the ignore raycast layer
        Collider[] hitObjs = Physics.OverlapSphere(location, headSize, ~MakeLayerMask(new int[] { 2 }));
        if (hitObjs.Length > 0)
        {
            foreach (Collider hitGround in hitObjs)
            {
                Vector3 hitPoint = hitGround.ClosestPoint(head.transform.position);
                float distanceToHead = (hitPoint - head.transform.position).magnitude -
                head.GetComponent<SphereCollider>().radius;
                //Debug.Log("Immediate Hit: " + hitGround.name);
                return distanceToHead;
            }
        }
        // (3) Shoot out a sphere from the head to the max distance each step along the peremeter
        // only works if the hit object is past the original sphere position.
        float angle = 0;
        while (angle < 2 * Mathf.PI)
        {
            // Check along “thick ray”, check all layers BUT the ignore raycast layer.
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            RaycastHit hitGround;
            if (Physics.SphereCast(location, headSize, dir, out hitGround, rayLength, ~MakeLayerMask(new int[] { 2 })))
            {
                Debug.Log("Hit: " + hitGround.collider.name);
                
                // Shorter distance?
                if (hitGround.distance < distance)
                    distance = hitGround.distance;
            }
            angle += stepSize;
        }
        return distance;
    }
    


    public void OnDrawGizmos()
    {
        // Show only during run, by checking if a variable is set.
        if (head != null)
        {
            Vector3 right = new Vector3(head.transform.right.x, 0, head.transform.right.z);
            right.Normalize();
            Vector3 forward = new Vector3(head.transform.forward.x, 0, head.transform.forward.z);
            forward.Normalize();
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Gizmos.DrawCube(head.transform.position + forward, new Vector3(.1f, .1f, .1f));
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawCube(head.transform.position + right, new Vector3(.1f, .1f, .1f));
            Gizmos.color = new Color(0, 0, 0, 0.5f);
            Gizmos.DrawSphere(feet.transform.position, 0.3f);
        }
    }
}
