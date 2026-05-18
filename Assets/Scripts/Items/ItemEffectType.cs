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
        MaxManaBonus,           // +x max mana (also increases time to fill ult meter)
        ManaRegenMultiplier,    // +x% mana regen speed (fills ult meter faster)
        ArmorBonus,             // +x flat armor
        CritChanceBonus,        // +x crit chance (additive, 0.05 = +5%)
        CritDamageBonus         // +x crit damage multiplier (additive, 0.5 = +50%)
    }
}
