using UnityEngine;

public class PlaceAtTopLeft : MonoBehaviour
{
    private Camera mainCamera; // 引用主摄像机

    void Start()
    {
        mainCamera=Camera.main;

        // 将物体放置在屏幕左上角
        Vector3 screenTopLeft = new Vector3(0, Screen.height, mainCamera.nearClipPlane); // 屏幕左上角的坐标
        Vector3 worldTopLeft = mainCamera.ScreenToWorldPoint(screenTopLeft);

        // 将物体的位置设置为计算出的世界坐标
        transform.position = worldTopLeft;
    }
}