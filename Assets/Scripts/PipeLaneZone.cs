using UnityEngine;

/// <summary>
/// Lane zone: the pipe widens from circular to a pill/oval shape.
/// Left side = SAFE (fewer obstacles, fewer speed boosts).
/// Right side = RISKY (more obstacles, but speed boosts + extra coins as reward).
/// No fork warning — the player just feels the pipe gradually widen.
/// </summary>
public class PipeLaneZone
{
    public float startDistance;   // where widening begins
    public float endDistance;     // where narrowing ends (full zone span)
    public float peakWidth;      // horizontal stretch multiplier at widest (e.g. 2.0)

    private float _transitionIn;  // meters to go from 1x to peakWidth
    private float _holdLength;    // meters at full width
    private float _transitionOut; // meters to go from peakWidth back to 1x

    public PipeLaneZone(float start, float transIn, float hold, float transOut, float peak = 2.0f)
    {
        startDistance = start;
        _transitionIn = transIn;
        _holdLength = hold;
        _transitionOut = transOut;
        peakWidth = peak;
        endDistance = start + transIn + hold + transOut;
    }

    /// <summary>True if distance is anywhere in this zone (including transitions).</summary>
    public bool Contains(float distance)
    {
        return distance >= startDistance && distance <= endDistance;
    }

    /// <summary>
    /// Returns the horizontal width multiplier at the given distance.
    /// 1.0 = normal circular pipe, peakWidth = fully widened pill shape.
    /// Uses smoothstep for organic feel.
    /// </summary>
    public float GetWidthMultiplier(float distance)
    {
        if (distance < startDistance || distance > endDistance)
            return 1f;

        float d = distance - startDistance;

        // Phase 1: transition in (widen)
        if (d < _transitionIn)
        {
            float t = d / _transitionIn;
            return Mathf.Lerp(1f, peakWidth, Smoothstep(t));
        }

        // Phase 2: hold at full width
        if (d < _transitionIn + _holdLength)
            return peakWidth;

        // Phase 3: transition out (narrow back)
        float outD = d - _transitionIn - _holdLength;
        float tOut = outD / _transitionOut;
        return Mathf.Lerp(peakWidth, 1f, Smoothstep(tOut));
    }

    /// <summary>
    /// Returns lane side based on circumferential angle.
    /// -1 = left (safe), +1 = right (risky).
    /// Angle convention: 0=right, 90=top, 180=left, 270=bottom.
    /// </summary>
    public static int GetLaneSide(float angleDeg)
    {
        // Normalize angle to 0-360
        float a = ((angleDeg % 360f) + 360f) % 360f;
        // Left side: 90-270 (left half of pipe)
        // Right side: 270-360 + 0-90 (right half of pipe)
        return (a >= 90f && a < 270f) ? -1 : 1;
    }

    /// <summary>
    /// Obstacle spawn multiplier. Left (safe) side has fewer, right (risky) has more.
    /// Returns a multiplier on base obstacle chance.
    /// </summary>
    public float GetObstacleMultiplier(float angleDeg)
    {
        int side = GetLaneSide(angleDeg);
        if (side < 0)
            return 0.35f;  // safe side: 35% obstacle chance
        else
            return 1.6f;   // risky side: 160% obstacle chance
    }

    /// <summary>
    /// Coin spawn multiplier. Risky side gets extra coins as reward.
    /// </summary>
    public float GetCoinMultiplier(float angleDeg)
    {
        int side = GetLaneSide(angleDeg);
        return side > 0 ? 2.0f : 0.7f;
    }

    /// <summary>
    /// Speed boost multiplier. Risky side gets speed boosts, safe side gets jump ramps.
    /// </summary>
    public float GetSpeedBoostChance(float angleDeg)
    {
        int side = GetLaneSide(angleDeg);
        return side > 0 ? 0.85f : 0.15f; // 85% of boosts on risky side
    }

    static float Smoothstep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
