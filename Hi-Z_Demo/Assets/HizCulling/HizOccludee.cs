using Unity.Mathematics;
using UnityEngine;

namespace Wepie.DesertSafari.GamePlay.HizCulling
{
    /// <summary>
    /// hiz 被遮挡物
    /// </summary>
    [DisallowMultipleComponent]
    public class HizOccludee : MonoBehaviour
    {
        public Bounds[] Bouns { get; private set; }
        public MeshRenderer[] Occludees { get; private set; }

        //用一个大的box包含所有的子物体
        private Vector3 size;
        private Vector3 center;

        private bool isInCameraView;

        void Start()
        {
            var meshs = GetComponentsInChildren<MeshRenderer>(false);
            Bouns = new Bounds[meshs.Length];
            Occludees = new MeshRenderer[meshs.Length];

            for (int i = 0; i < meshs.Length; i++)
            {
                Bouns[i] = meshs[i].bounds;
                Occludees[i] = meshs[i];
            }
            
            isInCameraView = false;
            InitBox();
        }

        private void InitBox()
        {
            // 初始化包围盒的最小和最大边界点
            Vector3 minBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 maxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            // 递归遍历所有子物体
            foreach (Bounds bound in Bouns)
            {
                // 假设子物体都有Collider组件，如果没有则需要其他方式获取边界
                minBounds = Vector3.Min(minBounds, bound.min);
                maxBounds = Vector3.Max(maxBounds, bound.max);
            }

            // 计算包围盒的中心和尺寸
            center = (minBounds + maxBounds) / 2f;
            size = maxBounds - minBounds;
        }

        private void OnEnable()
        {
            AABBMgr.Instance?.Register(this);
        }

        private void OnDisable()
        {
            AABBMgr.Instance?.UnRegister(this);
        }

        public bool IsInCameraView()
        {
            var vp = GL.GetGPUProjectionMatrix(HizMgr.Instance.CullCamera.projectionMatrix, false) * HizMgr.Instance.CullCamera.worldToCameraMatrix;

            Vector3[] vertices = new Vector3[8];
            Vector3 halfSize = size / 2f;

            // 顶点索引顺序：前上右，后上右，前下右，后下右，前上左，后上左，前下左，后下左
            vertices[0] = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
            vertices[1] = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
            vertices[2] = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
            vertices[3] = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
            vertices[4] = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);
            vertices[5] = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
            vertices[6] = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
            vertices[7] = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);

            for (int i = 0; i < vertices.Length; i++)
            {
                float4 ndc = vp * new Vector4(vertices[i].x, vertices[i].y, vertices[i].z, 1f);
                ndc.xyz /= ndc.w;
                //只有有一个点在相机内，就认为物体在相机内
                if (Mathf.Abs(ndc.x) < 1 && Mathf.Abs(ndc.y) < 1 && ndc.z > 0 && ndc.z < 1)
                {
                    isInCameraView = true;
                    return true;
                }
            }

            isInCameraView = false;
            return false;
        }

        void OnDrawGizmos()
        {
            if (isInCameraView)
            {
                DrawCube(center, size);
            }
        }

        void DrawCube(Vector3 center, Vector3 size)
        {
            if (!HizMgr.Instance.IsDrawDebug) return;
            // 计算半尺寸
            Vector3 halfSize = size / 2f;

            // 绘制长方体的边
            // 正面
            Debug.DrawLine(center + new Vector3(-halfSize.x, halfSize.y, halfSize.z), center + new Vector3(halfSize.x, halfSize.y, halfSize.z), Color.red);
            Debug.DrawLine(center + new Vector3(halfSize.x, halfSize.y, halfSize.z), center + new Vector3(halfSize.x, -halfSize.y, halfSize.z), Color.red);
            Debug.DrawLine(center + new Vector3(halfSize.x, -halfSize.y, halfSize.z), center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z), Color.red);
            Debug.DrawLine(center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z), center + new Vector3(-halfSize.x, halfSize.y, halfSize.z), Color.red);

            // 反面
            Debug.DrawLine(center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z), center + new Vector3(halfSize.x, halfSize.y, -halfSize.z), Color.red);
            Debug.DrawLine(center + new Vector3(halfSize.x, halfSize.y, -halfSize.z), center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z), Color.red);
            Debug.DrawLine(center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z), center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), Color.red);
            Debug.DrawLine(center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z), Color.red);

            // 连接前后面
            for (int i = 0; i < 4; i++)
            {
                Debug.DrawLine(
                    center + new Vector3(((i % 2) == 0 ? -halfSize.x : halfSize.x), ((i < 2) ? halfSize.y : -halfSize.y), halfSize.z),
                    center + new Vector3(((i % 2) == 0 ? -halfSize.x : halfSize.x), ((i < 2) ? halfSize.y : -halfSize.y), -halfSize.z),
                    Color.blue
                );
            }
        }
    }
}