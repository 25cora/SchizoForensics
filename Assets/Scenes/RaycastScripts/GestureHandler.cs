using Microsoft.MixedReality.Toolkit.Input;
using System;
using UnityEngine;

public class GestureHandler : MonoBehaviour, IMixedRealityGestureHandler<Vector3>
{
    public GameObject Object;

    [SerializeField]
    private MixedRealityInputAction tapAction = MixedRealityInputAction.None;

    public void OnGestureCanceled(InputEventData eventData)
    {
        throw new System.NotImplementedException();
    }

    public void OnGestureCompleted(InputEventData<Vector3> eventData)
    {
        throw new System.NotImplementedException();
    }

    public void OnGestureCompleted(InputEventData eventData)
    {
        MixedRealityInputAction action = eventData.MixedRealityInputAction;
        if (action == tapAction)
        {
            TapActionHandler();
        }
    }

    public void OnGestureStarted(InputEventData eventData)
    {
        throw new System.NotImplementedException();
    }

    public void OnGestureUpdated(InputEventData<Vector3> eventData)
    {
        throw new System.NotImplementedException();
    }

    public void OnGestureUpdated(InputEventData eventData)
    {
        throw new System.NotImplementedException();
    }

    private void TapActionHandler()
    {
        if (Object == null) return;

        var CameraCursor = Camera.main.GetComponent<Cursor>().cursor;

        var distance = new System.Random().Next(2, 50);

        var location = CameraCursor.position + CameraCursor.forward * distance/10;

        // Left - Right position
        if (new System.Random().Next(1, 3) == 1)
        {
            distance = new System.Random().Next(2, 50);
            location = location + Vector3.left * distance/10;
        }
        else
        {
            distance = new System.Random().Next(2, 50);
            location = location + Vector3.right * distance / 10;
        }
        // Up - Down position
        if (new System.Random().Next(1, 3) == 1)
        {
            distance = new System.Random().Next(2, 50);
            location = location + Vector3.up * distance / 10;
        }
        else
        {
            distance = new System.Random().Next(2, 50);
            location = location + Vector3.down * distance / 10;
        }

        Instantiate(Object, location, Quaternion.Euler(0, 180, 0));
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }
}
