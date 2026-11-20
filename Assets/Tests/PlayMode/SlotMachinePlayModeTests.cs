using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SlotMachine.Core;
using SlotMachine.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlotMachine.Tests
{
    /// <summary>
    /// End-to-end tests against the real MainGame scene with real reel timing.
    /// A scripted random provider fixes the outcome; the reels must still animate to it.
    /// </summary>
    public sealed class SlotMachinePlayModeTests
    {
        private const string SceneName = "MainGame";
        private const float SpinTimeout = 10f;

        private SlotMachineController controller;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<SlotMachineController>();
            Assert.IsNotNull(controller, "MainGame must contain a SlotMachineController.");
            Assert.IsTrue(controller.IsInitialized, "Controller failed validation - check the Console.");
        }

        [UnityTest]
        public IEnumerator WinningSpin_DebitsOnce_PaysOnce_AndStopsOnPlannedSymbols()
        {
            // Every roll = 0 -> every symbol is SEVEN (first interval) -> guaranteed 3 x SEVEN.
            controller.SetRandomProvider(new ScriptedRandom());
            Assert.AreEqual(1000, controller.Balance);
            Assert.AreEqual(10, controller.SelectedBet);

            Assert.IsTrue(controller.TrySpin());
            Assert.AreEqual(GameState.Spinning, controller.State);
            Assert.AreEqual(990, controller.Balance, "Bet must be debited once at spin start.");
            Assert.IsFalse(controller.TrySpin(), "A second spin must not start while spinning.");
            controller.PullLever();
            controller.RequestReset();
            Assert.AreEqual(990, controller.Balance, "Extra input during a spin must not change the balance.");

            yield return WaitForReady();

            Assert.IsTrue(controller.LastEvaluation.IsWin);
            Assert.AreEqual("SEVEN", controller.LastEvaluation.WinningSymbol.Id);
            Assert.AreEqual(1190, controller.Balance, "1000 - 10 + 10 x 20");
            AssertReelsShowPlannedCenter();
        }

        [UnityTest]
        public IEnumerator LosingSpin_PaysNothing()
        {
            // Grid rolls (reel0 top/center/bottom, reel1..., reel2...): centers SEVEN, CHERRIES, BELL.
            controller.SetRandomProvider(new ScriptedRandom(0, 0, 0, 10, 10, 10, 40, 40, 40));
            controller.SelectBet(1); // 50
            Assert.IsTrue(controller.TrySpin());
            yield return WaitForReady();

            Assert.IsFalse(controller.LastEvaluation.IsWin);
            Assert.AreEqual(950, controller.Balance);
            Assert.AreEqual("SEVEN", controller.Reels[0].GetVisibleSymbol(ReelController.CenterRow).Id);
            Assert.AreEqual("CHERRIES", controller.Reels[1].GetVisibleSymbol(ReelController.CenterRow).Id);
            Assert.AreEqual("BELL", controller.Reels[2].GetVisibleSymbol(ReelController.CenterRow).Id);
            AssertReelsShowPlannedCenter();
        }

        [UnityTest]
        public IEnumerator Reset_RestoresCredits_OnlyWhenIdle()
        {
            controller.SetRandomProvider(new ScriptedRandom(0, 0, 0, 10, 10, 10, 40, 40, 40));
            controller.SelectBet(2); // 100
            Assert.IsTrue(controller.TrySpin());
            controller.ConfirmReset();
            Assert.AreEqual(900, controller.Balance, "Reset must be ignored while spinning.");
            yield return WaitForReady();

            controller.RequestReset();
            controller.ConfirmReset();
            Assert.AreEqual(1000, controller.Balance);
            Assert.AreEqual(10, controller.SelectedBet);
        }

        [UnityTest]
        public IEnumerator RealRng_CompletesSpinWithConsistentBalance()
        {
            long before = controller.Balance;
            Assert.IsTrue(controller.TrySpin());
            yield return WaitForReady();
            long expected = before - 10 + controller.LastEvaluation.Payout;
            Assert.AreEqual(expected, controller.Balance);
            AssertReelsShowPlannedCenter();
        }

        private IEnumerator WaitForReady()
        {
            float t = 0f;
            while (controller.State != GameState.Ready && t < SpinTimeout)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(GameState.Ready, controller.State, "Spin did not finish in time.");
            LogAssert.NoUnexpectedReceived();
        }

        private void AssertReelsShowPlannedCenter()
        {
            for (int i = 0; i < SlotMachineController.ReelCount; i++)
                Assert.AreSame(controller.GetPlannedCenter(i), controller.Reels[i].GetVisibleSymbol(ReelController.CenterRow), $"Reel {i}");
        }

        /// <summary>Returns the scripted rolls, then 0 forever.</summary>
        private sealed class ScriptedRandom : IRandomProvider
        {
            private readonly Queue<int> rolls;

            public ScriptedRandom(params int[] rolls) => this.rolls = new Queue<int>(rolls);

            public int Range(int minInclusive, int maxExclusive) => rolls.Count > 0 ? rolls.Dequeue() : minInclusive;
        }
    }
}
