using Facepunch;
using Rust;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Oxide.Plugins
{
    [Info("NpcRustEdit", "KpucTaJl", "1.0.3")]
    internal class NpcRustEdit : RustPlugin
    {
        #region Oxide Hooks
        private void Init() => Unsubscribes();

        private void OnServerInitialized()
        {
            foreach (ScientistNPC npc in UnityEngine.Object.FindObjectsOfType<ScientistNPC>()) OnEntitySpawned(npc);
            foreach (ScarecrowNPC npc in UnityEngine.Object.FindObjectsOfType<ScarecrowNPC>()) OnEntitySpawned(npc);
            Subscribes();
        }

        private void Unload()
        {
            foreach (KeyValuePair<ScientistNPC, ControllerScientist> dic in _scientists) UnityEngine.Object.Destroy(dic.Value);
            foreach (KeyValuePair<ScarecrowNPC, ControllerScarecrow> dic in _scarecrows) UnityEngine.Object.Destroy(dic.Value);
        }

        private void OnEntitySpawned(ScientistNPC npc)
        {
            if (npc == null) return;
            if (!_scientists.ContainsKey(npc) && npc.ShortPrefabName == "scientistnpc_roam")
            {
                if (_scientists.Any(x => Vector3.Distance(x.Value.HomePosition, npc.transform.position) < 1f) && !npc.IsDestroyed) npc.Kill();
                else _scientists.Add(npc, npc.gameObject.AddComponent<ControllerScientist>());
            }
        }

        private void OnEntitySpawned(ScarecrowNPC npc)
        {
            if (npc == null) return;
            if (!_scarecrows.ContainsKey(npc) && npc.ShortPrefabName == "scarecrow")
            {
                if (_scarecrows.Any(x => Vector3.Distance(x.Value.HomePosition, npc.transform.position) < 1f) && !npc.IsDestroyed) npc.Kill();
                else _scarecrows.Add(npc, npc.gameObject.AddComponent<ControllerScarecrow>());
            }
        }

        private void OnEntityKill(ScientistNPC npc) { if (_scientists.ContainsKey(npc)) _scientists.Remove(npc); }

        private void OnEntityKill(ScarecrowNPC npc) { if (_scarecrows.ContainsKey(npc)) _scarecrows.Remove(npc); }
        #endregion Oxide Hooks

        #region Controller Scientist
        private readonly Dictionary<ScientistNPC, ControllerScientist> _scientists = new Dictionary<ScientistNPC, ControllerScientist>();

        internal class ControllerScientist : FacepunchBehaviour
        {
            private ScientistNPC _npc;
            internal Vector3 HomePosition;
            private const float RoamRange = 25f;

            private float DistanceFromBase => Vector3.Distance(_npc.transform.position, HomePosition);

            private void Awake()
            {
                _npc = GetComponent<ScientistNPC>();
                HomePosition = _npc.transform.position;
                InvokeRepeating(UpdatePosition, 1f, 1f);
            }

            private void OnDestroy() => CancelInvoke(UpdatePosition);

            private void UpdatePosition()
            {
                BasePlayer target = _npc.GetBestTarget() as BasePlayer;
                if (target == null)
                {
                    if (DistanceFromBase > RoamRange) _npc.Brain.Navigator.SetDestination(HomePosition, BaseNavigator.NavigationSpeed.Fast);
                    else if (!_npc.Brain.Navigator.Moving) _npc.Brain.Navigator.SetDestination(GetRoamPosition(), BaseNavigator.NavigationSpeed.Slowest);
                }
            }

            private Vector3 GetRoamPosition()
            {
                Vector2 random = UnityEngine.Random.insideUnitCircle * (RoamRange - 5f);
                Vector3 result = HomePosition + new Vector3(random.x, 0f, random.y);
                result.y = TerrainMeta.HeightMap.GetHeight(result);
                NavMeshHit navMeshHit;
                if (NavMesh.SamplePosition(result, out navMeshHit, 5f, _npc.NavAgent.areaMask))
                {
                    NavMeshPath path = new NavMeshPath();
                    if (NavMesh.CalculatePath(_npc.transform.position, navMeshHit.position, _npc.NavAgent.areaMask, path)) result = path.status == NavMeshPathStatus.PathComplete ? navMeshHit.position : path.corners.Last();
                    else result = HomePosition;
                }
                else result = HomePosition;
                return result;
            }
        }
        #endregion Controller Scientist

        #region Controller Scarecrow
        private readonly Dictionary<ScarecrowNPC, ControllerScarecrow> _scarecrows = new Dictionary<ScarecrowNPC, ControllerScarecrow>();

        internal class ControllerScarecrow : FacepunchBehaviour
        {
            private ScarecrowNPC _npc;
            internal Vector3 HomePosition;
            private const float RoamRange = 25f;

            private BaseMelee _weapon;
            private BasePlayer _target;

            private float DistanceFromBase => Vector3.Distance(_npc.transform.position, HomePosition);
            private float DistanceToTarget => Vector3.Distance(_npc.transform.position, _target.transform.position);

            private void Awake()
            {
                _npc = GetComponent<ScarecrowNPC>();
                HomePosition = _npc.transform.position;
                _weapon = _npc.GetHeldEntity() as BaseMelee;
            }

            private void FixedUpdate()
            {
                _target = GetBestTarget();
                if (_target == null)
                {
                    _npc.Brain.Navigator.ClearFacingDirectionOverride();
                    if (DistanceFromBase > RoamRange) _npc.Brain.Navigator.SetDestination(HomePosition, BaseNavigator.NavigationSpeed.Fast);
                    else
                    {
                        _npc.Brain.Navigator.SetCurrentSpeed(BaseNavigator.NavigationSpeed.Slowest);
                        if (!_npc.Brain.Navigator.Moving) _npc.Brain.Navigator.SetDestination(GetRoamPosition(), BaseNavigator.NavigationSpeed.Slowest);
                    }
                }
                else
                {
                    _npc.Brain.Navigator.SetFacingDirectionEntity(_target);
                    if (_weapon == null) _weapon = _npc.GetHeldEntity() as BaseMelee;
                    if (_weapon != null && DistanceToTarget < _weapon.effectiveRange && !_weapon.HasAttackCooldown()) DoMeleeAttack();
                    else _npc.Brain.Navigator.SetDestination(_target.transform.position, BaseNavigator.NavigationSpeed.Fast);
                }
            }

            private Vector3 GetRoamPosition()
            {
                Vector2 random = UnityEngine.Random.insideUnitCircle * (RoamRange - 5f);
                Vector3 result = HomePosition + new Vector3(random.x, 0f, random.y);
                result.y = TerrainMeta.HeightMap.GetHeight(result);
                NavMeshHit navMeshHit;
                if (NavMesh.SamplePosition(result, out navMeshHit, 5f, _npc.NavAgent.areaMask))
                {
                    NavMeshPath path = new NavMeshPath();
                    if (NavMesh.CalculatePath(_npc.transform.position, navMeshHit.position, _npc.NavAgent.areaMask, path)) result = path.status == NavMeshPathStatus.PathComplete ? navMeshHit.position : path.corners.Last();
                    else result = HomePosition;
                }
                else result = HomePosition;
                return result;
            }

            private void DoMeleeAttack()
            {
                _weapon.StartAttackCooldown(_weapon.repeatDelay * 2f);
                _npc.SignalBroadcast(BaseEntity.Signal.Attack, string.Empty);
                if (_weapon.swingEffect.isValid) Effect.server.Run(_weapon.swingEffect.resourcePath, _weapon.transform.position, Vector3.forward, _npc.net.connection);
                DoMeleeDamage(_weapon);
            }

            private void DoMeleeDamage(BaseMelee baseMelee)
            {
                Vector3 position = _npc.eyes.position;
                Vector3 forward = _npc.eyes.BodyForward();
                for (int i = 0; i < 2; i++)
                {
                    List<RaycastHit> list = Pool.GetList<RaycastHit>();
                    GamePhysics.TraceAll(new Ray(position - (forward * (i == 0 ? 0f : 0.2f)), forward), (i == 0 ? 0f : baseMelee.attackRadius), list, baseMelee.effectiveRange + 0.2f, 1219701521);
                    bool hasHit = false;
                    foreach (RaycastHit raycastHit in list)
                    {
                        BaseEntity hitEntity = raycastHit.GetEntity();
                        if (hitEntity != null && hitEntity != _npc && !hitEntity.EqualNetID(_npc) && !(hitEntity is ScientistNPC))
                        {
                            float damageAmount = baseMelee.damageTypes.Sum(x => x.amount);
                            hitEntity.OnAttacked(new HitInfo(_npc, hitEntity, DamageType.Slash, damageAmount * baseMelee.npcDamageScale));
                            HitInfo hitInfo = Pool.Get<HitInfo>();
                            hitInfo.HitEntity = hitEntity;
                            hitInfo.HitPositionWorld = raycastHit.point;
                            hitInfo.HitNormalWorld = -forward;
                            if (hitEntity is BaseNpc || hitEntity is BasePlayer) hitInfo.HitMaterial = StringPool.Get("Flesh");
                            else hitInfo.HitMaterial = StringPool.Get((raycastHit.GetCollider().sharedMaterial != null ? raycastHit.GetCollider().sharedMaterial.GetName() : "generic"));
                            Effect.server.ImpactEffect(hitInfo);
                            Pool.Free(ref hitInfo);
                            hasHit = true;
                            if (hitEntity.ShouldBlockProjectiles()) break;
                        }
                    }
                    Pool.FreeList(ref list);
                    if (hasHit) break;
                }
            }

            private BasePlayer GetBestTarget()
            {
                BasePlayer target = null;
                float delta = -1f;
                foreach (BasePlayer basePlayer in _npc.Brain.Senses.Memory.Targets.OfType<BasePlayer>())
                {
                    if (!CanTargetBasePlayer(basePlayer)) continue;
                    float rangeDelta = 1f - Mathf.InverseLerp(1f, _npc.Brain.SenseRange, Vector3.Distance(basePlayer.transform.position, _npc.transform.position));
                    rangeDelta += (_npc.Brain.Senses.Memory.IsLOS(basePlayer) ? 2f : 0f);
                    if (rangeDelta <= delta) continue;
                    target = basePlayer;
                    delta = rangeDelta;
                }
                return target;
            }

            private static bool CanTargetBasePlayer(BasePlayer player)
            {
                if (player == null || player.Health() <= 0f) return false;
                if (player.IsFlying || player.IsSleeping() || player.IsWounded() || player.IsDead() || player.InSafeZone()) return false;
                return player.userID.IsSteamId();
            }
        }
        #endregion Controller Scarecrow

        #region Helpers
        private readonly HashSet<string> _hooks = new HashSet<string>
        {
            "OnEntityKill",
            "OnEntitySpawned"
        };

        private void Unsubscribes() { foreach (string hook in _hooks) Unsubscribe(hook); }

        private void Subscribes() { foreach (string hook in _hooks) Subscribe(hook); }
        #endregion Helpers
    }
}