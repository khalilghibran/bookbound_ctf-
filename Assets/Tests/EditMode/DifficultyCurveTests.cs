using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BookBound.Tests
{
    public class DifficultyCurveTests
    {
        private static DifficultyCurve CreateDefaultCurve()
        {
            // Field defaults on DifficultyCurve already match the spec's START_SPM/DECAY/FLOOR_SPM.
            return ScriptableObject.CreateInstance<DifficultyCurve>();
        }

        // Worked examples straight from plan.md section 8's table.
        [TestCase(0, 18, 54)]
        [TestCase(3, 22, 54)]
        [TestCase(6, 26, 51)]
        [TestCase(10, 30, 44)]
        [TestCase(14, 34, 48)]
        [TestCase(18, 36, 51)]
        public void GetShelfDuration_MatchesSpecTable(int shelfIndex, int requiredMoves, float expectedDuration)
        {
            var curve = CreateDefaultCurve();
            float duration = curve.GetShelfDuration(shelfIndex, requiredMoves);
            Assert.AreEqual(expectedDuration, duration, 0.01f);
        }

        [Test]
        public void GetSecondsPerMove_NeverGoesBelowFloor()
        {
            var curve = CreateDefaultCurve();
            float spm = curve.GetSecondsPerMove(100);
            Assert.GreaterOrEqual(spm, 1.4f - 0.0001f);
        }

        [Test]
        public void GetSecondsPerMove_DecreasesAsShelfIndexIncreases()
        {
            var curve = CreateDefaultCurve();
            float spm0 = curve.GetSecondsPerMove(0);
            float spm5 = curve.GetSecondsPerMove(5);
            float spm10 = curve.GetSecondsPerMove(10);

            Assert.Greater(spm0, spm5);
            Assert.Greater(spm5, spm10);
        }

        private static DifficultyCurve CreateCurveWithBands((int minShelfIndex, ShelfLayoutConfig config)[] bandDefs)
        {
            var curve = ScriptableObject.CreateInstance<DifficultyCurve>();
            var so = new SerializedObject(curve);
            var bandsProp = so.FindProperty("bands");
            bandsProp.arraySize = bandDefs.Length;
            for (int i = 0; i < bandDefs.Length; i++)
            {
                var element = bandsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("minShelfIndex").intValue = bandDefs[i].minShelfIndex;
                element.FindPropertyRelative("layoutConfig").objectReferenceValue = bandDefs[i].config;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return curve;
        }

        [Test]
        public void GetLayoutConfig_PicksBandCoveringShelfIndex()
        {
            var early = ScriptableObject.CreateInstance<ShelfLayoutConfig>();
            var mid = ScriptableObject.CreateInstance<ShelfLayoutConfig>();
            var late = ScriptableObject.CreateInstance<ShelfLayoutConfig>();

            var curve = CreateCurveWithBands(new (int, ShelfLayoutConfig)[]
            {
                (0, early),
                (6, mid),
                (14, late),
            });

            Assert.AreEqual(early, curve.GetLayoutConfig(0));
            Assert.AreEqual(early, curve.GetLayoutConfig(5));
            Assert.AreEqual(mid, curve.GetLayoutConfig(6));
            Assert.AreEqual(mid, curve.GetLayoutConfig(13));
            Assert.AreEqual(late, curve.GetLayoutConfig(14));
            Assert.AreEqual(late, curve.GetLayoutConfig(999));
        }

        [Test]
        public void GetLayoutConfig_NoBands_Throws()
        {
            var curve = ScriptableObject.CreateInstance<DifficultyCurve>();
            Assert.Throws<System.InvalidOperationException>(() => curve.GetLayoutConfig(0));
        }
    }
}
