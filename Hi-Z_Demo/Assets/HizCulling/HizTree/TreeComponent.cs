using UnityEngine;

namespace Wepie.DesertSafari.GamePlay.HizCulling
{
    public class TreeComponent : MonoBehaviour
    {
        public Tree tree; //树

        private bool startEnd = false; //控制结束
        public Camera cam; //相机
        private Plane[] _planes; //存储视锥体六个面

        public void Init(Camera cam,Vector3 size)
        {
            this.cam = cam;
            _planes = new Plane[6]; //初始化
            var pos = transform.position;
            Bounds bounds = new Bounds(pos, size); //生成包围盒
            tree = new Tree(bounds); //初始化行为树
            startEnd = true;
        }

        public void Reflesh()
        {
            AABBMgr.Instance.Clear();
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            tree.TriggerMove(_planes); //传六个面
        }

        private void OnDrawGizmos()
        {
            if (startEnd && HizMgr.Instance.IsDrawDebug)
            {
                tree.DrawBound(); //开始绘制包围盒 用树组绘制盒
            }
        }
    }
}