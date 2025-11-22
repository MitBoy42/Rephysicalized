using HarmonyLib;
using Klei.AI;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace Rephysicalized.Content.System_Patches
{
    // Holds the dynamic modifier so we can remove it cleanly on unequip
    public sealed class DynamicScaldingHolder : KMonoBehaviour
    {
        private AttributeModifier mod;
        private GameObject lastTarget;

        public void Set(Equippable eq, AttributeModifier newMod)
        {
            Remove(eq); // ensure previous one is gone

            var owner = eq?.assignee?.GetSoleOwner()
                             ?.GetComponent<MinionAssignablesProxy>()
                             ?.GetTargetGameObject();
            if (owner == null) return;

            var attrs = owner.GetAttributes();
            if (attrs == null) return;

            // Attach to the ScaldingThreshold attribute
            attrs.Get(Db.Get().Attributes.ScaldingThreshold).Add(newMod);

            mod = newMod;
            lastTarget = owner;
        }

        public void Remove(Equippable eq)
        {
            if (mod != null && lastTarget != null)
            {
                var attrs = lastTarget.GetAttributes();
                attrs?.Get(Db.Get().Attributes.ScaldingThreshold)?.Remove(mod);
            }
            mod = null;
            lastTarget = null;
        }

        public override void OnCleanUp()
        {
            // Safety: ensure the modifier is removed if the item despawns
            Remove(null);
            base.OnCleanUp();
        }
    }

    [HarmonyPatch(typeof(AtmoSuitConfig), nameof(AtmoSuitConfig.CreateEquipmentDef))]
    public static class AtmoSuitConfig_CreateEquipmentDef_DynamicScalding_Patch
    {
        public static void Postfix(ref EquipmentDef __result)
        {

            // Remove static ScaldingThreshold modifiers (if any)
            var scaldAttrId = Db.Get().Attributes.ScaldingThreshold.Id;
            __result.AttributeModifiers?.RemoveAll(m => m.AttributeId == scaldAttrId);

            // Wrap OnEquip to add dynamic modifier from suit material highTemp (Celsius) * 0.6
            var oldEquip = __result.OnEquipCallBack;
            __result.OnEquipCallBack = (Equippable eq) =>
            {
                oldEquip?.Invoke(eq);

                var element = eq?.GetComponent<PrimaryElement>()?.Element;

                float dynamicValue = (element.highTemp - 273.15f) * 0.6f; // Celsius * 0.6

                var mod = new AttributeModifier(
                    scaldAttrId,
                    dynamicValue,
                    "Suit material threshold",
                    is_multiplier: false,
                    is_readonly: false,
                    uiOnly: false
                );

                eq.gameObject.AddOrGet<DynamicScaldingHolder>().Set(eq, mod);
            };

            // Wrap OnUnequip to remove our dynamic modifier
            var oldUnequip = __result.OnUnequipCallBack;
            __result.OnUnequipCallBack = (Equippable eq) =>
            {
                eq?.gameObject?.GetComponent<DynamicScaldingHolder>()?.Remove(eq);
                oldUnequip?.Invoke(eq);
            };
        }
    }

    [HarmonyPatch(typeof(JetSuitConfig), nameof(JetSuitConfig.CreateEquipmentDef))]
    public static class JetSuitConfig_CreateEquipmentDef_DynamicScalding_Patch
    {
        public static void Postfix(ref EquipmentDef __result)
        {

            var scaldAttrId = Db.Get().Attributes.ScaldingThreshold.Id;
            __result.AttributeModifiers?.RemoveAll(m => m.AttributeId == scaldAttrId);

            var oldEquip = __result.OnEquipCallBack;
            __result.OnEquipCallBack = (Equippable eq) =>
            {
                oldEquip?.Invoke(eq);

                var element = eq?.GetComponent<PrimaryElement>()?.Element;

                float dynamicValue = (element.highTemp - 273.15f) * 0.6f; // Same adjustment as Atmo Suit

                var mod = new AttributeModifier(
                    scaldAttrId,
                    dynamicValue,
                    "Suit material threshold",
                    is_multiplier: false,
                    is_readonly: false,
                    uiOnly: false
                );

                eq.gameObject.AddOrGet<DynamicScaldingHolder>().Set(eq, mod);
            };

            var oldUnequip = __result.OnUnequipCallBack;
            __result.OnUnequipCallBack = (Equippable eq) =>
            {
                eq?.gameObject?.GetComponent<DynamicScaldingHolder>()?.Remove(eq);
                oldUnequip?.Invoke(eq);
            };
        }
    }

    [HarmonyPatch(typeof(LeadSuitConfig), nameof(LeadSuitConfig.CreateEquipmentDef))]
    public static class LeadSuitConfig_CreateEquipmentDef_DynamicScalding_Patch
    {
        public static void Postfix(ref EquipmentDef __result)
        {

            var scaldAttrId = Db.Get().Attributes.ScaldingThreshold.Id;
            __result.AttributeModifiers?.RemoveAll(m => m.AttributeId == scaldAttrId);

            var oldEquip = __result.OnEquipCallBack;
            __result.OnEquipCallBack = (Equippable eq) =>
            {
                oldEquip?.Invoke(eq);

                var element = eq?.GetComponent<PrimaryElement>()?.Element;

                float dynamicValue = (element.highTemp - 273.15f); // No 0.6 multiplier for Lead Suit

                var mod = new AttributeModifier(
                    scaldAttrId,
                    dynamicValue,
                    "Suit material threshold",
                    is_multiplier: false,
                    is_readonly: false,
                    uiOnly: false
                );

                eq.gameObject.AddOrGet<DynamicScaldingHolder>().Set(eq, mod);
            };

            var oldUnequip = __result.OnUnequipCallBack;
            __result.OnUnequipCallBack = (Equippable eq) =>
            {
                eq?.gameObject?.GetComponent<DynamicScaldingHolder>()?.Remove(eq);
                oldUnequip?.Invoke(eq);
            };
        }
    }

    
}