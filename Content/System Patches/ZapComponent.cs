using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

using STRINGS;

public class ZapComponent : KMonoBehaviour, ISim200ms
{
    private const bool DEBUG_ZAP = false;
[Header("Zap Settings")]
    [SerializeField] public float DecorativeBeamScale = 1f;
    [SerializeField] public Vector3 DecorativeBeamOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] public Vector3 DamageBeamOffset = Vector3.zero;
    [SerializeField] public bool OnlyDamageBeamWhenTargeted = false;

    [SerializeField] public float DAMAGE_RATE = 1f;
    [SerializeField] public float SHOCK_RADIUS = 8f;
    [SerializeField] public float BEAM_INTERVAL = 0.5f; // Active every other second

private KBatchedAnimController beamFX;
    private KBatchedAnimController faceLightningFX;
    private KBatchedAnimController impactFX;
    private bool hasValidTarget = false;
    private GameObject target;
    private GameObject previousTarget;
    private bool isActive = false;
private float beamCooldown = 0f;
    private float damageAccumulator = 0f;
    private Vector3 sourcePos;

public void StartZapping()
    {
        if (DEBUG_ZAP) Debug.Log($"[{gameObject.name}] Zap START isActive=true");
        isActive = true;

        hasValidTarget = false;
        damageAccumulator = 0f;
        ClearBeam();
        MakeBeam();
        PickNewTarget();
        ShowDamageBeam(false);
        beamCooldown = 0f; // Immediate start
    }

public void StopZapping()
    {
        if (DEBUG_ZAP) Debug.Log($"[{gameObject.name}] Zap STOP isActive=false");
        isActive = false;

        hasValidTarget = false;
        ShowDamageBeam(false);
        ClearBeam();
        damageAccumulator = 0f;
        beamCooldown = 0f;
    }

    public void Sim200ms(float dt)
    {
        if (this == null || gameObject == null) return;
        if (DEBUG_ZAP) Debug.Log($"[{gameObject.name}] Zap Sim200ms dt={dt:F2} active={isActive} validSource={IsValidSource()}");
        if (!IsValidSource())
        {
            StopZapping();
            return;
        }


        if (!isActive || !this.enabled)
            return;

        bool wasValidTarget = hasValidTarget;
        // Full validation
        if (target == null || IsTargetInvalid())
        {
            previousTarget = target;
            PickNewTarget();
        }
        hasValidTarget = (target != null && !IsTargetInvalid());
        if (DEBUG_ZAP) Debug.Log($"[{gameObject.name}] Zap hasValidTarget={hasValidTarget} target={(target ? target.name : "null")} was={wasValidTarget}");

        if (!wasValidTarget && hasValidTarget)
            beamCooldown = 0f; // Reset cooldown for new target

        ShowDamageBeam(hasValidTarget);
        if (OnlyDamageBeamWhenTargeted && !hasValidTarget)
            MakeDamageBeamOnly();

        ShockUpdate200(dt);
        ShockUpdateRender(dt);
    }

    private bool IsTargetInvalid()
    {
        if (target == null || gameObject == null) return true;
        int sourceCell = Grid.PosToCell(transform.position);
        int targetCell = Grid.PosToCell(target.transform.position);
        float cellDist = Vector2.Distance(Grid.CellToPos2D(sourceCell), Grid.CellToPos2D(targetCell));
        return target.IsNullOrDestroyed() ||
               cellDist > SHOCK_RADIUS ||
               !HasValidComponents(target);
    }



private bool HasValidComponents(GameObject go)
    {
        return go.GetComponent<Health>() != null; // Only living things
    }

    private void PickNewTarget()
    {
        string selfName = gameObject?.name ?? "DESTROYED";
        if (gameObject == null) 
        {
            if (DEBUG_ZAP) Debug.Log($"[{selfName}] Zap PickNewTarget EARLY NULL");
            return;
        }
        int sourceCell = Grid.PosToCell(transform.position);
        int worldId = Grid.WorldIdx[sourceCell];
        if (DEBUG_ZAP) Debug.Log($"[{selfName}] Zap PickNewTarget sourceCell={sourceCell} worldId={worldId}");
        List<(GameObject go, float dist)> candidates = new List<(GameObject, float)>();


        // Prefer Health (dupes/critters)
        if (Components.Health != null)
        {
            foreach (Health health in Components.Health.GetWorldItems(worldId))
            {
                if (health == null) continue;
                GameObject go = health.gameObject;
                if (go == null || go == gameObject || !HasValidComponents(go)) continue;
                int targetCell = Grid.PosToCell(health);
                float dist = Vector2.Distance(Grid.CellToPos2D(sourceCell), Grid.CellToPos2D(targetCell));
                if (dist <= SHOCK_RADIUS && !CheckBlocked(sourceCell, targetCell))
                    candidates.Add((go, dist));
            }
        }

        if (candidates.Count == 0)
        {
            if (DEBUG_ZAP) Debug.Log($"[{selfName}] Zap PickNewTarget NO CANDIDATES");
            target = null;
            return;
        }
        if (DEBUG_ZAP) Debug.Log($"[{selfName}] Zap PickNewTarget candidates={candidates.Count}");


        // Sort by distance, prefer closest != previousTarget
        candidates = candidates.OrderBy(c => c.dist).ToList();
        target = candidates.FirstOrDefault(c => c.go != previousTarget).go ?? candidates[0].go;
        if (DEBUG_ZAP && target != null) Debug.Log($"[{selfName}] Zap PickNewTarget PICKED {target.name} prev={(previousTarget != null ? previousTarget.name : "null")}");
    }


    private void ShowDamageBeam(bool show)
    {
        if (beamFX != null)
            beamFX.enabled = show;
        if (impactFX != null)
            impactFX.enabled = show;
    }

    private static bool CheckBlocked(int sourceCell, int destCell)
    {
        HashSet<int> cells = new HashSet<int>();
        Grid.CollectCellsInLine(sourceCell, destCell, cells);
        foreach (int cell in cells)
            if (Grid.Solid[cell]) return true;
        return false;
    }

private void MakeBeam()
    {
        // Always make all beam components, but damaging initially disabled
        GameObject beamGO = new GameObject("shockFX");
        beamGO.SetActive(false);
        beamFX = beamGO.AddComponent<KBatchedAnimController>();
        beamFX.SwapAnims(new KAnimFile[] { Assets.GetAnim("bionic_dupe_stress_beam_fx_kanim") });
        beamGO.SetActive(true);
        sourcePos = GetSourcePosition() + DamageBeamOffset;
        beamFX.transform.position = sourcePos;

        beamFX.Play("beam1", KAnim.PlayMode.Loop);
        beamFX.enabled = false;

        GameObject impactGO = new GameObject("impactFX");
        impactGO.SetActive(false);
        impactFX = impactGO.AddComponent<KBatchedAnimController>();
        impactFX.SwapAnims(new KAnimFile[] { Assets.GetAnim("bionic_dupe_stress_beam_impact_fx_kanim") });
        impactGO.SetActive(true);
        impactFX.Play("stress_beam_impact_fx", KAnim.PlayMode.Loop);
        impactFX.enabled = false;

        // Always make decorative FX when zapping starts
        GameObject faceGO = new GameObject("faceLightningFX");
        faceGO.SetActive(false);
        faceLightningFX = faceGO.AddComponent<KBatchedAnimController>();
        faceLightningFX.SwapAnims(new KAnimFile[] { Assets.GetAnim("bionic_dupe_stress_lightning_fx_kanim") });
        faceLightningFX.transform.localScale = Vector3.one * DecorativeBeamScale;
        faceGO.SetActive(true);
        faceGO.transform.position = GetFaceOrigin() + (Vector3)DecorativeBeamOffset;
        faceLightningFX.Play("lightning", KAnim.PlayMode.Loop);
    }

    private void MakeDamageBeamOnly()
    {
        if (OnlyDamageBeamWhenTargeted && faceLightningFX != null)
        {
            UnityEngine.Object.Destroy(faceLightningFX.gameObject);
            faceLightningFX = null;
        }
        if (target != null && beamFX == null)
        {
            MakeBeam(); // Recreate if needed
        }
    }

    private Vector3 GetSourcePosition()
    {
        KBatchedAnimController kbac = gameObject.GetComponent<KBatchedAnimController>();
        if (kbac != null)
        {
            bool symbolVisible;
            Matrix4x4 snapTransform = kbac.GetSymbolTransform(new HashedString("snapTo_hat"), out symbolVisible);
            if (symbolVisible)
            {
                Vector3 pos = snapTransform.GetColumn(3);
                pos -= Vector3.up * 0.25f;
                pos.z = transform.position.z + 0.01f;
                return pos;
            }
        }
        Vector3 fallback = transform.position + Vector3.up * 1.5f;
        fallback.z = transform.position.z + 0.01f;
        return fallback;
    }

    private Vector3 GetFaceOrigin()
    {
        KBatchedAnimController kbac = gameObject.GetComponent<KBatchedAnimController>();
        if (kbac != null)
        {
            bool symbolVisible;
            Matrix4x4 snapTransform = kbac.GetSymbolTransform(new HashedString("snapTo_hat"), out symbolVisible);
            if (symbolVisible)
            {
                Vector3 pos = snapTransform.GetColumn(3);
                pos -= Vector3.up * 0.25f;
                pos.z = Grid.GetLayerZ(Grid.SceneLayer.FXFront);
                return pos;
            }
        }
        return transform.position;
    }

private void AimBeam(Vector3 targetPos)
    {
        if (beamFX == null) return;

        Vector3 pos = sourcePos;
        beamFX.transform.position = pos;
        float angle = MathUtil.AngleSigned(Vector3.up, Vector3.Normalize(targetPos - pos), Vector3.forward) + 90f;
        beamFX.transform.rotation = Quaternion.Euler(0, 0, angle);
        if (impactFX != null) impactFX.transform.position = targetPos;

        float dist = Vector3.Distance(pos with { z = 0 }, targetPos with { z = 0 });
        string anim = dist > 3 ? "beam3" : dist > 2 ? "beam2" : "beam1";
        if (beamFX.CurrentAnim?.name != anim)
            beamFX.Play(anim, KAnim.PlayMode.Loop);
        beamFX.animWidth = dist / (anim == "beam3" ? 3f : anim == "beam2" ? 2f : 1f);
    }

    private void ShockUpdateRender(float dt)
    {
        if (DEBUG_ZAP && hasValidTarget) Debug.Log($"[{gameObject.name}] Zap ShockUpdateRender dt={dt:F2} cooldown={beamCooldown:F2} target={target?.name}");
        if (faceLightningFX != null)
            faceLightningFX.transform.position = GetFaceOrigin() + (Vector3)DecorativeBeamOffset;


        if (!isActive || !hasValidTarget || beamCooldown > 0f)
        {
            // Fast cooldown tick in render for smooth fade
            if (beamCooldown > 0f) beamCooldown -= dt * 5f; // ~every tick when off
            return;
        }

        Vector3 targetPos = target.transform.position + Vector3.up * 0.5f;
        int sourceCell = Grid.PosToCell(transform.position);
        int targetCell = Grid.PosToCell(targetPos);
        if (!CheckBlocked(sourceCell, targetCell))
        {
            if (DEBUG_ZAP) Debug.Log($"[{gameObject.name}] Zap AimBeam NOT BLOCKED");
            AimBeam(targetPos);
            if (faceLightningFX != null)
                faceLightningFX.flipX = targetPos.x < faceLightningFX.transform.position.x;
            beamCooldown = BEAM_INTERVAL; // Start cooldown after aim
        }
        else
        {
            if (DEBUG_ZAP) Debug.Log($"[{gameObject.name}] Zap AimBeam BLOCKED source={sourceCell} target={targetCell}");
            beamCooldown = 0f; // Retry soon if blocked
        }
    }


    private void ShockUpdate200(float dt)
    {
        if (!hasValidTarget) return;

        damageAccumulator += dt;
        if (damageAccumulator >= 1f)
        {
            damageAccumulator -= 1f;
            float damage = DAMAGE_RATE;
            if (DEBUG_ZAP) Debug.Log($"[{gameObject.name}] Zap DAMAGE {damage} to {target.name}");

            Health health = target.GetComponent<Health>();
            if (health != null)
            {
                health.Damage(damage);
            }
        }
    }


private void ClearBeam()
    {
        if (beamFX != null) 
        {
            UnityEngine.Object.Destroy(beamFX.gameObject);
            beamFX = null;
        }
        if (faceLightningFX != null) 
        {
            UnityEngine.Object.Destroy(faceLightningFX.gameObject);
            faceLightningFX = null;
        }
        if (impactFX != null) 
        {
            UnityEngine.Object.Destroy(impactFX.gameObject);
            impactFX = null;
        }
        previousTarget = target;
        target = null;
        beamCooldown = 0f;
    }

    public override void OnCleanUp()
    {
        StopZapping();
        base.OnCleanUp();
    }

private bool IsValidSource()
    {
        return this.enabled && this.transform != null && !this.IsNullOrDestroyed() && 
               (GetComponent<BuildingHP>()?.HitPoints > 0 || GetComponent<Electrobank>() != null);
    }

    private bool IsValid()
    {
        return IsValidSource();
    }
}


