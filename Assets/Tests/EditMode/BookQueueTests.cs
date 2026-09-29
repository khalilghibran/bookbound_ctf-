using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BookBound.Tests
{
    public class BookQueueTests
    {
        [Test]
        public void ShuffleRemaining_KeepsEveryTableBookAndChangesTheActiveBook()
        {
            GenreDefinition genre = null;
            GameObject root = null;
            GameObject prefab = null;
            try
            {
                genre = ScriptableObject.CreateInstance<GenreDefinition>();
                root = new GameObject("Queue", typeof(RectTransform), typeof(BookQueue));
                var queue = root.GetComponent<BookQueue>();
                var anchors = new RectTransform[5];
                for (int i = 0; i < anchors.Length; i++)
                {
                    var anchor = new GameObject($"Anchor {i}", typeof(RectTransform));
                    anchors[i] = (RectTransform)anchor.transform;
                    anchors[i].SetParent(root.transform, false);
                }
                SetPrivateField(queue, "anchors", anchors);

                prefab = new GameObject("Book Prefab", typeof(RectTransform), typeof(Image), typeof(BookView));
                SetPrivateField(prefab.GetComponent<BookView>(), "image", prefab.GetComponent<Image>());

                var dealt = new List<BookData>();
                for (int number = 1; number <= 8; number++) dealt.Add(new BookData(genre, number));
                queue.Populate(dealt, prefab);

                BookData previousActive = anchors[0].GetComponentInChildren<BookView>().Data;
                Assert.IsTrue(queue.ShuffleRemaining(42));
                Assert.AreNotEqual(previousActive, anchors[0].GetComponentInChildren<BookView>().Data);
                AssertTableContains(queue, anchors, dealt);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (prefab != null) Object.DestroyImmediate(prefab);
                if (genre != null) Object.DestroyImmediate(genre);
            }
        }

        [Test]
        public void ShuffleRemaining_UsesTheBookActuallyInTheActiveSlotAfterASwap()
        {
            GenreDefinition genre = null;
            GameObject root = null;
            GameObject prefab = null;
            try
            {
                genre = ScriptableObject.CreateInstance<GenreDefinition>();
                root = new GameObject("Queue", typeof(RectTransform), typeof(BookQueue));
                var queue = root.GetComponent<BookQueue>();
                var anchors = new RectTransform[5];
                for (int i = 0; i < anchors.Length; i++)
                {
                    var anchor = new GameObject($"Anchor {i}", typeof(RectTransform));
                    anchors[i] = (RectTransform)anchor.transform;
                    anchors[i].SetParent(root.transform, false);
                }
                SetPrivateField(queue, "anchors", anchors);

                prefab = new GameObject("Book Prefab", typeof(RectTransform), typeof(Image), typeof(BookView));
                SetPrivateField(prefab.GetComponent<BookView>(), "image", prefab.GetComponent<Image>());

                var dealt = new List<BookData>();
                for (int number = 1; number <= 8; number++) dealt.Add(new BookData(genre, number));
                queue.Populate(dealt, prefab);

                var oldActive = anchors[0].GetComponentInChildren<BookView>();
                oldActive.transform.SetParent(root.transform, false); // it has moved to a shelf
                var incoming = Object.Instantiate(prefab).GetComponent<BookView>();
                incoming.Bind(new BookData(genre, 9));
                incoming.FillParent(anchors[0]);
                anchors[0].GetComponent<TableSlot>().AssignOccupant(incoming);

                Assert.IsTrue(queue.ShuffleRemaining(42));
                dealt.RemoveAt(0);
                dealt.Add(new BookData(genre, 9));
                AssertTableContains(queue, anchors, dealt);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (prefab != null) Object.DestroyImmediate(prefab);
                if (genre != null) Object.DestroyImmediate(genre);
            }
        }

        private static void AssertTableContains(BookQueue queue, RectTransform[] anchors, List<BookData> expected)
        {
            var actual = new HashSet<BookData>();
            foreach (RectTransform anchor in anchors)
            {
                var book = anchor.GetComponentInChildren<BookView>();
                if (book != null) Assert.IsTrue(actual.Add(book.Data), "duplicate visible book");
            }

            var backlog = (List<BookData>)GetPrivateField(queue, "_backlog");
            foreach (BookData book in backlog) Assert.IsTrue(actual.Add(book), "duplicate backlog book");
            Assert.AreEqual(expected.Count, actual.Count);
            foreach (BookData book in expected) Assert.IsTrue(actual.Contains(book), $"missing {book}");
        }

        private static object GetPrivateField(object target, string name) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void SetPrivateField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
