using UnityEngine;

namespace NamPhuThuy.AssetPipelineTools
{
    /// <summary>
    /// Animates a shader property in sync across all child renderers.
    /// Works in both Edit Mode (using [ExecuteAlways]) and Play Mode.
    /// Includes a customizable pause delay at the boundary values.
    /// </summary>
    [ExecuteAlways]
    public class VariantTestAnimator : MonoBehaviour
    {
        #region Private Fields
        [Header("Animation Settings")]
        [SerializeField] private string _propertyName = "_Progress";
        [SerializeField] private float _startValue = 0f;
        [SerializeField] private float _endValue = 1f;
        [SerializeField] private float _duration = 2.0f;
        [SerializeField] private float _delayAtBounds = 0.5f;
        [SerializeField] private bool _pingPong = true;
        [SerializeField] private bool _animate = true;

        private float _elapsedTime = 0f;
        private float _waitTimer = 0f;
        private bool _isWaiting = false;
        private bool _forward = true;
        private MaterialPropertyBlock _propertyBlock;
        #endregion

        #region Public Methods
        /// <summary>
        /// Configures the animation properties from the editor window script.
        /// </summary>
        public void Configure(string propName, float start, float end, float duration, float delayAtBounds = 0.5f)
        {
            _propertyName = propName;
            _startValue = start;
            _endValue = end;
            _duration = duration;
            _delayAtBounds = delayAtBounds;
        }
        #endregion

        #region Unity Callbacks
        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (!_animate) return;
            if (string.IsNullOrEmpty(_propertyName)) return;
            if (_duration <= 0.0f) return;

            // Handle pause delay at boundaries
            if (_isWaiting)
            {
                _waitTimer += Time.deltaTime;
                if (_waitTimer >= _delayAtBounds)
                {
                    _isWaiting = false;
                    _waitTimer = 0f;
                    _elapsedTime = 0f;
                    if (_pingPong)
                    {
                        _forward = !_forward;
                    }
                }
            }
            else
            {
                _elapsedTime += Time.deltaTime;
                if (_elapsedTime >= _duration)
                {
                    _elapsedTime = _duration;
                    _isWaiting = true;
                    _waitTimer = 0f;
                }
            }

            // Calculate progress value
            float currentProgress = Mathf.Clamp01(_elapsedTime / _duration);
            if (!_forward)
            {
                currentProgress = 1f - currentProgress;
            }

            float finalValue = Mathf.Lerp(_startValue, _endValue, currentProgress);

            // Fetch renderers in children
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null) return;

            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rend = renderers[i];
                if (rend == null)
                {
                    continue;
                }

                // Apply float value through MaterialPropertyBlock to avoid dirtying material assets on disk
                rend.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(_propertyName, finalValue);
                rend.SetPropertyBlock(_propertyBlock);
            }
        }
        #endregion
    }
}
