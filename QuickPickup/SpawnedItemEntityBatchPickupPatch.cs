using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace QuickPickup
{
    [HarmonyPatch(typeof(SpawnedItemEntity), "OnUseStopped")]
    public class Patch_SpawnedItemEntity_OnUseStopped
    {
        static void Postfix(SpawnedItemEntity __instance, Agent userAgent, bool isSuccessful, int preferenceIndex)
        {
            if (!isSuccessful || userAgent == null) return;

            var pickedWeapon = __instance.WeaponCopy;
            if (pickedWeapon.IsEmpty) return;

            var settings = AutoAmmoPickupSettings.Instance;
            Vec3 center = __instance.GameEntity.GlobalPosition;
            var scene = Mission.Current.Scene;

            WeakGameEntity[] buffer = new WeakGameEntity[128];
            UIntPtr[] idBuffer = new UIntPtr[128];

            Vec3 min = center - new Vec3(settings.Radius, settings.Radius, settings.Radius);
            Vec3 max = center + new Vec3(settings.Radius, settings.Radius, settings.Radius);

            int count = SceneCompat.SelectEntitiesInBoxWithScriptComponent(scene, ref min, ref max, buffer, idBuffer, false);

            // 🔄 第一阶段：仅处理触发者自身拾取
            List<SpawnedItemEntity> remainingItems = new List<SpawnedItemEntity>();
            for (int i = 0; i < count; i++)
            {
                var weak = buffer[i];
                var groundEntity = weak.GetFirstScriptOfType<SpawnedItemEntity>();
                if (groundEntity == null || groundEntity.IsRemoved || groundEntity == __instance) continue;

                var groundWeapon = groundEntity.WeaponCopy;
                if (groundWeapon.IsEmpty) continue;

                // 检查是否是弹药类物品
                if (settings.OnlyAmmo && !IsAmmoLike(groundWeapon)) continue;

                // 尝试让触发者拾取
                if (TryPickupForAgent(userAgent, groundEntity, groundWeapon, settings))
                {
                    //InformationManager.DisplayMessage(new InformationMessage($"[QPickup] SELF: {userAgent.Name} takes {groundWeapon.Item.Name}"));
                }
                else
                {
                    // 未被拾取的物品加入剩余列表
                    remainingItems.Add(groundEntity);
                }
            }

            // 🔄 第二阶段：处理队友拾取（仅当有剩余物品时）
            if (remainingItems.Count > 0)
            {
                List<Agent> teammates = GetTeammates(userAgent);
                foreach (var teamAgent in teammates)
                {
                    // 跳过自身（已在第一阶段处理）
                    if (teamAgent == userAgent) continue;

                    // 检查队友是否在拾取范围内
                    if (teamAgent.Position.Distance(center) > settings.Radius) continue;

                    foreach (var groundEntity in remainingItems.ToList())
                    {
                        var groundWeapon = groundEntity.WeaponCopy;
                        if (groundWeapon.IsEmpty) continue;

                        if (TryPickupForAgent(teamAgent, groundEntity, groundWeapon, settings))
                        {
                            //InformationManager.DisplayMessage(new InformationMessage($"[QPickup] TEAM: {teamAgent.Name} takes {groundWeapon.Item.Name}"));
                            remainingItems.Remove(groundEntity); // 移除已拾取的物品
                            break; // 拾取成功后跳出当前物品循环
                        }
                    }
                }
            }
        }

        // 获取队友列表（同编队优先，其次同队伍）
        private static List<Agent> GetTeammates(Agent agent)
        {
            List<Agent> teammates = new List<Agent>();

            // 优先1: 同编队 (Formation)
            if (agent.Formation != null && agent.Formation.Team != null)
            {
                agent.Formation.ApplyActionOnEachUnit(a => {
                    if (a != null && a != agent)
                        teammates.Add(a);
                }, null);
            }
            // 降级2: 同队伍 (Team)
            else if (agent.Team != null)
            {
                teammates.AddRange(agent.Team.ActiveAgents.Where(a => a != agent));
            }

            return teammates;
        }

        // 尝试为指定代理拾取物品
        private static bool TryPickupForAgent(Agent agent, SpawnedItemEntity groundEntity, MissionWeapon groundWeapon, AutoAmmoPickupSettings settings)
        {
            if (settings.OnlyAmmo)
            {
                if (!IsAmmoLike(groundWeapon)) return false;
                if (!HasCompatibleAmmoSlot(agent, groundWeapon)) return false;
            }
            else
            {
                // 非弹药模式保持原逻辑
                return false;
            }

            var compatibleSlots = GetCompatibleAmmoSlots(agent, groundWeapon.Item);
            int totalCurrentAmmo = 0;
            int totalMaxAmmo = 0;

            foreach (var slot in compatibleSlots)
            {
                totalCurrentAmmo += agent.Equipment[slot].Amount;
                totalMaxAmmo += agent.Equipment[slot].MaxAmmo;
            }

            if (totalCurrentAmmo >= totalMaxAmmo) return false;

            // 找到第一个有空间的槽位进行拾取
            foreach (var slot in compatibleSlots)
            {
                int slotCurrent = agent.Equipment[slot].Amount;
                int slotMax = agent.Equipment[slot].MaxAmmo;

                if (slotCurrent < slotMax)
                {
                    if (groundEntity == null || groundEntity.IsRemoved) return false;
                    agent.OnItemPickup(groundEntity, slot, out bool success);
                    if (success)
                    {
                        groundEntity.RequestDeletionOnNextTick();
                        return true;
                    }
                }
            }

            return false;
        }

        // 以下方法保持不变
        private static bool HasCompatibleAmmoSlot(Agent agent, MissionWeapon weapon)
        {
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                if (agent.Equipment[slot].IsEmpty) continue;
                var slotItem = agent.Equipment[slot].CurrentUsageItem;
                if (slotItem == null) continue;
                if (IsCompatibleAmmo(slotItem, weapon.CurrentUsageItem))
                    return true;
            }
            return false;
        }

        private static List<EquipmentIndex> GetCompatibleAmmoSlots(Agent agent, ItemObject targetItem)
        {
            var compatibleSlots = new List<EquipmentIndex>();
            if (targetItem == null) return compatibleSlots;

            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot;
                 slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                if (agent.Equipment[slot].IsEmpty) continue;
                var slotItem = agent.Equipment[slot].CurrentUsageItem;
                if (slotItem == null) continue;
                if (IsCompatibleAmmo(slotItem, targetItem.PrimaryWeapon))
                {
                    compatibleSlots.Add(slot);
                }
            }
            return compatibleSlots;
        }

        private static bool IsCompatibleAmmo(WeaponComponentData item1, WeaponComponentData item2)
        {
            if (item1 == null || item2 == null) return false;
            // 弩箭兼容性
            if (item1.WeaponClass == WeaponClass.Bolt || item2.WeaponClass == WeaponClass.Bolt)
            {
                return item1.WeaponClass == WeaponClass.Bolt && item2.WeaponClass == WeaponClass.Bolt;
            }
            // 箭矢兼容性
            if (item1.WeaponClass == WeaponClass.Arrow || item2.WeaponClass == WeaponClass.Arrow)
            {
                return item1.WeaponClass == WeaponClass.Arrow && item2.WeaponClass == WeaponClass.Arrow;
            }
            // 投掷类武器兼容性
            if ((item1.WeaponClass == WeaponClass.Javelin || item1.WeaponClass == WeaponClass.ThrowingAxe ||
                 item1.WeaponClass == WeaponClass.ThrowingKnife) &&
                (item2.WeaponClass == WeaponClass.Javelin || item2.WeaponClass == WeaponClass.ThrowingAxe ||
                 item2.WeaponClass == WeaponClass.ThrowingKnife))
            {
                return item1.WeaponClass == item2.WeaponClass;
            }
            return item1 == item2;
        }

        private static bool IsAmmoLike(MissionWeapon weapon)
        {
            var kind = GetPickupKind(weapon);
            return kind.HasValue;
        }

        private static WeaponClass? GetPickupKind(MissionWeapon weapon)
        {
            if (weapon.IsEmpty) return null;
            var usageItem = weapon.CurrentUsageItem;
            if (usageItem == null) return null;

            switch (usageItem.WeaponClass)
            {
                case WeaponClass.Arrow:
                case WeaponClass.Bolt:
                case WeaponClass.ThrowingAxe:
                case WeaponClass.ThrowingKnife:
                case WeaponClass.Javelin:
                    return usageItem.WeaponClass;
                default:
                    return null;
            }
        }
    }

    // 兼容旧版游戏无 bool 参数的 SelectEntitiesInBoxWithScriptComponent
    internal static class SceneCompat
    {
        // 带 bool 参数的新版委托
        private delegate int SelectDelegateWithBool(
            Scene scene, ref Vec3 boundingBoxMin, ref Vec3 boundingBoxMax,
            WeakGameEntity[] entitiesOutput, UIntPtr[] entityIds, bool flag);

        // 不带 bool 参数的旧版委托
        private delegate int SelectDelegateWithoutBool(
            Scene scene, ref Vec3 boundingBoxMin, ref Vec3 boundingBoxMax,
            WeakGameEntity[] entitiesOutput, UIntPtr[] entityIds);

        private static SelectDelegateWithBool _cachedDelegate;
        private static bool _initialized;

        // 非泛型方法，专用于 SpawnedItemEntity
        public static int SelectEntitiesInBoxWithScriptComponent(
            Scene scene, ref Vec3 min, ref Vec3 max,
            WeakGameEntity[] buffer, UIntPtr[] idBuffer, bool flag = false)
        {
            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }

            if (_cachedDelegate == null)
                return 0;

            return _cachedDelegate(scene, ref min, ref max, buffer, idBuffer, flag);
        }

        private static void Initialize()
        {
            MethodInfo targetMethod = null;
            foreach (var m in typeof(Scene).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (m.Name == "SelectEntitiesInBoxWithScriptComponent" && m.IsGenericMethod)
                {
                    targetMethod = m;
                    break;
                }
            }

            if (targetMethod == null)
                return;

            // 具体化为 SpawnedItemEntity 的泛型方法
            MethodInfo genericMethod = targetMethod.MakeGenericMethod(typeof(SpawnedItemEntity));
            var parameters = genericMethod.GetParameters();
            bool hasBoolFlag = parameters.Length > 0 &&
                               parameters[parameters.Length - 1].ParameterType == typeof(bool);

            if (hasBoolFlag)
            {
                // 新版：直接创建委托
                _cachedDelegate = (SelectDelegateWithBool)Delegate.CreateDelegate(
                    typeof(SelectDelegateWithBool), genericMethod);
            }
            else
            {
                // 旧版：创建无 bool 委托，然后用静态方法包装，忽略 bool 参数
                var d = (SelectDelegateWithoutBool)Delegate.CreateDelegate(
                    typeof(SelectDelegateWithoutBool), genericMethod);
                _cachedDelegate = InvokeWithoutBool;

                // 本地函数（或单独的方法）用于包装调用
                int InvokeWithoutBool(
                    Scene scene, ref Vec3 min, ref Vec3 max,
                    WeakGameEntity[] buffer, UIntPtr[] idBuffer, bool flag)
                {
                    return d(scene, ref min, ref max, buffer, idBuffer);
                }
            }
        }
    }
}