using UnityEngine;

namespace Wepie.DesertSafari.GamePlay.HizCulling
{
    public class ObjData //数据集合
    {
        public string uid; //id标识
        public Vector3 pos; //坐标点
        public Vector3 ang; //欧拉角度
        public HizOccludee hizOccludee;

        public ObjData(HizOccludee hizOccludee, Vector3 pos, Vector3 ang)
        {
            this.uid = System.Guid.NewGuid().ToString();
            this.hizOccludee = hizOccludee;
            this.pos = pos;
            this.ang = ang;
        }
    }
}