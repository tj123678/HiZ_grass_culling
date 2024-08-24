
    using UnityEngine;
    using Wepie.DesertSafari.GamePlay.HizCulling;

    public class ObjData//数据集合
    {
        public string uid;//id标识
        public GameObject prefab;//预制体
        public Vector3 pos;//坐标点
        public Vector3 ang;//欧拉角度
        public HizOccludee hizOccludee;

        public ObjData(GameObject prefab, Vector3 pos,Vector3 ang)
        {
            this.uid = System.Guid.NewGuid().ToString();
            this.prefab = prefab;
            this.pos = pos;
            this.ang = ang;
            hizOccludee = prefab.GetComponent<HizOccludee>();
        }
    }
