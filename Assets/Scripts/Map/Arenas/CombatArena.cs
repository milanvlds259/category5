using Category5.Enemies;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Splines;

public class CombatArena : Arena
{
    // Do a pass for outer and inner later!
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

            CreateIsland(entranceObj.transform.position - new Vector3(0, 25f, 0), new Vector3(20f, 2f, 20f));
        }

        int numIslands = 1;
        if (scaleFactor <= 1f)
        {
            numIslands = 2;
        }
        else if (scaleFactor <= 2f)
        {
            numIslands = 3;
        }
        else
        {
            numIslands = 4;
        }
        for (int i = 0; i < numIslands; i++) {
            for (int tries = 0; tries < 50; tries++)
            {
                Vector3 randomPos = transform.position + new Vector3(
                    Random.Range(-radius + scaleFactor * 10, radius - scaleFactor * 10),
                    Random.Range(-scaleFactor * 30, scaleFactor * 30),
                    Random.Range(-radius + scaleFactor * 10, radius - scaleFactor * 10));

                if (CreateIsland(randomPos, new Vector3(20f, 2f, 20f)) != null)
                {
                    break;
                }
            }
        }
    }
}
