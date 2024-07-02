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
        public GameObject[] Occludees { get; private set; }

        void Start()
        {
            var meshs = GetComponentsInChildren<MeshRenderer>(false);
            Bouns = new Bounds[meshs.Length];
            Occludees = new GameObject[meshs.Length];

            for (int i = 0; i < meshs.Length; i++)
            {
                Bouns[i] = meshs[i].bounds;
                Occludees[i] = meshs[i].gameObject;
            }
        }

        private void OnEnable()
        {
            AABBMgr.Instance.Register(this);
        }

        private void OnDisable()
        {
            AABBMgr.Instance.UnRegister(this);
        }
    }
}