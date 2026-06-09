using Category5.Enemies;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Splines;

public class CombatArena : Arena
{
    public override void GenerateArena()
    {
        foreach (Path path in connectedPaths)
        {
            
            BezierKnot entranceKnot;
            GameObject entranceObj;
            int knotIndex = 0;

            if (path.arenaA == this)
            {
                entranceKnot = path.spline[0];
                entranceObj = path.entranceA;
                knotIndex = 0;
            }
            else
            {
                entranceKnot = path.spline[path.spline.Count - 1];
                entranceObj = path.entranceB;
                knotIndex = path.spline.Count - 1;
            }
            Debug.Log("YEAH");
            CreateIsland(entranceObj.transform.position - new Vector3(0, 25f, 0), new Vector3(20f, 2f, 20f));
        }
    }
}
