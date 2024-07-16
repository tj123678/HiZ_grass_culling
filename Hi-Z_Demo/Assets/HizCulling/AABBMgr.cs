using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wepie.DesertSafari.GamePlay.HizCulling
{
    public class AABBMgr : MonoBehaviour
    {
        private List<HizOccludee> _hizOccludees;
        public static AABBMgr Instance { get; private set; }
        private Bounds[] _bounds;
        private Camera cullCamera;

        // Start is called before the first frame update
        void Awake()
        {
            Instance = this;
            _hizOccludees = new List<HizOccludee>();
            cullCamera = Camera.main;
        }

        public void UpdateRender(List<GameObject> occuldes, bool[] isCulls)
        {
            for (int i = 0; i < occuldes.Count; i++)
            {
                occuldes[i].SetActive(!isCulls[i]);
                // _renderers[i].renderingLayerMask = isCulls[i] ? 0u :1u;
            }
        }

        public void ShowAll()
        {
            foreach (var occludee in _hizOccludees)
            {
                foreach (var obj in occludee.Occludees)
                {
                    obj.gameObject.SetActive(true);
                }
            }
        }

        private void LogScreenPos(int index, Bounds bounds)
        {
            var center = bounds.center;
            var size = bounds.size;
            var posMax = cullCamera.WorldToViewportPoint(bounds.max);
            var posMin = cullCamera.WorldToViewportPoint(bounds.min);


            // 使用String.Format保留三位小数
            string posMaxString = String.Format("({0:F3},{1:F3},{2:F3})", posMax.x, 1 - posMax.y, posMax.z);
            string posMinString = String.Format("({0:F3},{1:F3},{2:F3})", posMin.x, 1 - posMin.y, posMin.z);

            var distance = Vector3.Distance(bounds.center, cullCamera.transform.position) - bounds.size.z / 2;
            //DX平台
            var dxDepth = ((cullCamera.farClipPlane - distance) * cullCamera.nearClipPlane) /
                          ((cullCamera.farClipPlane - cullCamera.nearClipPlane) * distance);
            //OpenGL 平台
            distance *= -1;
            var openGlDepth = ((cullCamera.nearClipPlane + distance) * cullCamera.farClipPlane) /
                              ((cullCamera.farClipPlane - cullCamera.nearClipPlane) * distance);

            // Debug.LogError($"screent index:{index} posMax:{posMaxString}  posMin:{posMinString} DX_depth:{dxDepth} openGl_depth:{openGlDepth} ");
        }

        #region 管理能被剔除的单位

        public void Register(HizOccludee occludee)
        {
            _hizOccludees.Add(occludee);
        }

        public void UnRegister(HizOccludee occludee)
        {
            _hizOccludees.Remove(occludee);
        }

        public void GetAABBInfos(List<GameObject> objs, List<Bounds> bouns)
        {
            if (objs == null || bouns == null) return;
            objs.Clear();
            bouns.Clear();

            foreach (var occludee in _hizOccludees)
            {
                if (occludee.IsInCameraView())
                {
                    objs.AddRange(occludee.Occludees);
                    bouns.AddRange(occludee.Bouns);
                }
            }
        }
        #endregion
    }
}