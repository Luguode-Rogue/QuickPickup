using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;


namespace QuickPickup
{
    public class SubModule : MBSubModuleBase
    {
        private static Harmony? _harmony;
        private static string? _moduleRoot;
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            mission.AddMissionBehavior(new BatchPickupMissionLogic());
        }
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            AutoAmmoPickupSettings.Load();
            _harmony = new Harmony("mod.autoammopickup");
            _harmony.PatchAll();
        }
        

    }
    public enum SupplyState
    {
        Idle,
        MovingToEdge,
        Returning,
        Replenishing
    }

    //public class PackHorseMissionLogic : MissionLogic
    //{
    //    private SupplyState _state = SupplyState.Idle;

    //    private float _checkInterval = 2f;
    //    private float _timer = 0f;
    //    private float _ammoThreshold = 0.25f;

    //    private List<Agent> _fetchers = new List<Agent>();
    //    private Dictionary<Agent, Vec3> _origin = new Dictionary<Agent, Vec3>();
    //    private Dictionary<Agent, Vec3> _edgeTargets = new Dictionary<Agent, Vec3>();

    //    public override void OnMissionTick(float dt)
    //    {
    //        if (Mission.Current == null || Mission.Current.Mode != MissionMode.Battle)
    //            return;

    //        _timer += dt;
    //        if (_timer < _checkInterval)
    //            return;

    //        _timer = 0f;

    //        switch (_state)
    //        {
    //            case SupplyState.Idle:
    //                CheckAmmo();
    //                break;

    //            case SupplyState.MovingToEdge:
    //                UpdateMoving();
    //                break;

    //            case SupplyState.Returning:
    //                UpdateReturning();
    //                break;

    //            case SupplyState.Replenishing:
    //                Replenish();
    //                break;
    //        }
    //    }

    //    // ======================
    //    // 检测弹药
    //    // ======================
    //    private void CheckAmmo()
    //    {
    //        Team team = Mission.Current.Teams.FirstOrDefault(t => t.IsPlayerTeam);
    //        if (team == null) return;

    //        float total = 0, max = 0;

    //        // 遍历玩家团队的所有编队
    //        foreach (var formation in team.FormationsIncludingEmpty)
    //        {
    //            if (formation == null) continue;

    //            formation.ApplyActionOnEachUnit(agent =>
    //            {
    //                if (agent == null || !agent.IsActive()) return;

    //                var eq = agent.Equipment;

    //                for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot;
    //                     i < EquipmentIndex.ExtraWeaponSlot;
    //                     i++)
    //                {
    //                    var item = eq[i];
    //                    if (item.IsEmpty) continue;

    //                    var usageItem = item.CurrentUsageItem;
    //                    if (usageItem == null) continue;

    //                    // 与之前代码保持一致的过滤逻辑
    //                    if (usageItem.IsRangedWeapon)
    //                    {
    //                        total += eq.GetAmmoAmount(i);
    //                        max += eq.GetMaxAmmo(i);
    //                    }
    //                }
    //            }, null);
    //        }

    //        if (max <= 0) return;

    //        if (total / max < _ammoThreshold)
    //        {
    //            Dispatch(team);
    //            _state = SupplyState.MovingToEdge;
    //        }
    //    }

    //    // ======================
    //    // 派兵
    //    // ======================
    //    private void Dispatch(Team team)
    //    {
    //        _fetchers.Clear();
    //        _origin.Clear();
    //        _edgeTargets.Clear();

    //        var selected = team.ActiveAgents.Take(3).ToList();

    //        foreach (var agent in selected)
    //        {
    //            _fetchers.Add(agent);
    //            _origin[agent] = agent.Position;

    //            Vec3 edge = GetEdge(agent.Position);
    //            _edgeTargets[agent] = edge;

    //            MoveAgent(agent, edge);
    //        }
    //    }

    //    // ======================
    //    // 前往边缘
    //    // ======================
    //    private void UpdateMoving()
    //    {
    //        foreach (var agent in _fetchers)
    //        {
    //            if (!agent.IsActive()) continue;

    //            Vec3 target = _edgeTargets[agent];

    //            if (agent.Position.Distance(target) < 5f)
    //            {
    //                SpawnHorse(agent);
    //            }
    //        }

    //        // 检查所有士兵是否已经上马
    //        if (_fetchers.All(a => a.MountAgent != null))
    //        {
    //            OrderReturn();
    //            _state = SupplyState.Returning;
    //        }
    //    }

    //    // ======================
    //    // 生成驮马并上马 - 使用原生方法
    //    // ======================
    //    private void SpawnHorse(Agent agent)
    //    {
    //        if (agent.MountAgent != null) return;

    //        var character = Game.Current.ObjectManager
    //            .GetObject<CharacterObject>("aserai_horse");

    //        if (character == null) return;

    //        MatrixFrame globalFrame = agent.GetWorldFrame().ToGroundMatrixFrame();
    //        Vec3 spawnPos = globalFrame.origin + new Vec3(1, 0, 0);

    //        var build = new AgentBuildData(character)
    //            .Team(agent.Team)
    //            .InitialPosition(spawnPos);

    //        var horse = Mission.Current.SpawnAgent(build);

    //        if (horse != null)
    //        {
    //            horse.FadeIn();

    //            // ⚠️ 直接使用原生 Mount 方法
    //            agent.Mount(horse);

    //            // 设置上马动画
    //            agent.SetActionChannel(0, ActionIndexCache.Create("act_mount_horse_from_left"));
    //        }
    //    }


    //    // ======================
    //    // 返回
    //    // ======================
    //    private void OrderReturn()
    //    {
    //        foreach (var agent in _fetchers)
    //        {
    //            if (!agent.IsActive()) continue;

    //            Vec3 pos = _origin[agent];
    //            MoveAgent(agent, pos);
    //        }
    //    }

    //    private void UpdateReturning()
    //    {
    //        bool allNear = true;

    //        foreach (var agent in _fetchers)
    //        {
    //            if (!agent.IsActive()) continue;

    //            if (agent.Position.Distance(_origin[agent]) > 10f)
    //            {
    //                allNear = false;
    //                break;
    //            }
    //        }

    //        if (allNear)
    //            _state = SupplyState.Replenishing;
    //    }

    //    // ======================
    //    // 补给 - 使用原生方法
    //    // ======================
    //    private void Replenish()
    //    {
    //        Team team = Mission.Current.Teams.FirstOrDefault(t => t.IsPlayerTeam);
    //        if (team == null) return;

    //        foreach (var agent in team.ActiveAgents)
    //        {
    //            RefillAmmo(agent);
    //        }

    //        // 下马并移除驮马
    //        foreach (var agent in _fetchers)
    //        {
    //            if (agent.MountAgent != null)
    //            {
    //                Agent horse = agent.MountAgent;

    //                // ⚠️ 使用原生 Mount(null) 下马
    //                agent.Mount(null);

    //                // ⚠️ 注意：此时 horse 可能已经被原生方法清理了
    //                // 所以不要对 horse 进行操作

    //                // 士兵翻滚动画
    //                agent.SetActionChannel(0, ActionIndexCache.Create("act_agent_roll"));
    //            }
    //        }

    //        _fetchers.Clear();
    //        _origin.Clear();
    //        _edgeTargets.Clear();

    //        _state = SupplyState.Idle;
    //    }

    //    // ======================
    //    // 士兵翻滚动画（辅助方法）
    //    // ======================
    //    private void AgentRoll(Agent agent)
    //    {
    //        // 设置翻滚动画
    //        agent.SetActionChannel(0, ActionIndexCache.Create("act_agent_roll"));
    //    }

    //    private void RefillAmmo(Agent agent)
    //    {
    //        var eq = agent.Equipment;

    //        for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot;
    //             i < EquipmentIndex.NumAllWeaponSlots;
    //             i++)
    //        {
    //            var item = eq[i];
    //            if (item.IsEmpty) continue;

    //            if (item.CurrentUsageItem?.IsRangedWeapon == true)
    //            {
    //                short max = (short)eq.GetMaxAmmo(i);
    //                eq.SetAmountOfSlot(i, max);
    //            }
    //        }
    //    }

    //    // ======================
    //    // 工具
    //    // ======================
    //    private void MoveAgent(Agent agent, Vec3 pos)
    //    {
    //        WorldPosition wp = new WorldPosition(
    //            Mission.Current.Scene,
    //            UIntPtr.Zero,
    //            pos,
    //            false
    //        );

    //        agent.SetScriptedPosition(ref wp, false);
    //    }

    //    private Vec3 GetEdge(Vec3 from)
    //    {
    //        Vec3 min, max;
    //        Mission.Current.Scene.GetBoundingBox(out min, out max);

    //        Vec3 center = (min + max) * 0.5f;
    //        Vec3 dir = (from - center).NormalizedCopy();

    //        return center + dir * 150f;
    //    }

 
    //}
    //[HarmonyPatch(typeof(Agent), "Mount")]
    //public class AgentMountPatch
    //{
    //    [HarmonyPrefix]
    //    public static bool Prefix(Agent __instance, Agent mountAgent)
    //    {
    //        // 检查马匹是否在后仰状态
    //        bool isHorseRearing = mountAgent != null &&
    //                              mountAgent.GetCurrentActionType(0) == Agent.ActionCodeType.Rear;

    //        // 上马逻辑
    //        if (__instance.MountAgent == null && mountAgent != null && mountAgent.RiderAgent == null)
    //        {
    //            if (__instance.CheckSkillForMounting(mountAgent) &&
    //                !isHorseRearing &&
    //                __instance.GetCurrentAction(0) == ActionIndexCache.act_none)
    //            {
    //                // 1. 设置上马标志
    //                __instance.EventControlFlags |= Agent.EventControlFlag.Mount;
    //                __instance.SetInteractionAgent(mountAgent);

    //                // 2. 直接设置坐骑引用（绕过原生检查）
    //                var traverse = Traverse.Create(__instance);
    //                traverse.Property("MountAgent").SetValue(mountAgent);
    //            }
    //        }
    //        // 下马逻辑
    //        else if (__instance.MountAgent == mountAgent && !isHorseRearing)
    //        {
    //            // 1. 设置下马标志
    //            __instance.EventControlFlags |= Agent.EventControlFlag.Dismount;

    //            // 2. 直接清空坐骑引用
    //            var traverse = Traverse.Create(__instance);
    //            traverse.Property("MountAgent").SetValue(null);
    //        }

    //        // ⚠️ 返回false，阻止原生方法执行
    //        return false;
    //    }
    //}
}
