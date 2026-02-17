using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Classic cartoon bomb mine that floats in the sewer water.
/// Only the top and lit fuse poke above the murky surface.
/// Fuse spark flickers and intensifies when player approaches.
/// On detonation, triggers a charred effect on Mr. Corny.
/// </summary>
public class SewerMineBehavior : ObstacleBehavior
{
    private Transform _fuseSparkLight;
    private Renderer _sparkRenderer;
    private MaterialPropertyBlock _mpb;
    private Vector3 _originalPos;
    private float _bobPhase;

    // Fuse spark particle
    private ParticleSystem _fuseSparkPS;

    protected override void Start()
    {
        base.Start();
        _fuseSparkLight = CreatureAnimUtils.FindChildRecursive(transform, "warning");
        if (_fuseSparkLight == null) _fuseSparkLight = CreatureAnimUtils.FindChildRecursive(transform, "light");
        if (_fuseSparkLight == null) _fuseSparkLight = CreatureAnimUtils.FindChildRecursive(transform, "WarnLight");
        if (_fuseSparkLight != null)
            _sparkRenderer = _fuseSparkLight.GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _originalPos = transform.localPosition;
        _bobPhase = Random.value * Mathf.PI * 2f;

        // Create fuse spark particles
        CreateFuseSparkParticles();
    }

    void CreateFuseSparkParticles()
    {
        var go = new GameObject("FuseSparks");
        go.transform.SetParent(transform);
        // Position at the fuse tip
        go.transform.localPosition = new Vector3(0.06f, 1.08f, 0.1f);
        _fuseSparkPS = go.AddComponent<ParticleSystem>();

        var main = _fuseSparkPS.main;
        main.maxParticles = 20;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.8f, 0.2f, 1f),
            new Color(1f, 0.4f, 0.05f, 0.8f));
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.3f; // sparks float up

        var emission = _fuseSparkPS.emission;
        emission.rateOverTime = 8;

        var shape = _fuseSparkPS.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.02f;

        var sizeOverLife = _fuseSparkPS.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null || shader.name.Contains("Error"))
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null || shader.name.Contains("Error"))
            shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(1f, 0.7f, 0.1f, 0.9f));
        mat.SetColor("_Color", new Color(1f, 0.7f, 0.1f, 0.9f));
        mat.SetFloat("_Surface", 1);
        mat.SetFloat("_Blend", 0);
        mat.SetFloat("_ZWrite", 0);
        mat.SetFloat("_Cull", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 3000;
        renderer.material = mat;
    }

    protected override void DoIdle()
    {
        float t = Time.time + _bobPhase;

        // Bob gently on the water surface
        float bob = CreatureAnimUtils.BreathingOffset(t, 0.4f, 0.03f);
        Vector3 pos = _originalPos;
        pos.y += bob;
        transform.localPosition = pos;

        // Very slow lazy spin (floating)
        transform.Rotate(Vector3.up, 5f * Time.deltaTime, Space.Self);

        // Fuse spark - gentle flicker
        if (_sparkRenderer != null)
        {
            float flicker = 0.5f + Mathf.PerlinNoise(t * 3f, 0) * 0.5f;
            Color glow = Color.Lerp(new Color(1f, 0.5f, 0.05f) * 1.5f, new Color(1f, 0.8f, 0.2f) * 3f, flicker);
            _sparkRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", glow);
            _sparkRenderer.SetPropertyBlock(_mpb);
        }
    }

    protected override void DoReact()
    {
        float t = Time.time;

        // Faster bob - agitated
        float bob = Mathf.Sin(t * 4f + _bobPhase) * 0.06f;
        Vector3 pos = _originalPos;
        pos.y += bob;
        transform.localPosition = pos;

        // Faster spin
        transform.Rotate(Vector3.up, 25f * Time.deltaTime, Space.Self);

        // Fuse spark - rapid intense flicker (it's about to blow!)
        if (_sparkRenderer != null)
        {
            float rapid = Mathf.Sin(t * 20f) > 0 ? 1f : 0.3f;
            Color glow = new Color(1f, 0.6f, 0.1f) * (rapid * 5f);
            _sparkRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", glow);
            _sparkRenderer.SetPropertyBlock(_mpb);
        }

        // Intensify fuse sparks
        if (_fuseSparkPS != null)
        {
            var em = _fuseSparkPS.emission;
            em.rateOverTime = 25; // more sparks when player near
        }

        // Proximity beep
        float timeSinceReact = t - _reactTime;
        if (timeSinceReact < 0.1f && TryPlaySound())
        {
            if (ProceduralAudio.Instance != null)
                ProceduralAudio.Instance.PlayBarrelBeep();
        }
    }

    public override void OnPlayerHit(Transform player)
    {
        StartCoroutine(MineDetonateAnim(player));
    }

    private System.Collections.IEnumerator MineDetonateAnim(Transform player)
    {
        if (ProceduralAudio.Instance != null)
            ProceduralAudio.Instance.PlayMineExplosion();
        if (ParticleManager.Instance != null)
            ParticleManager.Instance.PlayMineExplosion(transform.position);

        // Stop fuse sparks
        if (_fuseSparkPS != null) _fuseSparkPS.Stop();

        Vector3 startScale = transform.localScale;

        // Bomb flash and expand
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float p = t / 0.2f;
            transform.localScale = startScale * (1f + p * 0.8f);

            if (_sparkRenderer != null)
            {
                Color flash = Color.Lerp(new Color(1f, 0.5f, 0.05f) * 6f, Color.white * 4f,
                    Mathf.Sin(t * 60f) > 0 ? 1f : 0f);
                _sparkRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", flash);
                _sparkRenderer.SetPropertyBlock(_mpb);
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        // Apply charred effect to Mr. Corny
        TurdController tc = player.GetComponent<TurdController>();
        if (tc != null)
        {
            CharredEffect charred = player.GetComponent<CharredEffect>();
            if (charred == null) charred = player.gameObject.AddComponent<CharredEffect>();
            charred.ApplyCharred(3f); // charred for 3 seconds
        }

        // Shrink the spent bomb
        t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            float p = t / 0.5f;
            transform.localScale = Vector3.Lerp(startScale * 1.8f, startScale * 0.5f, p);
            yield return null;
        }
        transform.localScale = startScale * 0.5f;
    }
}
