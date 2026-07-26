namespace Category5.WeakPoints
{
    // determines how the weak point detects hits
    public enum WeakPointType
    {
        // spherical hitbox that detects when ranged attacks collide with it
        Ranged,

        // 3d area that counts melee strikes as a weak point hit
        // if the attacker is standing inside it (fiora passive style)
        MeleeZone
    }
}
