using UnityEngine;

/// <summary>
/// Translucent googly-eyed sea squirt creature that pops up from the sewage water
/// and ducks back down as the player approaches. Purely cosmetic.
/// Inspired by real sea squirts with eye-stalks.
/// </summary>
public class SewerSquirt : MonoBehaviour
{
    [Header("Behavior")]
    public float popUpSpeed = 3f;
    public float duckSpeed = 5f;
    public float scareRange = 6f;
    public float peekHeight = 0.25f;

    private Transform _player;
    private float _currentHeight;
    private float _targetHeight;
    private float _idleTimer;
    private float _peekDelay;
    private bool _scared;
    private float _scaredTimer;

    // Eye stalks
    private Transform _leftStalk;
    private Transform _rightStalk;
    private float _blinkTimer;
    private float _stalkPhase;

    void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.player != null)
            _player = GameManager.Instance.player.transform;

        _currentHeight = -peekHeight; // start hidden
        _targetHeight = peekHeight;
        _peekDelay = Random.Range(0.5f, 3f);
        _idleTimer = _peekDelay;
        _stalkPhase = Random.value * Mathf.PI * 2f;

        _leftStalk = CreatureAnimUtils.FindChildRecursive(transform, "leftstalk");
        _rightStalk = CreatureAnimUtils.FindChildRecursive(transform, "rightstalk");
    }

    void Update()
    {
        if (_player == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.player != null)
                _player = GameManager.Instance.player.transform;
            else return;
        }

        float distSqr = (_player.position - transform.position).sqrMagnitude;

        // Scare check - duck when player gets close
        if (distSqr < scareRange * scareRange && !_scared)
        {
            _scared = true;
            _scaredTimer = 0f;
            _targetHeight = -peekHeight * 1.5f; // duck below water
        }

        if (_scared)
        {
            _scaredTimer += Time.deltaTime;
            if (_scaredTimer > 2f && distSqr > scareRange * scareRange * 2f)
            {
                _scared = false;
                _idleTimer = Random.Range(1f, 4f);
            }
        }
        else
        {
            // Idle behavior: peek up, look around, duck back
            _idleTimer -= Time.deltaTime;
            if (_idleTimer <= 0f)
            {
                _targetHeight = _targetHeight > 0 ? -peekHeight * 0.5f : peekHeight;
                _idleTimer = Random.Range(2f, 5f);
            }
        }

        // Smooth height transition
        float speed = _currentHeight > _targetHeight ? duckSpeed : popUpSpeed;
        _currentHeight = Mathf.Lerp(_currentHeight, _targetHeight, Time.deltaTime * speed);

        Vector3 pos = transform.localPosition;
        pos.y = _currentHeight;
        transform.localPosition = pos;

        // Eye stalk wobble
        float t = Time.time + _stalkPhase;
        if (_leftStalk != null)
        {
            float wobble = CreatureAnimUtils.OrganicWobble(t, 1.2f, 2.1f, 8f, 4f);
            _leftStalk.localRotation = Quaternion.Euler(wobble, wobble * 0.5f, 0);
        }
        if (_rightStalk != null)
        {
            float wobble = CreatureAnimUtils.OrganicWobble(t + 0.7f, 1.5f, 1.8f, 8f, 4f);
            _rightStalk.localRotation = Quaternion.Euler(wobble, -wobble * 0.5f, 0);
        }

        // Eye blink
        _blinkTimer -= Time.deltaTime;
        if (_blinkTimer <= 0f)
        {
            _blinkTimer = Random.Range(2f, 6f);
            // Quick scale pulse on eyes for blink
        }

        // Gentle body sway
        float sway = CreatureAnimUtils.OrganicWobble(t, 0.5f, 0.9f, 3f, 1.5f);
        transform.localRotation = Quaternion.Euler(sway, 0, sway * 0.5f);
    }
}
