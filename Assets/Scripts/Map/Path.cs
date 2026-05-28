using UnityEngine;
using UnityEngine.Splines;

public class Path : MonoBehaviour
{
    public Arena arenaA;
    public Arena arenaB;
    public GameObject gameObjectRef;

    public bool isHidden=false;

    // The spline that makes up the physical object of this path
    public Spline spline;

    // Wind launch pad gameobjects, these are created in the AddWindTunnelToPaths function
    public GameObject entranceA;
    public GameObject entranceB;

    
}
