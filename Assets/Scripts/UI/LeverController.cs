using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SlotMachine.UI
{
    /// <summary>
    /// Visual-only lever. Swaps between the supplied "up" (slot-machine2) and "pulled"
    /// (slot-machine3) sprites. It never generates outcomes; input is routed through
    /// SlotMachineController.PullLever, which uses the same validated spin path as the Spin button.
    /// </summary>
    public sealed class LeverController : MonoBehaviour
    {
        [SerializeField] private Image leverImage;
        [SerializeField] private Sprite upSprite;
        [SerializeField] private Sprite pulledSprite;
        [SerializeField, Min(0.05f)] private float pulledHoldTime = 0.3f;

        private Coroutine pullRoutine;

        public void PlayPull()
        {
            if (leverImage == null || upSprite == null || pulledSprite == null)
            {
                Debug.LogWarning($"{name}: lever sprites are not assigned; skipping lever animation.", this);
                return;
            }

            if (pullRoutine != null)
                StopCoroutine(pullRoutine);
            if (isActiveAndEnabled)
                pullRoutine = StartCoroutine(PullRoutine());
        }

        private void OnDisable()
        {
            pullRoutine = null;
            if (leverImage != null && upSprite != null)
                leverImage.sprite = upSprite;
        }

        private IEnumerator PullRoutine()
        {
            leverImage.sprite = pulledSprite;
            float t = 0f;
            while (t < pulledHoldTime)
            {
                t += Time.deltaTime;
                yield return null;
            }

            leverImage.sprite = upSprite;
            pullRoutine = null;
        }
    }
}
