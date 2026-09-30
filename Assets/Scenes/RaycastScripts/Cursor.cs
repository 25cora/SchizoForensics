using UnityEngine;

public class Cursor : MonoBehaviour
{
    public Transform cursor;
    
    public float maxDistance = 1.0f;


   void Start()
   {
        
   }

   
    void Update()
    {
        if (cursor != null)
        {
            var camTransform = Camera.main.transform;

            RaycastHit raycastHit;
            if (Physics.Raycast(new Ray(camTransform.position, camTransform.forward), out raycastHit, maxDistance))
            {
                cursor.position = raycastHit.point;
                cursor.up = camTransform.forward;
            }
            else
            {
                cursor.position = camTransform.position + camTransform.forward * maxDistance;
                cursor.rotation = Quaternion.LookRotation(-camTransform.forward);
            }
        }
        else
        {
            return;
        }

    }
}
