using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DemoPanel : MonoBehaviour
{
    public Transform _player;
    public EventTrigger _upBtn;
    public EventTrigger _downBtn;
    public EventTrigger _leftBtn;
    public EventTrigger _rightBtn;
    public EventTrigger _leftRotateBtn;
    public EventTrigger _rightRotateBtn;
    
    public float speed = 5.0f; // 玩家移动速度
    public float rotationSpeed = 1.0f;

    private Vector3 handleDir;
    private int rotateDir;

    void Start()
    {
#if false
        _upBtn.gameObject.SetActive(false);
        _downBtn.gameObject.SetActive(false);
        _leftBtn.gameObject.SetActive(false);
        _rightBtn.gameObject.SetActive(false);
        _leftRotateBtn.gameObject.SetActive(false);
        _rightRotateBtn.gameObject.SetActive(false);
#endif

        AddTriggerEvent(_upBtn, EventTriggerType.PointerDown, () => handleDir = Vector3.forward);
        AddTriggerEvent(_downBtn, EventTriggerType.PointerDown, () => handleDir = Vector3.back);
        AddTriggerEvent(_leftBtn, EventTriggerType.PointerDown, () => handleDir = Vector3.left);
        AddTriggerEvent(_rightBtn, EventTriggerType.PointerDown, () => handleDir = Vector3.right);
        AddTriggerEvent(_leftRotateBtn, EventTriggerType.PointerDown, () => rotateDir = -1);
        AddTriggerEvent(_rightRotateBtn, EventTriggerType.PointerDown, () => rotateDir = 1);
        
        AddTriggerEvent(_upBtn, EventTriggerType.PointerUp, () => handleDir = Vector3.zero);
        AddTriggerEvent(_downBtn, EventTriggerType.PointerUp, () => handleDir = Vector3.zero);
        AddTriggerEvent(_leftBtn, EventTriggerType.PointerUp, () => handleDir = Vector3.zero);
        AddTriggerEvent(_rightBtn, EventTriggerType.PointerUp, () => handleDir = Vector3.zero);
        AddTriggerEvent(_leftRotateBtn, EventTriggerType.PointerUp, () => rotateDir = 0);
        AddTriggerEvent(_rightRotateBtn, EventTriggerType.PointerUp, () => rotateDir = 0);
    }

    private void Update()
    {
        if (handleDir.magnitude > 0.01f)
        {
            Move(handleDir);
        }

        if (rotateDir != 0)
        {
            Rotation(rotateDir);
        }
    }

    private void AddTriggerEvent(EventTrigger eventTrigger, EventTriggerType triggerType, Action callBack)
    {
        var trigger = new EventTrigger.Entry() { eventID = triggerType };
        trigger.callback.AddListener(eventData => callBack?.Invoke());
        eventTrigger.triggers.Add(trigger);
    }

    void Move(Vector3 dir)
    {
        _player.Translate(dir * speed * Time.deltaTime, Space.Self);
    }

    void Rotation(int dir)
    {
        _player.Rotate(Vector3.up, dir * rotationSpeed * Time.deltaTime, Space.World);
    }
}
