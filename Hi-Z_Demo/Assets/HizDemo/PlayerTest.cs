using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerTest : MonoBehaviour
{
    public float speed = 5.0f; // 玩家移动速度
    public float rotationSpeed = 1.0f;

    private CharacterController controller;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        float moveHorizontal = Input.GetAxis("Horizontal");
        float  moveVertical = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(moveHorizontal, 0.0f,moveVertical);

        transform.Translate(movement * speed * Time.deltaTime, Space.Self);
        // controller.Move(movement * speed * Time.deltaTime);
        
        // 检查玩家是否按下了 "E" 键
        if (Input.GetKey(KeyCode.E))
        {
            // 旋转摄像头向右
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime,Space.World);
        }
        // 检查玩家是否按下了 "Q" 键
        else if (Input.GetKey(KeyCode.Q))
        {
            // 旋转摄像头向左
            transform.Rotate(Vector3.up, -rotationSpeed * Time.deltaTime, Space.World);
        }
    }
}
