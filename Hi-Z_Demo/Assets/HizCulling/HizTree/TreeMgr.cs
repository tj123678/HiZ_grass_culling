using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

public class TreeMgr : MonoBehaviour
{
    public Tree tree;//树

    private bool startEnd = false;//控制结束
    public Camera cam;//相机
    private Plane[] _planes;//存储视锥体六个面
    
    void Start()
    {
        cam=Camera.main;
        _planes = new Plane[6];//初始化
        var pos = transform.position;
        Bounds bounds = new Bounds(pos,new Vector3(2000,1000,2000));//生成包围盒
        tree = new Tree(bounds);//初始化行为树
        startEnd = true;
    }
   
    void Update()
    {
        GeometryUtility.CalculateFrustumPlanes(cam, _planes);
        tree.TriggerMove(_planes);//传六个面
    }

    private void OnDrawGizmos()
    {
        if(startEnd)
        {
            tree.DrawBound();//开始绘制包围盒 用树组绘制盒
        }
    }
}
