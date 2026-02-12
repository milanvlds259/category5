namespace Category5.Items
{
    // defines the type of effect an item provides
    public enum ItemEffectType
    {
        DamageMultiplier,       // +x% damage
        MaxHealthBonus,         // +x max hp
        DodgeCooldownReduction, // -x seconds dodge cooldown
        FlatDamageBonus,        // +x flat damage
        Lifesteal,              // x hp per hit
        MoveSpeedMultiplier,    // +x% movement speed (future)
        AttackSpeedMultiplier,  // +x% attack speed (future)
        MaxManaBonus,           // +x max mana
        ManaRegenMultiplier,    // +x% mana regen speed
        ManaCostReduction       // -x% mana cost
    }
}
