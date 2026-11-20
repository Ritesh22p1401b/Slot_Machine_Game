using UnityEngine;
using UnityEngine.UI;

namespace SlotMachine.UI
{
    public enum PaylineState
    {
        Idle,
        Spinning,
        Win
    }

    /// <summary>Arrow beside the center payline. Swaps between supplied button-sheet states.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class PaylineMarker : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite spinningSprite;
        [SerializeField] private Sprite winSprite;
        [SerializeField, Min(0.05f)] private float winBlinkInterval = 0.2f;

        private PaylineState state;
        private float blinkTimer;
        private bool blinkOn;

        public void SetState(PaylineState newState)
        {
            state = newState;
            blinkTimer = 0f;
            blinkOn = true;
            Apply(newState == PaylineState.Win ? winSprite : newState == PaylineState.Spinning ? spinningSprite : idleSprite);
        }

        private void Awake()
        {
            if (image == null)
                image = GetComponent<Image>();
        }

        private void Update()
        {
            if (state != PaylineState.Win)
                return;

            blinkTimer += Time.deltaTime;
            if (blinkTimer < winBlinkInterval)
                return;

            blinkTimer = 0f;
            blinkOn = !blinkOn;
            Apply(blinkOn ? winSprite : idleSprite);
        }

        private void Apply(Sprite sprite)
        {
            if (image != null && sprite != null)
                image.sprite = sprite;
        }
    }
}
